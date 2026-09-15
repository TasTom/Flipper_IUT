"""Vérifie que l'append d'une pièce du dépôt est praticable.

`export_parts.py` ouvre chaque `.blend` en entier (`wm.open_mainfile`) puis n'en garde qu'un
objet : c'est simple, mais cela jette les matériaux, puisqu'on repart d'une scène vide ensuite.
Pour **assembler** des pièces sur la table, les matériaux comptent.

L'autre voie est `bpy.data.libraries.load(..., link=False)`, qui importe les objets d'un `.blend`
sans ouvrir le fichier. Reste à savoir si elle tient le coup sur les gros fichiers du dépôt
(`Guides.blend` fait 85 Mo, 142 objets) et si les matériaux suivent.

Ce script ne décide rien : il mesure. Il rapporte, pour quelques pièces témoins, le temps
d'append, le nombre d'objets et de matériaux ramenés, et les dimensions réelles de la pièce —
de quoi régler l'échelle de chaque pièce avant de l'assembler.

    blender --background --factory-startup --python probe_append.py
"""

import json
import os
import sys
import time

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.join(HERE, "..", "pinball-parts")
INVENTORY = os.path.join(HERE, "..", "pinball-parts-inventory.json")

# Les fichiers sont rangés par catégorie (`Bumpers/Bumpers.blend`), et l'inventaire connaît le
# chemin exact : on le lui demande plutôt que de le reconstruire à la main.
#
# Deux tailles très différentes, et des pièces à hiérarchie comme à maillage plat.
CASES = [
    "Flipper Williams 3\"",
    "Bumper Base - Williams/Bally",
    "Bumper Cap - Clear 3\"",
    "Bumper Ring",
    "Ball",
    "Plunger",
    "Drop Target",
    "Lane Guide - 2\"1/8 [1\"1/2 OC]",
]


def locate(inventory):
    """Nom de pièce -> chemin absolu du `.blend` qui la contient."""
    found = {}

    for _category, files in inventory["categories"].items():
        for _filename, contents in files.items():
            for part in contents.get("parts", []):
                found.setdefault(part["name"], contents.get("file"))

    return found


def collect(root):
    """La pièce et tous ses descendants — une pièce du dépôt est souvent une hiérarchie."""
    objects = [root]
    stack = list(root.children)

    while stack:
        obj = stack.pop()
        objects.append(obj)
        stack.extend(obj.children)

    return objects


def bounds(objects):
    """Boîte englobante en coordonnées monde, en mètres."""
    low = [float("inf")] * 3
    high = [float("-inf")] * 3

    for obj in objects:
        if obj.type != "MESH":
            continue

        matrix = obj.matrix_world

        for corner in obj.bound_box:
            point = matrix @ __import__("mathutils").Vector(corner)

            for axis in range(3):
                low[axis] = min(low[axis], point[axis])
                high[axis] = max(high[axis], point[axis])

    if low[0] == float("inf"):
        return None

    return low, high


def purge():
    """Vide la scène entre deux essais : sans cela les noms se suffixent en `.001`."""
    bpy.ops.wm.read_factory_settings(use_empty=True)


def main():
    with open(INVENTORY, "r", encoding="utf-8") as handle:
        inventory = json.load(handle)

    sources = locate(inventory)

    print("=" * 78)
    print("Append depuis {}/".format(os.path.abspath(REPO)))
    print("=" * 78)

    for part in CASES:
        path = sources.get(part)
        filename = os.path.basename(path) if path else "?"

        if not path or not os.path.isfile(path):
            print("  ABSENT  {!r}".format(part))
            continue

        purge()

        start = time.time()
        found = []

        with bpy.data.libraries.load(path, link=False) as (src, dst):
            names = [name for name in src.objects if name == part]
            dst.objects = names
            found = list(names)

        elapsed = time.time() - start

        if not found:
            print("  INTROUVABLE  {:<22} dans {}".format(repr(part), filename))
            continue

        root = bpy.data.objects.get(found[0])

        if root is None:
            print("  NON IMPORTÉ  {:<22} dans {}".format(repr(part), filename))
            continue

        hierarchy = collect(root)
        meshes = [obj for obj in hierarchy if obj.type == "MESH"]
        materials = set()

        for obj in meshes:
            for slot in obj.material_slots:
                if slot.material is not None:
                    materials.add(slot.material.name)

        triangles = 0

        for obj in meshes:
            obj.data.calc_loop_triangles()
            triangles += len(obj.data.loop_triangles)

        box = bounds(meshes)

        print()
        print("  {}  /  {}".format(filename, part))
        print("    append     : {:.2f} s   {} objets   {} maillages   {} tris".format(
            elapsed, len(hierarchy), len(meshes), triangles))
        print("    matériaux  : {} {}".format(
            len(materials), sorted(materials)[:6]))

        if box is None:
            print("    dimensions : aucune géométrie")
            continue

        low, high = box
        print("    dimensions : {:.4f} × {:.4f} × {:.4f} m   (x × y × z)".format(
            high[0] - low[0], high[1] - low[1], high[2] - low[2]))
        print("    origine    : x {:.4f}  y {:.4f}  z {:.4f}  (min de la boîte)".format(
            low[0], low[1], low[2]))

        for obj in hierarchy[:8]:
            parent = obj.parent.name if obj.parent else "—"
            print("      {:<38} {:<7} parent {}".format(
                obj.name[:38], obj.type, parent[:28]))

        if len(hierarchy) > 8:
            print("      … {} autres".format(len(hierarchy) - 8))

    print()
    print("=" * 78)

    return 0


if __name__ == "__main__":
    sys.exit(main())
