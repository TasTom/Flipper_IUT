# Industries — deux lignes de production

**Mise à jour du 8 octobre :** la rampe bois a été retirée et Ramp1 est commune aux deux matières. Voir [la correction actuelle](../IndustriesRamp1/README.md). Le document ci-dessous décrit le layout précédent.

Demande : changer les tirs, les positions et les mécaniques de la seconde table, après la refonte de son habillage.

Le layout est appliqué dans la scène Industries ouverte. La scène reste modifiée et **n'est jamais enregistrée par l'outil**. Ctrl+S conserve le résultat ; les migrations disposent d'Undo. Sur une scène qui n'a pas reçu ce layout : menu `Flipper > Industries > Installer le layout et les règles de production`. Une seconde exécution laisse les placements manuels intacts.

## Disposition

- Les trois drop targets bois sont au centre, en biais face au flipper droit.
- Les trois cibles fixes textile occupent le côté droit. Le gabarit commun des six cibles et les chiffres centrés devant chaque face sont conservés.
- Les cinq bumpers sont répartis en un groupe à droite et un bumper à gauche, avec leurs voyants et cadrans déplacés ensemble.
- Une nouvelle rampe VPE part du centre gauche et rejoint le retour métallique gauche au-dessus des guides. La rampe textile droite monte progressivement, avec des guides physiques adaptés.
- Le scoop passe à gauche, avec sa coupelle et une ouverture réelle dans le plateau imprimé et le plateau VPE manuel.

![Layout précédent](before.png)
![Layout de production](after.png)

## Règles

Chaque matière est indépendante. Les trois cibles de sa banque chargent une commande. Un passage entrée → sortie sur sa rampe, par **la même bille**, transforme cette commande. Le scoop livre les produits transformés. Il ne livre pas une matière seulement chargée. Une matière en cours de chargement reste disponible lorsqu'on livre l'autre.

Valeurs initiales réglables dans `IndustriesConfig` : 500 par cible, 1 000 par transformation, 4 000 par produit livré, 2 000 supplémentaires pour livrer bois et textile ensemble. Le scoop garde ses 1 000 points de contact. Six cibles seules ne donnent plus automatiquement le bonus de 5 000. Le parcours de rampe expire après 8 secondes de temps de jeu ; la pause ne consomme pas ce délai. Une nouvelle bille réinitialise les commandes. Les commandes clavier/borne, le score partagé et les trois billes restent pris en charge.

Le HUD indique `0/3`, `RAMPE` ou `LIVRER` pour chaque matière. Les inserts devant les cibles et les instructions de l'apron accompagnent cette boucle.

## Sources de conception

Le [GDD](../../GameDesignDocument.docx), sections **Cibles fixes**, **Rampes**, **Score, combos et bonus**, établit les séries de cibles, les deux parcours principaux et les séquences récompensées. La boucle bois/textile/livraison constitue une déclinaison industrielle demandée pour cette seconde table ; elle ne remplace pas les cinq groupes de missions IUT du GDD.

La mécanique utilise les composants Ramp, Trigger, Target et Kicker de VPE. Références : [manuel VPE](https://docs.visualpinball.org/creators-guide/manual/manual.html), [tutoriels](https://docs.visualpinball.org/creators-guide/tutorials/index.html). Les volumes de validation sont des colliders VPE en hauteur, pas des triggers PhysX au sol.

## Validation

**24 contrôles de règles/layout, 19 contrôles en Play et 3 contrôles Undo/Redo/idempotence passent.** `editor.txt`, `runtime.txt` et `undo.txt` consignent les résultats. Les essais en Play emploient des billes natives : cibles, deux parcours complets depuis leurs entrées, double livraison de 11 000 points avec le contact du scoop, réarmement des cibles, flippers, lanceur, pause, drains et nouvelle partie. Ces tirs contrôlés vérifient l'accès et le fonctionnement des mécanismes ; l'équilibrage en partie prolongée reste à ajuster dans l'Inspector.

L'éditeur présente déjà une erreur d'import du shader URP `AutodeskInteractiveTransparent.shadergraph` au passage en Play. Elle déclenche Error Pause ; les essais sont repris après cette pause. Aucun enregistrement automatique, remplacement des scènes utilisateur ou changement du shader de package n'est effectué.

L'art original du plateau et de l'apron est reproductible avec `python Docs/IndustriesLayout/source/generate_art.py`. Les scripts de migration gardent les géométries et paramètres natifs sérialisés dans la scène.
