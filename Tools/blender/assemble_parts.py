"""Assemble la table à partir des pièces du dépôt `vbousquet/pinball-parts`.

`build_table.py` engendre le plateau, les murs et l'orbit — la géométrie que le dépôt **ne
contient pas** : `pinball-parts` est un magasin de pièces, pas un décor. Tout le reste
(bumpers, cibles, poteaux) y est modélisé à l'échelle réelle, et c'est ce que ce script pose.

Le partage est donc explicite :

| Origine  | Objets |
| --- | --- |
| engendré | `Playfield`, `Walls`, `Orbit` |
| dépôt    | `Bumpers` (socle + anneau + chapeau), `Targets` (16 cibles), `Posts` (4 poteaux) |

**Les slingshots ne sont plus engendrés** (2026-09-15). Ils l'étaient faute d'équivalent dans le
dépôt ; `Plastic with decal` (`Miscellaneous`) les remplace, posé dans Unity sous les hôtes
`Slingshot_Left` / `Slingshot_Right` — il n'a rien à faire dans ce FBX, pas plus que les
flippers : c'est une pièce à part, placée par un script de scène.

**Les flippers ne sont pas ici**, et ce n'est pas un oubli : ils tournent. Ce sont des
`Rigidbody` + `HingeJoint` pilotés par `Flipper.cs`, pas des colliders statiques — les inclure
dans `Pinball_Table.fbx` les figerait. Ils restent des pièces à part, posées dans Unity sous
`Flipper_Bat`.

Le repère est celui de `build_table.py`, et `to_blender` y est **réutilisé** au lieu d'être
recopié : c'est lui qui compense la réflexion qu'Unity applique à la lecture, et deux copies
divergeraient tôt ou tard. Les pièces du dépôt sont donc miroitées dans Blender pour ressortir
droites dans Unity — ce qui règle au passage leur chiralité, le miroir d'Unity étant annulé.

    blender --background --factory-startup --python assemble_parts.py -- --verify

Écrit `Assets/Models/Table/Pinball_Table.fbx` — le même chemin que `build_table.py`, dont ce
script prend le relais pour le lot « table ».
"""

import argparse
import json
import math
import os
import sys

import bmesh
import bpy
import mathutils

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import build_table as table                                        # noqa: E402

INVENTORY = os.path.abspath(os.path.join(HERE, "..", "pinball-parts-inventory.json"))

# Le quart de tour qui redresse une pièce du dépôt : `(y, z) -> (z, -y)`. Un `.blend` a son axe
# vertical en `z`, la table en `y` — et comme les coordonnées du dépôt sont lues telles quelles
# en repère de table, c'est la seule rotation qui remette le haut en haut.
REDRESSEMENT = mathutils.Matrix.Rotation(math.radians(-90.0), 4, "X")


# --------------------------------------------------------------------------- ce qui reste engendré

# Le dépôt n'a ni plateau, ni murs, ni orbit. `bumpers`, `targets` et `posts` sont remplacés par
# les pièces réelles ; les garder ferait deux fois la même chose, à la même place. Les `slingshots`
# ont suivi le même chemin le 2026-09-15, remplacés par `Plastic with decal`.
KEPT_SECTIONS = ("playfield", "walls", "orbit")


# --------------------------------------------------------------------------- placements
#
# x, y, z en repère de table : mètres, x vers la droite, y vers le haut, z vers le haut de la
# table — le repère de `build_table.py`, pas celui de Blender.
#
# `anchor` dit où tombe le point de pose sur la pièce :
#   "base"   le dessous de la boîte englobante — une pièce posée sur le plateau ;
#   "origin" l'origine réglée par le dépôt — un empilement, où chaque couche garde son décalage.

# Le socle Williams/Bally fait 54,9 mm de diamètre pour 37,4 mm de haut : c'est le corps que la
# bille percute. L'anneau et le chapeau se posent dessus, à leur propre hauteur d'origine.
BUMPER_STACK = (
    ("Bumper Base - Williams/Bally", "base"),
    ("Bumper Ring", "plan"),
    ("Bumper Cap - Clear 3\"", "plan"),
)

POST_PART = "Post - Metal - 1\"1/4 - Round"

# Une cible relevée du dépôt est un ensemble monté : la face dépasse du plateau, le support
# descend dessous. `Drop Target` est la plus proche des cibles du GDD.
TARGET_PART = "Drop Target"

# Une cible du dépôt est un ensemble monté qui **traverse** le plateau : 59 mm de haut au total,
# dont la fixation. Les cibles engendrées en faisaient 24 mm, et une vraie cible relevée en
# laisse voir environ 25 mm. On l'enfonce donc d'autant, plutôt que de retailler la pièce : elle
# reste l'asset du dépôt, c'est sa pose qui change.
TARGET_SINK = 0.032


def placements():
    """Les pièces à poser, en repère de table."""
    out = []

    for index, (x, z) in enumerate(table.BUMPERS):
        for position, (name, anchor) in enumerate(BUMPER_STACK):
            out.append({
                "part": name,
                "anchor": anchor,
                "at": (x, 0.0, z),
                "spin": 0.0,
                "label": "Bumper_{:02d}_{}".format(index + 1, position),
            })

    for index, (x, z) in enumerate(table.POSTS):
        out.append({
            "part": POST_PART,
            "anchor": "base",
            "at": (x, 0.0, z),
            "spin": 0.0,
            "label": "Post_{:02d}".format(index + 1),
        })

    counts = {}

    for group, x, z, facing in table.TARGETS:
        counts[group] = counts.get(group, 0) + 1

        out.append({
            "part": TARGET_PART,
            "anchor": "base",
            "at": (x, -TARGET_SINK, z),
            # La rangée `Réseau` regarde vers le bas de la table : sa face fait un demi-tour de
            # plus que les autres.
            "spin": 180.0 if facing == "-z" else 0.0,
            "label": "Target_{}_{:02d}".format(group, counts[group]),
        })

    return out


# --------------------------------------------------------------------------- repère


def blender_of(point):
    """Un point du repère de table vers celui de Blender."""
    return mathutils.Vector(table.to_blender(point[0], point[1], point[2]))


def frame_matrix():
    """La matrice de passage table -> Blender, en 4×4.

    Construite en interrogeant `table.to_blender` sur les trois axes, plutôt que réécrite : la
    sonde `probe_axis.py` a établi que cette conversion est une **réflexion** (déterminant −1),
    et c'est exactement ce qui annule le miroir d'Unity. La recopier ici, c'est risquer de la
    désynchroniser de sa jumelle.
    """
    axes = []

    for axis in range(3):
        unit = [0.0, 0.0, 0.0]
        unit[axis] = 1.0
        axes.append(blender_of(unit))

    return mathutils.Matrix((
        (axes[0].x, axes[1].x, axes[2].x, 0.0),
        (axes[0].y, axes[1].y, axes[2].y, 0.0),
        (axes[0].z, axes[1].z, axes[2].z, 0.0),
        (0.0, 0.0, 0.0, 1.0),
    ))


def table_frame(low, high):
    """Une boîte de Blender relue en repère de table, pour que le rapport se lise à côté des
    coordonnées de `build_table.py`."""
    return (
        -high[0], -low[0],      # table x = -blender x : les bornes s'inversent
        low[2], high[2],        # table y =  blender z
        -high[1], -low[1],      # table z = -blender y
    )


# --------------------------------------------------------------------------- dépôt


def load_inventory():
    with open(INVENTORY, "r", encoding="utf-8") as handle:
        return json.load(handle)


def sources(inventory):
    """Nom de pièce -> chemin absolu du `.blend` qui la contient."""
    found = {}

    for _category, files in inventory["categories"].items():
        for _filename, contents in files.items():
            for part in contents.get("parts", []):
                found.setdefault(part["name"], contents.get("file"))

    return found


def collect(root):
    """La pièce et tous ses descendants."""
    objects = [root]
    stack = list(root.children)

    while stack:
        obj = stack.pop()
        objects.append(obj)
        stack.extend(obj.children)

    return objects


def append_file(path):
    """Importe **tous** les objets d'un `.blend`, sans ouvrir le fichier.

    Tous, et pas seulement la pièce visée : une pièce du dépôt est souvent une hiérarchie, et
    n'apporter que la racine laisserait ses parents derrière — les matrices monde seraient alors
    fausses. La sonde `probe_append.py` l'a montré sur `Plunger`, annoncé à 0 maillage.

    L'append, et non `wm.open_mainfile`, est aussi ce qui **conserve les matériaux** :
    `export_parts.py` peut s'en passer puisqu'il repart d'une scène vide, pas nous.
    """
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = list(src.objects)


def purge_unlinked():
    """Supprime ce que l'append a laissé et qui n'est rattaché à aucune collection.

    Nécessaire avant d'exporter : `export_scene.fbx` n'est pas sélectif, et les 93 objets de
    `Bumpers.blend` — caméra, repères `Setup`, rebuts — partiraient dans le FBX avec les pièces.
    """
    for obj in [o for o in bpy.data.objects if not o.users_collection]:
        bpy.data.objects.remove(obj, do_unlink=True)

    for mesh in [m for m in bpy.data.meshes if m.users == 0]:
        bpy.data.meshes.remove(mesh)


def world_bounds(objects):
    """Boîte englobante des maillages, en coordonnées monde."""
    low = [float("inf")] * 3
    high = [float("-inf")] * 3

    for obj in objects:
        if obj.type != "MESH":
            continue

        matrix = obj.matrix_world

        for corner in obj.bound_box:
            point = matrix @ mathutils.Vector(corner)

            for axis in range(3):
                low[axis] = min(low[axis], point[axis])
                high[axis] = max(high[axis], point[axis])

    if low[0] == float("inf"):
        return None

    return low, high


def anchor_offset(box, anchor, origin):
    """Translation qui amène l'ancre de la pièce sur l'origine.

    Tout se lit en coordonnées **monde du dépôt** : la pièce est prise telle que son fichier la
    présente, pas dans le repère local de son maillage. C'est la seule lecture qui ne dépende pas
    d'une rotation d'objet posée par l'auteur du dépôt.

    En coordonnées monde, `x` et `y` sont horizontaux et `z` vertical : une pièce posée sur le
    plateau se recentre donc dans le plan et vient affleurer en `z`.
    """
    if anchor == "origin":
        # L'origine réglée par le dépôt, pour un empilement : chaque couche garde son décalage,
        # mais la position de la pièce dans **son** fichier ne doit pas suivre.
        return -origin

    low, high = box

    if anchor == "center":
        return mathutils.Vector((
            -(low[0] + high[0]) / 2.0,
            -(low[1] + high[1]) / 2.0,
            -(low[2] + high[2]) / 2.0,
        ))

    if anchor == "plan":
        # Recentrée dans le plan, mais **sans** toucher à la hauteur : c'est l'ancre d'une pièce
        # concentrique — un anneau, un chapeau — dont le décalage vertical est voulu.
        return mathutils.Vector((
            -(low[0] + high[0]) / 2.0,
            -(low[1] + high[1]) / 2.0,
            0.0,
        ))

    return mathutils.Vector((                       # "base"
        -(low[0] + high[0]) / 2.0,
        -(low[1] + high[1]) / 2.0,
        -low[2],
    ))


def recalc_normals(mesh):
    """Rétablit le sens des faces après une réflexion."""
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def place(root, spec, frame):
    """Pose une pièce et retourne sa ligne de rapport.

    La géométrie est **figée** — les sommets sont écrits — au lieu d'être portée par un
    transform. La matrice de passage est une réflexion, et un transform de déterminant négatif
    se propage mal : Unity en retourne le sens des faces. Figer oblige à recalculer les
    normales, ce qui est fait ici et vérifié au volume signé par `build_table.report`.
    """
    source_object = bpy.data.objects.get(spec["part"])

    if source_object is None:
        return "INTROUVABLE", spec["label"], spec["part"], ""

    meshes = [obj for obj in collect(source_object) if obj.type == "MESH"]

    if not meshes:
        return "SANS MAILLAGE", spec["label"], spec["part"], ""

    box = world_bounds(meshes)

    if box is None:
        return "VIDE", spec["label"], spec["part"], ""

    # Trois transformations, appliquées de la droite vers la gauche :
    #
    # 1. `Translation` — amène l'ancre de la pièce sur l'origine, en coordonnées du dépôt ;
    # 2. `Redressement` — l'axe vertical d'un `.blend` est `z`, celui de la table est `y`. Lire
    #    les coordonnées du dépôt comme des coordonnées de table est une identité de *nombres*,
    #    pas d'orientation : sans ce quart de tour, chaque pièce sort couchée sur le dos, son
    #    chapeau de bumper étalé sur 76 mm de long au lieu de 76 mm de large ;
    # 3. `Rotation(spin, "Z")` — le lacet. Appliqué **avant** le redressement, il tourne donc
    #    autour de l'axe qui deviendra la verticale de la table : c'est bien un lacet.
    #
    # Puis `frame`, qui réinterprète le tout en repère de Blender — et dont la réflexion annule
    # le miroir qu'Unity applique à la lecture.
    bake = (frame
            @ REDRESSEMENT
            @ mathutils.Matrix.Rotation(math.radians(spec["spin"]), 4, "Z")
            @ mathutils.Matrix.Translation(
                anchor_offset(box, spec["anchor"], source_object.matrix_world.translation)))

    mirrored = bake.determinant() < 0.0
    created = []
    materials = set()
    triangles = 0
    bare = 0

    for obj in meshes:
        mesh = obj.data.copy()
        mesh.transform(bake @ obj.matrix_world)

        if mirrored:
            recalc_normals(mesh)

        # En Blender 5, la liste des matériaux est portée par le maillage (`mesh.materials`) et
        # non par l'objet : la copie l'emporte donc telle quelle, sans avoir à la recâbler.
        for material in mesh.materials:
            if material is not None:
                materials.add(material.name)
            else:
                bare += 1

        triangles += sum(len(poly.vertices) - 2 for poly in mesh.polygons)

        copy = bpy.data.objects.new("{}_{}".format(spec["label"], obj.name), mesh)
        bpy.context.scene.collection.objects.link(copy)
        copy.parent = root
        copy.matrix_parent_inverse = mathutils.Matrix.Identity(4)
        copy.location = blender_of(spec["at"])
        created.append(copy)

    # Les matrices monde ne sont calculées qu'à l'évaluation du depsgraph : sans cette ligne,
    # `world_bounds` relirait la pose précédente et le rapport décrirait une autre scène.
    bpy.context.view_layer.update()

    placed = world_bounds(created)
    span = table_frame(placed[0], placed[1]) if placed else (0.0,) * 6

    note = "{} tris   x {:+.3f}→{:+.3f}  y {:+.3f}→{:+.3f}  z {:+.3f}→{:+.3f}   {}".format(
        triangles, span[0], span[1], span[2], span[3], span[4], span[5],
        ", ".join(sorted(materials))[:44] if materials else "AUCUN MATÉRIAU")

    if bare:
        note += "   ⚠ {} emplacement(s) sans matériau".format(bare)

    return "POSÉ", spec["label"], spec["part"], note


# --------------------------------------------------------------------------- construction


def build(root, specs, frame, args):
    """Engendre les sections conservées, puis pose les pièces du dépôt."""
    table._GROUPS.clear()

    for section in KEPT_SECTIONS:
        table.SECTIONS[section](root)

    by_source = {}
    missing = []

    for spec in specs:
        path = args.sources.get(spec["part"])

        if not path or not os.path.isfile(path):
            missing.append(spec)
            continue

        by_source.setdefault(path, []).append(spec)

    rows = []

    for path in sorted(by_source):
        append_file(path)

        for spec in by_source[path]:
            rows.append(place(root, spec, frame))

        # Après chaque fichier, et pas seulement à la fin : deux `.blend` du dépôt partagent des
        # noms d'objets, et un `Bumper Ring` resté en mémoire serait repris par le suivant.
        purge_unlinked()

    for spec in missing:
        rows.append(("ABSENT DU DÉPÔT", spec["label"], spec["part"], ""))

    return rows


def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1:] if "--" in argv else []

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", default="Assets/Models/Table",
                        help="dossier de sortie du FBX")
    parser.add_argument("--inventory", default=INVENTORY,
                        help="inventaire du dépôt (défaut : Tools/pinball-parts-inventory.json)")
    parser.add_argument("--verify", action="store_true",
                        help="réimporte le FBX écrit et mesure ce qu'il contient")
    parser.add_argument("--units-per-meter", type=float, default=table.UNITS_PER_METER)
    parser.add_argument("--no-export", action="store_true")
    return parser.parse_args(argv)


def main():
    args = parse_args()
    args.sources = sources(load_inventory())

    specs = placements()
    frame = frame_matrix()

    print("=" * 78)
    print("Assemblage de la table — {} pièce(s) du dépôt, {} section(s) engendrée(s)".format(
        len(specs), len(KEPT_SECTIONS)))
    print("Échelle : {:.4f} unités Unity par mètre".format(args.units_per_meter))
    print("=" * 78)

    bpy.ops.wm.read_factory_settings(use_empty=True)

    root = bpy.data.objects.new("PinballTable_Model", None)
    bpy.context.scene.collection.objects.link(root)
    root.empty_display_size = 0.05
    bpy.context.view_layer.objects.active = root

    rows = build(root, specs, frame, args)

    print("\n--- pièces posées " + "-" * 60)

    for status, label, part, note in rows:
        print("  {:<14} {:<22} {:<34} {}".format(status, label[:22], part[:34], note))

    placed = sum(1 for row in rows if row[0] == "POSÉ")
    print("\n  {} posée(s), {} en échec".format(placed, len(rows) - placed))

    table.report(("playfield", "walls", "orbit", "bumpers", "targets", "posts"))

    if args.no_export:
        print("\n(--no-export : rien n'a été écrit)")
        return

    path = os.path.abspath(os.path.join(args.out, "Pinball_Table.fbx"))
    table.export(root, path, args.units_per_meter)
    print("\n  FBX écrit : {}   (échelle de racine {:.4f})".format(
        path, args.units_per_meter))

    if args.verify:
        table.verify_fbx(path, sum(1 for o in bpy.data.objects if o.type == "MESH"))

    table.report_hosts()


if __name__ == "__main__":
    main()
