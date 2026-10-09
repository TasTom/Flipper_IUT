# Industries — Bumper3 remonté

Réglage demandé le 9 octobre 2026, lié au fonctionnement des bumpers et à la circulation de la bille (GDD : bumpers / conception de la table).

## Placement appliqué

Dans le repère local du Playfield, Bumper3 passe de **(0,124 ; 0 ; −0,432)** à **(0,154 ; 0 ; −0,395)**. Il remonte donc de 0,5774 unité de scène et se décale de 0,4682 unité vers le centre. Le décalage latéral évite de remplacer le passage fermé contre Wall2 par un passage fermé contre Rubber10.

Les deux lampes natives `b3l1`, `b3l2` et le halo `Halo_Bumper3` suivent ce déplacement. Le manomètre est déjà enfant du bumper. La force (7), le scatter (0), le rayon, les murs, rampes et rubbers ne sont pas modifiés. Les retouches de placement passent par Undo ; l'outil n'enregistre pas la scène. Ctrl+S conserve le placement.

Le même emplacement devient la valeur initiale de `ArrangeIndustriesProduction`, qui préserve toujours les scènes déjà équipées du marqueur ProductionLayout.

[Avant](before.png) · [Après](after.png)

## Décor imprimé au sol

L'anneau gradué sous Bumper3 était resté à son ancienne position : il appartient à la texture du plateau, et ne suit donc pas le Transform du bumper. Le centre de ce seul anneau est corrigé dans [la source du dessin](../IndustriesLayout/source/generate_art.py), puis `Assets/Art/Industries/Production/Playfield.png` est régénéré et réimporté dans Unity. La capture Après inclut cette correction.

La génération d'origine reproduit exactement les deux PNG existants. Après correction, seuls 7 352 pixels changent, tous dans les emprises de l'ancien et du nouvel anneau ; les autres pixels et `Instructions.png` restent identiques. Le contrôle dans l'éditeur confirme le nouveau centre (0,154 ; −0,395), la texture active corrigée, et 1 039 transforms ainsi que 162 composants de collision inchangés. La scène était propre avant le réimport et le reste ; cette retouche de texture ne nécessite pas d'enregistrement de scène. Voir [le relevé](floor-art.txt) et [le script de réimport et capture](source/refresh-floor-art.cs).

## Vérification

- `recompile` puis `recompile_status` : compilation terminée, aucune erreur C#.
- Inspection des colliders VPE pendant Play : le centre physique suit la nouvelle position ; rayons natifs 40,5 pour le déclenchement et 20,25 pour le corps solide.
- Dégagement mesuré depuis la **zone active**, en tenant compte du contour interpolé des murs et d'une marge de tube de 5 pour Rubber10 : 74,675 unités contre Wall2, 65,274 contre Rubber10. Le diamètre de bille vaut 50 : les deux dépassent 1,3 diamètre. Voir [mesures](clearance.txt).
- Des sondes VPE reprennent des positions/vitesses enregistrées dans la boucle initiale et une approche depuis l'aire centrale. L'observation utilise `VisitBallStates` sur le thread de simulation ; les sondes sont ensuite détruites et les abonnements retirés. Voir [résultats](runtime.txt).
- Sur le placement final : les deux trajectoires du couloir n'émettent plus aucun impact Bumper3 en dix secondes, mais restent coincées. L'approche centrale déclenche un impact Bumper3 et poursuit sa course vers le bas de la table. Le contrôle final confirme le halo aligné et aucune sonde restante.
- L'erreur d'import existante `AutodeskInteractiveTransparent.shadergraph` reste présente ; aucune nouvelle exception de gameplay identifiée.

## Limite constatée

**Ce déplacement ne suffit pas à résoudre le blocage global.** Dans les trajectoires provenant du couloir, les impacts répétés de Bumper3 s'arrêtent, mais la bille finit presque immobile autour de (199,65 ; 762,2 ; 25) en coordonnées natives. L'inspection des colliders à cet endroit relève l'extrémité de Rubber1 et Wall2 : c'est un autre piège de géométrie à investiguer et ouvrir. Ces pièces ont été laissées intactes pendant la retouche demandée.

Les premiers essais qui évaluaient les positions rendues alors que l'éditeur n'avait pas le focus n'ont pas été utilisés comme validation finale. Les relevés finaux portent sur les états physiques natifs, avec `runInBackground` activé uniquement pour le test.

Les scripts de [placement avec Undo](source/move.cs) et de [reproduction VPE](source/probe.cs) sont conservés hors Assets pour audit et relance explicite avec `unity command eval_file`.
