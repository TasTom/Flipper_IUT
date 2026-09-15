"""Sonde : pourquoi 67 pièces du dépôt sont-elles hors du view layer ?

`export_parts.py` écarte un objet absent du view layer courant — un objet hors vue refuse
`select_set`, ce qui interrompait tout l'export. Mais 67 pièces du dépôt tombent dans ce cas
(beaucoup de `Screws`, tout `Shared`), et « hors vue » n'est pas « rebut » : le filtre
`Trash` / `WIP` existait déjà pour ça.

Cette sonde ouvre les fichiers concernés et relève, collection par collection :
  - le nom de la collection et son drapeau `exclude` dans le view layer,
  - combien d'objets elle contient,
  - si ses objets sont atteignables depuis la scène.

    blender --background --factory-startup --python Tools/blender/probe_viewlayer.py
"""

import os
import sys

import bpy

REPO = os.path.join("Tools", "pinball-parts")
CIBLES = ["Screws.blend", "Shared.blend", "Posts.blend"]


def walk(layer_collection, profondeur=0):
    """Parcourt l'arbre des collections du view layer, en signalant les exclusions."""
    lignes = []
    pad = "  " * profondeur
    nom = layer_collection.name

    lignes.append("{}{:<34} exclude={:<5}  objets={}".format(
        pad, nom, str(layer_collection.exclude), len(layer_collection.collection.objects)))

    for enfant in layer_collection.children:
        lignes.extend(walk(enfant, profondeur + 1))

    return lignes


def main():
    for nom in CIBLES:
        chemin = None

        for racine, _dirs, fichiers in os.walk(REPO):
            if nom in fichiers:
                chemin = os.path.join(racine, nom)
                break

        if chemin is None:
            print("{} : introuvable".format(nom))
            continue

        bpy.ops.wm.open_mainfile(filepath=chemin)

        print("=" * 78)
        print(nom)
        print("=" * 78)

        for ligne in walk(bpy.context.view_layer.layer_collection):
            print("  " + ligne)

        # Ce que la scène peut réellement sélectionner, en comptant.
        vus = [o for o in bpy.data.objects]
        dans_vue = [o for o in vus if o.name in bpy.context.view_layer.objects]

        print("  -> {} objets au total, {} dans le view layer".format(len(vus), len(dans_vue)))
        print()


if __name__ == "__main__":
    main()
