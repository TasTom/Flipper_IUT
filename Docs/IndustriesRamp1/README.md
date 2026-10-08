# Industries — correction de Ramp1 (8 octobre 2026)

**Version suivante :** entrée entre Primitive28/29, rampe abaissée et virages couverts. Voir [la correction actuelle](../IndustriesCoveredRamp/README.md). Les mesures ci-dessous concernent la première correction.

La rampe `ProductionWoodRamp` et ses deux capteurs sont retirés de la scène Industries ouverte. `Ramp1` devient le parcours de transformation commun au bois et au textile : trois cibles chargent chaque matière, un passage complet transforme les commandes chargées, puis le scoop les livre. Le bonus double reste disponible. Cette adaptation relève des sections Cibles fixes, Rampes et Score/combos du GDD.

La scène reste modifiée, sans enregistrement automatique : **Ctrl+S conserve le résultat**. Sur une ancienne scène : `Flipper > Industries > Retirer la rampe bois et corriger Ramp1`. La correction utilise Undo. Sa seconde exécution conserve les ajustements manuels. Le menu d'installation du layout ne crée plus la rampe bois.

## Géométrie

- Entrée recentrée à `(0,360 ; 0 ; -0,580)` dans le repère natif du plateau ; les approches des six cibles restent accessibles.
- Montée progressive, courbe dégagée du poteau `Primitive28` et du portique, puis descente vers le retour métallique gauche. Sommet mesuré : 84,60 mm.
- Largeur de 36 mm à l'entrée, raccord de 31,31 mm correspondant aux guides du retour existant. L'extrémité s'arrête environ 1,7 mm avant le maillage du retour : la bille franchit ce raccord sans deux sols superposés.
- Parois physiques et visibles de 55 mm, plastique transparent et fins bords en cuivre. Les guides retiennent les tirs rapides ; élasticité 0,05 et friction 0,08.
- Traverse du portique rehaussée de 45 mm, avec allongement des parties hautes et déplacement de sa plaque. Les nouveaux maillages sont propres à Industries ; le FBX source n'est pas modifié. Le décor reste sans collider.
- Deux capteurs VPE, sans doublons bois/textile. La sortie couvre les 55 mm du canal, au-dessus du plateau, pour prendre aussi les billes légèrement en vol dans la descente. Une bille au sol ne valide pas ce retour.

![Après correction](after.png)
![Détail de Ramp1](after-detail.png)

## Validation

- `editor.txt` : **24 PASS, 0 FAIL** (règles et scène).
- `runtime.txt` : **20 PASS, 0 FAIL**, avec billes natives VPE. Six cibles, flippers, lanceur, pause, trajets complets à 2,5 / 3 / 3,5 m/s, même bille aux deux capteurs, absence de bonus répété, double livraison de 11 000 points avec contact scoop, réarmement, drains et nouvelle partie.
- `idempotence.txt` : seconde application sans modification du profil, de la pose ni de la hiérarchie.
- `geometry.txt` : aucune intersection détectée des triangles du sol et des parois de Ramp1 avec les autres maillages visibles de la table. Le contrôle de dégagement d'une bille posée sur le parcours haut donne au moins 18,75 mm, hors contact normal avec le retour métallique.
- Compilation Unity vérifiée : aucune erreur C#.

Ces essais sont des tirs contrôlés et une analyse géométrique ; ils ne remplacent pas une session prolongée d'équilibrage. L'erreur d'import URP préexistante `AutodeskInteractiveTransparent.shadergraph` reste présente au lancement de Play ; les tests ont repris après Error Pause.
