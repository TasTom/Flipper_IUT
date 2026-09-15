"""Inventaire des pièces du dépôt `vbousquet/pinball-parts`.

Le dépôt ne contient que des `.blend` non empaquetés (aucun FBX/GLB/OBJ) : il faut donc
ouvrir chaque fichier et choisir les pièces une par une avant d'exporter.

Ce script ne modifie rien : il ouvre chaque `.blend` en mode arrière-plan et relève ce qu'il
contient — pièces racines, nombre de maillages, polygones, dimensions réelles, matériaux et
textures. La sortie JSON sert à décider quoi exporter vers `Assets/Models/Parts/`.

    blender --background --factory-startup --python Tools/blender/inventory_parts.py -- \
        --repo Tools/pinball-parts \
        --out Tools/pinball-parts-inventory.json
"""

import argparse
import json
import os
import sys

import bpy
import mathutils

# Ancre d'échelle : la bille. Une bille de flipper réelle fait 27,0 mm (1"1/16) de diamètre ;
# la table Unity utilise un rayon de 0,225 (BuildPinballTable.BallRadius), soit 0,45 de
# diamètre. C'est le seul repère commun aux deux mondes : une pièce à l'échelle réelle doit
# donc être multipliée par ce facteur pour s'accorder à la bille de la table.
REAL_BALL_DIAMETER_M = 0.027
UNITY_BALL_DIAMETER = 0.45
UNITY_UNITS_PER_METER = UNITY_BALL_DIAMETER / REAL_BALL_DIAMETER_M  # ≈ 16,667

# Les fichiers de matériaux partagés ne contiennent pas de pièce : on les inventorie
# quand même (ils disent quels matériaux existent) mais ils ne sont pas exportables.
NOT_A_PART_FILE = {"Materials.blend"}


def parse_args():
    """Lit les arguments passés après `--`, que Blender laisse intacts."""
    argv = sys.argv
    argv = argv[argv.index("--") + 1:] if "--" in argv else []

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", default="Tools/pinball-parts",
                        help="racine du dépôt pinball-parts")
    parser.add_argument("--out", default="Tools/pinball-parts-inventory.json",
                        help="fichier JSON à écrire")
    return parser.parse_args(argv)


def find_blend_files(repo):
    """Tous les `.blend` du dépôt, triés, avec leur catégorie (nom du dossier)."""
    found = []

    for dirpath, _dirnames, filenames in os.walk(repo):
        # Le dépôt embarque des addons et des textures : seuls les .blend nous intéressent.
        for filename in filenames:
            if not filename.lower().endswith(".blend"):
                continue

            full = os.path.join(dirpath, filename)
            category = os.path.basename(dirpath)
            found.append((category, filename, full))

    return sorted(found, key=lambda item: (item[0].lower(), item[1].lower()))


def descendants(root):
    """Tous les descendants d'un objet, à n'importe quelle profondeur."""
    out = []
    stack = list(root.children)

    while stack:
        obj = stack.pop()
        out.append(obj)
        stack.extend(obj.children)

    return out


def part_objects(root):
    """L'objet racine **et** tous ses descendants.

    La racine compte. Ce dépôt contient massivement des pièces plates : un unique maillage
    sans enfant (« Flipper 2" - Left », 58 objets pour 58 racines dans `Flippers.blend`).
    Ne décrire que les descendants rendait ces pièces invisibles à leur propre inventaire —
    le rapport annonçait 0 pièce exportable sur 58, et l'export n'aurait rien produit.
    """
    return [root] + descendants(root)


def mesh_objects(objects):
    """Filtre une liste d'objets sur les maillages."""
    return [obj for obj in objects if obj.type == "MESH"]


def collections_of(obj):
    """Collections auxquelles l'objet appartient.

    Les fichiers du dépôt rangent leurs rebuts dans `Trash` et leur travail en cours dans
    `WIP` : sans cette information, l'export ne peut pas les distinguer des vraies pièces.
    """
    return sorted(collection.name for collection in obj.users_collection)


def world_bounds(objects):
    """Boîte englobante en coordonnées monde, ou None si aucun maillage.

    On passe par `bound_box` (les 8 coins locaux) plutôt que par `dimensions` : ce dernier
    ne tient pas compte de la hiérarchie, et une pièce est souvent un parent vide dont les
    enfants portent la géométrie.
    """
    low = [float("inf")] * 3
    high = [float("-inf")] * 3
    found = False

    for obj in objects:
        matrix = obj.matrix_world

        for corner in obj.bound_box:
            point = matrix @ mathutils.Vector(corner)

            for axis in range(3):
                low[axis] = min(low[axis], point[axis])
                high[axis] = max(high[axis], point[axis])

            found = True

    return (low, high) if found else None


def polygon_count(objects):
    """Nombre de polygones cumulés — le critère qui décide d'une décimation."""
    return sum(len(obj.data.polygons) for obj in objects)


def materials_of(objects):
    """Matériaux utilisés, et par eux les textures à copier à côté du mesh."""
    names = []
    textures = set()

    for obj in objects:
        for slot in obj.material_slots:
            material = slot.material

            if material is None:
                continue

            if material.name not in names:
                names.append(material.name)

            if not material.use_nodes or material.node_tree is None:
                continue

            for node in material.node_tree.nodes:
                if node.type != "TEX_IMAGE" or node.image is None:
                    continue

                # `filepath` est le chemin tel qu'enregistré, souvent relatif au .blend.
                textures.add(node.image.filepath or node.image.name)

    return names, sorted(textures)


def custom_properties(obj):
    """Propriétés personnalisées de l'objet, hors métadonnées d'interface Blender."""
    out = {}

    for key in obj.keys():
        if key.startswith("_") or key in {"cycles", "asset_data"}:
            continue

        value = obj[key]

        # Les valeurs non scalaires ne s'écrivent pas en JSON tel quel.
        if isinstance(value, (str, int, float, bool)):
            out[key] = value
        else:
            out[key] = repr(value)

    return out


def describe_part(root):
    """Fiche d'une pièce : l'objet racine et tout ce qu'il porte."""
    contents = mesh_objects(part_objects(root))
    bounds = world_bounds(contents)
    materials, textures = materials_of(contents)

    part = {
        "name": root.name,
        "root_type": root.type,
        "collections": collections_of(root),
        "mesh_count": len(contents),
        "polygons": polygon_count(contents),
        "materials": materials,
        "textures": textures,
        "children": [child.name for child in root.children],
    }

    if bounds is None:
        # Un objet sans maillage : un simple repère, rien à exporter.
        part["size_m"] = None
        part["size_unity"] = None
        part["origin_m"] = None
        return part

    low, high = bounds
    size = [high[axis] - low[axis] for axis in range(3)]

    part["size_m"] = [round(value, 5) for value in size]
    part["size_unity"] = [round(value * UNITY_UNITS_PER_METER, 4) for value in size]
    part["origin_m"] = [round((low[axis] + high[axis]) / 2.0, 5) for axis in range(3)]
    part["min_m"] = [round(value, 5) for value in low]

    return part


def inventory_file(path):
    """Ouvre un `.blend` et inventorie ses pièces racines."""
    bpy.ops.wm.open_mainfile(filepath=path)
    scene = bpy.context.scene

    roots = [obj for obj in bpy.data.objects if obj.parent is None]

    return {
        "unit_scale_length": scene.unit_settings.scale_length,
        "collections": [collection.name for collection in bpy.data.collections],
        "object_count": len(bpy.data.objects),
        "parts": [describe_part(root) for root in roots],
    }


def exporter_names():
    """Opérateurs d'export disponibles dans ce Blender.

    Blender 5.x a remanié ses exportateurs : plutôt que de supposer que `export_scene.fbx`
    existe encore, on le demande à l'installation qui tourne.
    """
    names = []

    for name in dir(bpy.ops.export_scene):
        if name.startswith("_"):
            continue

        names.append(name)

    return sorted(names)


def main():
    args = parse_args()
    repo = os.path.abspath(args.repo)

    if not os.path.isdir(repo):
        raise SystemExit("Dépôt introuvable : {}".format(repo))

    files = find_blend_files(repo)
    report = {
        "repo": repo,
        "units_per_meter": round(UNITY_UNITS_PER_METER, 6),
        "real_ball_diameter_m": REAL_BALL_DIAMETER_M,
        "unity_ball_diameter": UNITY_BALL_DIAMETER,
        "export_scene_operators": exporter_names(),
        "categories": {},
    }

    print("=" * 78)
    print("Inventaire de {} ({} fichiers .blend)".format(repo, len(files)))
    print("Échelle retenue : {} unités Unity par mètre".format(
        round(UNITY_UNITS_PER_METER, 4)))
    print("Exportateurs scene disponibles : {}".format(
        ", ".join(report["export_scene_operators"]) or "(aucun)"))
    print("=" * 78)

    for category, filename, full in files:
        label = "{}/{}".format(category, filename)

        try:
            contents = inventory_file(full)
        except Exception as error:  # noqa: BLE001 — un fichier illisible ne doit pas tout arrêter
            print("\n[{}] ÉCHEC : {}".format(label, error))
            report["categories"].setdefault(category, {})[filename] = {
                "file": full,
                "error": str(error),
            }
            continue

        is_part_file = filename not in NOT_A_PART_FILE
        contents["file"] = full
        contents["is_part_file"] = is_part_file

        parts = contents["parts"]
        exportable = [part for part in parts if part.get("size_m")]

        print("\n[{}] {} objets, {} pièce(s) racine(s), {} exportable(s)".format(
            label, contents["object_count"], len(parts), len(exportable)))

        for part in exportable:
            size = part["size_m"]
            unity = part["size_unity"]
            print("    {:<28} {:>5} mesh {:>8} tris  {:>7.3f}×{:>6.3f}×{:>6.3f} m"
                  "  → {:>7.2f}×{:>6.2f}×{:>6.2f} u".format(
                      part["name"][:28], part["mesh_count"], part["polygons"],
                      size[0], size[1], size[2],
                      unity[0], unity[1], unity[2]))

        report["categories"].setdefault(category, {})[filename] = contents

    out_path = os.path.abspath(args.out)
    os.makedirs(os.path.dirname(out_path), exist_ok=True)

    with open(out_path, "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2, ensure_ascii=False)

    print("\n" + "=" * 78)
    print("Rapport écrit : {}".format(out_path))


if __name__ == "__main__":
    main()
