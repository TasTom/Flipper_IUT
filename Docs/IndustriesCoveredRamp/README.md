# Industries — Ramp1 basse et couverte (8 octobre 2026)

Correction appliquée dans la scène Industries ouverte, à la demande de l'utilisateur. Elle relève des sections Rampes et Physique du GDD. `ProductionWoodRamp` reste supprimée ; Ramp1 conserve la transformation commune des matières et son retour métallique gauche.

La scène reste modifiée **sans enregistrement automatique** : Ctrl+S conserve le résultat. Le menu `Flipper > Industries > Retirer la rampe bois et corriger Ramp1` applique cette version aux scènes précédentes, avec Undo. Le marqueur `ProductionLayout/Ramp1CoveredRefit` protège ensuite les réglages manuels.

## Géométrie finale

- L'entrée est au milieu de `Primitive28` et `Primitive29`, à `(0,377270 ; 0 ; -0,531340)` dans le repère natif du plateau. L'axe d'entrée est perpendiculaire à la ligne des poteaux. Leur placement reste intact.
- Le tracé suit le contour extérieur réel de `Wall36`, auquel est ajusté `Carter_Wall36`. Les courbes partent des sommets du maillage natif, avec un décalage tenant compte de la largeur de rampe.
- Le sol culmine à **46,22 mm**, contre 84,60 mm auparavant. Les parois visibles et physiques mesurent **20 mm**, contre 55 mm.
- Le plafond `Ramp1TurnCeiling` couvre la montée après le début de l'entrée et les virages. Sa face inférieure suit le sol, avec **34 mm de passage vertical** pour une bille de 26,99 mm. Son bord d'entrée est progressivement relevé. La plaque mesure 1,2 mm d'épaisseur ; ses attaches en cuivre sont du décor sans collider.
- Le plafond porte un `PrimitiveComponent` et un `PrimitiveColliderComponent` **VPE**, avec des faces orientées vers l'intérieur du passage. Un MeshCollider PhysX ne participerait pas à la simulation de cette scène.
- Le plastique du plafond utilise un matériau transparent dédié, sans masquer le parcours. Le matériau existant du sol et des parois est conservé.
- Largeur : 42 mm à l'entrée, puis 31,31 mm au raccord du retour. Le raccord conserve le niveau de 39,40 mm et un intervalle de 1,7 mm entre les deux sols.
- Les capteurs d'entrée et de sortie suivent la nouvelle rampe. L'ancien clapet `Gate`, dont les supports traversaient le nouveau virage, est masqué et son collider désactivé ; son hôte reste présent. Les anciens couvre-rampes Ramp6/Ramp7 restent masqués.
- Le portique retrouve sa hauteur d'origine : sa surélévation de 45 mm n'est plus nécessaire. Seuls les maillages identifiés comme provenant de la correction précédente sont remplacés.

## Vérification

- [30 contrôles d'éditeur](editor.txt) passent : règles de production, capteurs, entrée centrée, axe du portail, hauteur, parois, plafond et raccord.
- [23 contrôles en Play](runtime.txt) passent : six cibles accessibles, tirs complets à **3,5 / 4,5 / 5,5 m/s**, même bille entre entrée et sortie, bonus sans répétition, livraison et réarmement, trois drains et nouvelle partie. Un tir faible à **2,5 m/s** redescend par l'entrée ; deux impulsions verticales dans les virages heurtent réellement le plafond et restent dessous.
- [Contrôle géométrique](geometry.txt) : aucune intersection transversale détectée du sol, des parois ou du plafond avec les maillages voisins visibles. Ce contrôle par triangles ne prouve pas l'absence de contacts coplanaires ou de tout blocage possible en jeu. Les attaches touchent volontairement les bordures et le plafond.
- Dégagement minimal relevé pour une bille centrée sur la partie élevée du parcours : **9,36 mm** avec le décor voisin, hors contacts prévus avec la rampe, son plafond et le retour.
- [Réapplication](idempotence.txt) : profil, pose, spline et hiérarchie conservés. Compilation Unity terminée sans erreur C#.

Les essais sont des trajectoires contrôlées dans le moteur natif, pas une session longue jouée à la main. L'erreur URP préexistante concernant `AutodeskInteractiveTransparent.shadergraph` est toujours présente au lancement Play ; elle n'est pas une erreur C# introduite par cette correction.

## Captures

| Avant | Après |
| --- | --- |
| ![Avant](before-detail.png) | ![Après](after-detail.png) |

[Table entière avant](before.png) · [Table entière après](after.png)
