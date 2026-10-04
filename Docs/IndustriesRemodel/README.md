# Industries — remodelage de l'atelier, 4 octobre 2026

Branche `feature/industries-remodel`. Le GDD §Univers et direction artistique prévoit une table identifiable, stylisée entre low-poly et semi-réaliste. La demande complémentaire de l'utilisateur définit Industries comme une seconde table industrielle vosgienne, distincte de la Full Example Table de VPE.

Le nouvel habillage comprend huit carters ajustés aux contours mesurés dans Unity, avec une épaisseur de 3 mm, des chants chanfreinés, bordures cuivre et fixations hexagonales. Le portique d'atelier donne une silhouette industrielle à la zone haute ; ses supports sont sur les montants extérieurs. Les cinq bumpers portent des manomètres fixés à leurs chapeaux natifs. Les flippers utilisent le même émail ivoire et le même caoutchouc sombre. Les rampes, plots, chants et cache-lanceur suivent la palette acier/pétrole/cuivre. Les motifs Williams, plastiques disparates, chants bois et étoiles des outlanes du template sont remplacés visuellement.

Le plateau et les carters partagent une nouvelle signalétique : Industries / Atelier des Vosges, salle des machines, transmissions et chaîne de production. Les numéros des six cibles restent leurs objets et voyants VPE corrigés précédemment. Les décors patrimoniaux détaillés, cartes de commandes, caméra, fronton, règles et sons existants sont conservés.

- [Avant, vue de la table](before.png)
- [Après, même caméra de comparaison](after.png)
- [Cadrage de borne](game.png)
- [Contrôles d'édition](editor.txt) : carters, cinq manomètres animés avec leurs hôtes, matériaux persistants, absence de nouveaux colliders, scripts et idempotence.
- [16 contrôles physiques](runtime.txt) : commandes, lancement, pause, six cibles et bonus, scoop, trois drains et nouvelle partie.
- [Undo et Redo](undo.txt)
- [Comparaison des données VPE](native.txt)

Les 29 012 triangles du FBX comprennent les huit carters et le portique. Les petites pièces sont regroupées par matériau à l'intérieur de chaque carter. Les textures de plateau et de carters sont produites à 2048 × 4096, le cadran à 512 × 512. Aucune ombre, lumière ou série de chiffres n'est peinte dans ces textures. La normal map de microtexture existante reste distincte. Aucun nouveau package ni éclairage temps réel n'est ajouté ; aucun budget de performance sur borne n'est revendiqué.

## Reproduire et ajuster

Les sources sont [generate_art.py](source/generate_art.py), [model_visuals.py](source/model_visuals.py) et [panel-guides.json](source/panel-guides.json), les contours exportés du plateau. Les conversions Blender/Unity sont cuites dans la géométrie avant export, pour éviter les écarts de position des pièces à hiérarchie.

```powershell
python Docs/IndustriesRemodel/source/generate_art.py
& 'C:/Program Files/Blender Foundation/Blender 5.0/blender.exe' --background --factory-startup --python Docs/IndustriesRemodel/source/model_visuals.py
```

`Flipper > Industries > Remodeler l'habillage atelier (Undo)` applique la migration explicite dans Industries. Elle crée `ExampleTable/Playfield/WorkshopRemodel` et les cinq manomètres sous les chapeaux des bumpers. Les anciens carters sont masqués par renderer, leurs GameObjects et composants natifs restent actifs. Une relance conserve l'habillage et les ajustements manuels existants. Les matériaux propres à cette scène sont sous `Assets/Generated/Industries/Remodel`, les sources originales restent disponibles.

Les références de méthode sont les [tutoriels VPE](https://docs.visualpinball.org/creators-guide/tutorials/index.html), l'[import du plateau visuel dans Unity](https://docs.visualpinball.org/creators-guide/tutorials/realistic-playfield/4-import-into-unity.html), les [plastiques](https://docs.visualpinball.org/creators-guide/tutorials/realistic-plastics/index.html) et le [styleguide d'assets](https://docs.visualpinball.org/creators-guide/editor/asset-library-styleguide.html). Le [manuel](https://docs.visualpinball.org/creators-guide/manual/manual.html) et le [lancement VPE](https://docs.visualpinball.org/creators-guide/setup/running-vpe.html) fournis par l'utilisateur complètent ces références. Les matériaux sont adaptés à l'URP déjà utilisé dans le projet ; aucune migration vers HDRP n'est nécessaire.

La topologie jouable, les colliders VPE, les contacts, bobines et règles restent ceux d'Industries. Le remodelage ne crée pas une nouvelle disposition mécanique. L'erreur d'import `AutodeskInteractiveTransparent.shadergraph` déjà connue a remis Unity en pause au début du test ; le test a été repris et les 16 contrôles ont réussi. L'habillage reste appliqué dans la scène ouverte, **sans sauvegarde automatique**, conformément à AGENTS.md : Ctrl+S dans Unity pour le conserver.

Tous les nouveaux graphismes et carters sont créés pour ce projet. Les mécanismes VPE et décors patrimoniaux existants conservent leurs attributions dans [Industries.md](../Industries.md).
