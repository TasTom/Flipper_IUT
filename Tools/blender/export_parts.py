"""Exporte les pièces de `vbousquet/pinball-parts` en FBX, une pièce par fichier.

Le dépôt ne contient que des `.blend` : ce script en sort un FBX par pièce, à l'échelle de la
table Unity, dans `Assets/Models/Parts/<Catégorie>/`.

**Mise à l'échelle.** Les pièces du dépôt sont à l'échelle réelle (mètres) ; la table Unity
utilise une bille de 0,45 unité pour 27 mm réels, soit ≈ 16,667 unités par mètre (même ancre
que `inventory_parts.py`).

Une hiérarchie se met à l'échelle en **une seule écriture** : celle de l'échelle de la racine.
Les descendants en héritent — leur taille comme leur décalage par rapport à la racine sont
multipliés par `k`.

Multiplier **en plus** la `location` des descendants est la faute à ne pas commettre : leur
décalage subirait `k` deux fois — une fois par héritage, une fois à la main — soit `k²`. Le
piège est sournois parce que la plupart des pièces du dépôt sont **plates** (un unique maillage
sans enfant ; `Flippers.blend` compte 58 objets pour 58 racines) : sur celles-là, les deux
méthodes donnent le même résultat et l'erreur reste invisible. Ce sont les pièces à hiérarchie
qui tranchent — mesuré sur `Plunger`, annoncé 0,39×2,76×0,39 et réimporté 159×251×3,7.

`--verify` réimporte chaque FBX écrit dans un fichier vide et mesure sa boîte englobante :
l'échelle est constatée, pas supposée.

    blender --background --factory-startup --python Tools/blender/export_parts.py -- \
        --inventory Tools/pinball-parts-inventory.json \
        --out Assets/Models/Parts \
        --only Flipper,Bumpers \
        --verify
"""

import argparse
import json
import mathutils
import os
import re
import sys

import bpy

# Même ancre que l'inventaire : voir inventory_parts.py pour le raisonnement.
REAL_BALL_DIAMETER_M = 0.027
UNITY_BALL_DIAMETER = 0.45
UNITY_UNITS_PER_METER = UNITY_BALL_DIAMETER / REAL_BALL_DIAMETER_M  # ≈ 16,667

# Paramètres d'export FBX visés. Ils sont filtrés à l'appel sur ce que l'installation connaît
# réellement : les exportateurs de Blender ont bougé entre 4.x et 5.x, et passer un paramètre
# disparu fait échouer l'appel.
#
# **Les textures ne sont PAS embarquées.** Mesuré le 2026-09-15 : `Flange_Bolt_10-32.fbx`
# pesait **15,61 Mo** avec textures contre **0,04 Mo** pour `Plastic_with_decal.fbx` sans —
# soit 400 fois plus pour le même genre de pièce. Les 723 FBX totalisaient **1,25 Go** dans
# `Assets/Models/Parts/`, dont la quasi-totalité en textures recopiées d'un fichier à l'autre.
#
# Ce que cela coûte : rien d'utile. Les matériaux du dépôt survivent par leur NOM dans Unity
# mais perdent leur couleur, et le projet les réassigne de toute façon depuis son propre set
# URP (`BumperRed_Mat`, `Metal_Mat`, `FlipperOrange_Mat`…). La table, elle, n'est même pas
# construite depuis ces FBX : `assemble_parts.py` lit les `.blend` du dépôt directement. Sur les
# 723 pièces, trois seulement sont instanciées dans la scène (bat de flipper, plastique de
# slingshot, lanceur).
#
# `--embed-textures` rétablit l'ancien comportement si un jour une pièce en a besoin.
FBX_SETTINGS = {
    "use_selection": True,
    "global_scale": 1.0,        # la mise à l'échelle est faite à la main, cf. en-tête
    "apply_unit_scale": False,  # idem : on ne veut pas que Blender en rajoute une
    "axis_forward": "-Z",       # convention Unity (gauche, Y vers le haut)
    "axis_up": "Y",
    "use_mesh_modifiers": True,
    "mesh_smooth_type": "FACE",
    "use_tspace": True,
    "path_mode": "AUTO",
    "embed_textures": False,
    "batch_mode": "OFF",
}


# Collections qui ne contiennent pas des pièces mais des rebuts ou du travail en cours.
# Le dépôt marque lui-même ses objets inutilisables : `Trash` (pièces abandonnées) et `WIP`
# (gabarits de référence — des repères de 5 et 15 mm, pas des pièces de flipper). Sans ce
# filtre, un export « tout » les déverserait dans le projet à côté des vraies pièces.
DEFAULT_SKIP_COLLECTIONS = "Trash,WIP"


def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1:] if "--" in argv else []

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", default="Tools/pinball-parts-inventory.json",
                        help="JSON produit par inventory_parts.py")
    parser.add_argument("--out", default="Assets/Models/Parts",
                        help="dossier de sortie des FBX")
    parser.add_argument("--only", default="",
                        help="catégories à exporter, séparées par des virgules (défaut : toutes)")
    parser.add_argument("--parts", default="",
                        help="noms de pièces à exporter, séparés par des virgules (défaut : toutes)")
    parser.add_argument("--skip-collections", default=DEFAULT_SKIP_COLLECTIONS,
                        help="collections à ne pas exporter (défaut : {})".format(
                            DEFAULT_SKIP_COLLECTIONS))
    parser.add_argument("--limit", type=int, default=0,
                        help="n'exporte que les N premières pièces (0 : pas de limite)")
    parser.add_argument("--dry-run", action="store_true",
                        help="liste ce qui serait exporté, sans rien écrire")
    parser.add_argument("--verify", action="store_true",
                        help="réimporte chaque FBX écrit et mesure sa taille")
    parser.add_argument("--units-per-meter", type=float, default=UNITY_UNITS_PER_METER,
                        help="facteur d'échelle (défaut : ancre de la bille)")
    parser.add_argument("--embed-textures", action="store_true",
                        help="embarque les textures dans les FBX (les rend ~400 fois plus gros ; "
                             "sans intérêt ici, voir FBX_SETTINGS)")
    return parser.parse_args(argv)


def sanitize(name):
    """Nom de fichier sûr, sur le modèle de `clean_filename` de l'addon du dépôt."""
    cleaned = re.sub(r"[^A-Za-z0-9 _.\-()]", "", name).strip()
    cleaned = re.sub(r"\s+", "_", cleaned)
    return cleaned[:120] or "part"


def unique_path(directory, stem, used):
    """Un chemin de fichier que personne n'a déjà pris dans cette passe.

    `sanitize` écarte ce que le système de fichiers refuse (`#`, espaces en trop, guillemets),
    si bien que deux pièces distinctes peuvent viser le MÊME fichier : mesuré, 723 pièces
    exportées pour 720 fichiers écrits — trois paires se recouvraient, la seconde écrasant la
    première sans un mot. Un export qui perd des pièces en silence ne vaut pas mieux qu'un
    export qui échoue.

    Le suffixe est donc ajouté par l'exporteur, pas deviné : `_2`, `_3`…
    """
    chemin = os.path.join(directory, stem + ".fbx")
    n = 2

    while chemin.lower() in used:
        chemin = os.path.join(directory, "{}_{}.fbx".format(stem, n))
        n += 1

    used.add(chemin.lower())

    return chemin


def split_filter(value):
    """Découpe un filtre « a, b, c » en ensemble insensible à la casse."""
    return {item.strip().lower() for item in value.split(",") if item.strip()}


def load_inventory(path):
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def iter_parts(inventory, categories, part_names, skip_collections, skipped):
    """Parcourt les pièces exportables : (catégorie, fichier .blend, fiche de la pièce).

    `skipped` recueille les noms écartés par le filtre de collections : une pièce qui
    disparaît sans trace dans un rapport n'est pas vérifiable.
    """
    for category, files in sorted(inventory["categories"].items()):
        if categories and category.lower() not in categories:
            continue

        for _filename, contents in sorted(files.items()):
            if contents.get("error") or not contents.get("is_part_file"):
                continue

            for part in contents.get("parts", []):
                # Une pièce sans maillage n'a rien à exporter : c'est un repère.
                if not part.get("size_m"):
                    continue

                if part_names and part["name"].lower() not in part_names:
                    continue

                # Rangée dans `Trash` ou `WIP`, ce n'est pas une pièce du dépôt.
                if skip_collections.intersection(
                        name.lower() for name in part.get("collections", [])):
                    skipped.append("{} [{}]".format(
                        part["name"], ", ".join(part.get("collections", []))))
                    continue

                yield category, contents["file"], part


def deselect_all():
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)


def collect_hierarchy(root):
    """La pièce racine et tous ses descendants."""
    objects = [root]
    stack = list(root.children)

    while stack:
        obj = stack.pop()
        objects.append(obj)
        stack.extend(obj.children)

    return objects


def in_view_layer(obj):
    """L'objet est-il sélectionnable dans la vue courante ?"""
    try:
        return obj.name in bpy.context.view_layer.objects
    except AttributeError:  # vue non initialisée : on ne filtre pas
        return True


def ensure_visible(obj):
    """Rend l'objet sélectionnable, même rangé dans une collection exclue du view layer.

    Un `.blend` du dépôt range certains objets hors de la vue : `Screws.blend` a une collection
    `Legacy Parts` (48 objets) dont le drapeau `exclude` est coché. Un objet hors vue refuse
    `select_set`, et l'exporter demande donc de lever l'exclusion de sa branche.

    C'est fait **en mémoire seulement** — le `.blend` n'est jamais enregistré. Le dépôt garde
    donc son classement, et l'export récupère les pièces.

    Renvoie True si l'objet est sélectionnable après l'opération.
    """
    if in_view_layer(obj):
        return True

    def lever(layer_collection):
        """Ouvre la branche qui porte l'objet ; renvoie True si c'est elle."""
        if obj.name in layer_collection.collection.objects:
            layer_collection.exclude = False
            layer_collection.hide_viewport = False
            layer_collection.collection.hide_viewport = False
            return True

        for enfant in layer_collection.children:
            if lever(enfant):
                layer_collection.exclude = False
                layer_collection.hide_viewport = False
                return True

        return False

    try:
        lever(bpy.context.view_layer.layer_collection)
        bpy.context.view_layer.update()
    except (AttributeError, ReferenceError):
        return False

    return in_view_layer(obj)


def scale_hierarchy(root, factor):
    """Met la pièce à l'échelle `factor`, autour de l'origine de sa racine.

    Une seule écriture suffit, et c'est tout l'intérêt : l'échelle de la racine. Les
    descendants en héritent, taille **et** décalage. Toucher à leur `location` en plus
    appliquerait `factor` deux fois — voir l'en-tête.
    """
    root.scale = root.scale * factor


def export_fbx(operator, fbx_path, settings):
    """Appelle l'exportateur en ne passant que les paramètres qu'il accepte."""
    available = set(operator.get_rna_type().properties.keys())
    accepted = {key: value for key, value in settings.items() if key in available}
    ignored = sorted(set(settings) - set(accepted))

    operator(filepath=fbx_path, **accepted)

    return ignored


def measure_current_scene():
    """Boîte englobante de tous les maillages de la scène courante, en unités locales."""
    low = [float("inf")] * 3
    high = [float("-inf")] * 3
    found = False

    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue

        matrix = obj.matrix_world

        for corner in obj.bound_box:
            point = matrix @ mathutils.Vector(corner)

            for axis in range(3):
                low[axis] = min(low[axis], point[axis])
                high[axis] = max(high[axis], point[axis])

            found = True

    if not found:
        return None

    return [round(high[axis] - low[axis], 5) for axis in range(3)]


def verify_fbx(fbx_path):
    """Réimporte un FBX dans un fichier vide et mesure ce qui est réellement écrit."""
    bpy.ops.wm.read_factory_settings(use_empty=True)

    try:
        bpy.ops.import_scene.fbx(filepath=fbx_path)
    except Exception as error:  # noqa: BLE001 — on rapporte, on n'interrompt pas
        return None, str(error)

    return measure_current_scene(), None


def main():
    args = parse_args()

    if args.embed_textures:
        FBX_SETTINGS["embed_textures"] = True
        FBX_SETTINGS["path_mode"] = "COPY"

    inventory = load_inventory(args.inventory)

    categories = split_filter(args.only)
    part_names = split_filter(args.parts)
    factor = args.units_per_meter

    skipped = []
    found = list(iter_parts(inventory, categories, part_names,
                            split_filter(args.skip_collections), skipped))

    # Un nom demandé qui ne correspond à rien disparaît sans bruit : le rapport annoncerait
    # « 17 pièces » sans dire que la 18e n'existe pas sous ce nom. On les nomme — c'est ainsi
    # qu'un nom recopié depuis un affichage tronqué se repère.
    unmatched = sorted(part_names - {part["name"].lower() for _c, _s, part in found})

    selection = found[:args.limit] if args.limit > 0 else found

    print("=" * 78)
    print("Export FBX — {} pièce(s) retenue(s)".format(len(selection)))
    print("Échelle : {:.6f} unités Unity par mètre".format(factor))
    print("Sortie  : {}".format(os.path.abspath(args.out)))
    print("Écartées: {} pièce(s) dans [{}]".format(
        len(skipped), args.skip_collections))

    for name in unmatched:
        print("⚠ AUCUNE CORRESPONDANCE : {!r}".format(name))

    print("=" * 78)

    if not selection:
        print("Rien à exporter : vérifier --only / --parts.")
        return

    if args.dry_run:
        for category, source, part in selection:
            print("  {:<22} {:<30} {:>8} tris".format(
                category, part["name"][:30], part["polygons"]))
        print("\n(dry-run : rien n'a été écrit)")
        return

    # Les pièces d'un même .blend sont exportées à la suite, sans rouvrir le fichier.
    current_file = None
    written = []
    used_paths = set()
    ignored_settings = None

    try:
        for category, source, part in selection:
            if source != current_file:
                bpy.ops.wm.open_mainfile(filepath=source)
                current_file = source

            root = bpy.data.objects.get(part["name"])

            if root is None:
                print("  IGNORÉ  {:<30} (objet absent après ouverture)".format(part["name"]))
                continue

            deselect_all()

            complet = collect_hierarchy(root)
            hierarchy = [obj for obj in complet if ensure_visible(obj)]
            ecartes = len(complet) - len(hierarchy)

            for obj in hierarchy:
                obj.hide_set(False)
                obj.hide_viewport = False
                obj.select_set(True)

            # La racine elle-même peut rester hors vue : l'export n'aurait alors rien à prendre,
            # et le rapport doit le dire plutôt que d'écrire un FBX vide.
            if root not in hierarchy:
                print("  IGNORÉ  {:<30} (hors view layer : impossible à rendre sélectionnable)".format(
                    part["name"][:30]))
                continue

            bpy.context.view_layer.objects.active = root

            if ecartes:
                print("          {:<30} ({} objet(s) hors vue non exportables)".format(
                    "", ecartes))

            scale_hierarchy(root, factor)

            out_dir = os.path.join(args.out, sanitize(category))
            os.makedirs(out_dir, exist_ok=True)
            fbx_path = unique_path(out_dir, sanitize(part["name"]), used_paths)

            ignored_settings = export_fbx(bpy.ops.export_scene.fbx, fbx_path, FBX_SETTINGS)

            written.append((category, part, fbx_path))

            print("  OK      {:<22} {:<30} → {}".format(
                category, part["name"][:30], os.path.relpath(fbx_path, args.out)))

    except AttributeError:
        raise SystemExit(
            "Cette installation de Blender n'expose pas bpy.ops.export_scene.fbx. "
            "Exportateurs disponibles : {}".format(
                ", ".join(inventory.get("export_scene_operators", []))))

    print("\n{} FBX écrit(s).".format(len(written)))

    if skipped:
        print("{} pièce(s) écartée(s) par le filtre de collections :".format(len(skipped)))

        for name in skipped:
            print("  ÉCARTÉE  {}".format(name))

    if ignored_settings:
        print("Paramètres ignorés (inconnus de ce Blender) : {}".format(
            ", ".join(ignored_settings)))

    if not args.verify:
        return

    print("\n" + "=" * 78)
    print("Vérification par réimport")
    print("=" * 78)

    mismatches = 0

    for category, part, fbx_path in written:
        measured, error = verify_fbx(fbx_path)

        if error:
            print("  ÉCHEC   {:<30} {}".format(part["name"][:30], error))
            mismatches += 1
            continue

        expected = part["size_unity"]
        # La tolérance absorbe les arrondis de l'inventaire (5 décimales) et du FBX.
        worst = max(abs(measured[axis] - expected[axis]) for axis in range(3))
        verdict = "OK " if worst < 0.01 else "ÉCART"

        if worst >= 0.01:
            mismatches += 1

        print("  {} {:<28} attendu {:>7.2f}×{:>6.2f}×{:>6.2f}  mesuré {:>7.2f}×{:>6.2f}×{:>6.2f}".format(
            verdict, part["name"][:28],
            expected[0], expected[1], expected[2],
            measured[0], measured[1], measured[2]))

    if mismatches:
        print("\n{} pièce(s) en écart : l'échelle est à revoir avant import.".format(mismatches))
    else:
        print("\nToutes les pièces sont à l'échelle attendue.")


if __name__ == "__main__":
    main()
