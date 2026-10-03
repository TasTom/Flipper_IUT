# Adaptation d’Industries dans Neutral

Neutral conserve PhysX, `Flipper.cs`, `InputRouter` et les actions de la borne. Industries conserve son moteur VPE. Cette adaptation répond aux sections **Bille et lanceur**, **Flippers principaux**, **Contrôles**, **Plateau** et **Interface utilisateur** du `GameDesignDocument.docx`, ainsi qu’à la demande explicite de reprendre les pièces et indications d’Industries.

## Résultat

- Les deux bats proviennent des meshes `Base` et `Rubber` des flippers d’Industries. Les meshes sont copiés dans des assets persistants, avec les matériaux et textures nécessaires. Les générateurs et composants physiques VPE ne sont pas ajoutés à Neutral.
- L’axe est recentré sur le moyeu. L’encombrement longitudinal au repos est conservé. Un collider convexe arrondi suit le bat et remplace le collider rectangulaire ; les anciens renderers et colliders sont désactivés, leurs objets et références restent présents.
- La bille reçoit une finition URP issue de la bille VPE, avec une surface d’acier poli et sans son quadrillage de diagnostic. Son diamètre reste **0,414**. Le nouveau `NeutralBall.prefab`, câblé sur les gestionnaires de partie et de multiball, garantit la même taille et les mêmes contacts aux billes supplémentaires. Le prefab `Ball.prefab` utilisé par Main est inchangé.
- Deux cartes reprennent le style des cartes d’Industries. Elles indiquent Q/A, D, Espace et Échap, puis les six cibles, les deux rampes et le passage à Industries à 100 000 points. Elles sont posées sur l’apron, sans collider, à l’écart de la marque centrale.

## Profil PhysX

Les coefficients VPE et PhysX ne représentent pas des moteurs interchangeables. Le profil ci-dessous adapte le comportement et a été vérifié dans Neutral ; il ne reproduit pas intégralement les corrections de flipper, le live catch ou les modèles de contacts VPE.

| Réglage | Valeur |
|---|---:|
| Pas physique | 0,002 s, 500 Hz |
| Montée des flippers | 2 600°/s |
| Retour des flippers | 700°/s |
| Masse de bille | 2 unités, conservée |
| Amortissement linéaire / angulaire | 0 / 0,02 |
| Contact offset de bille et bats | 0,003 |
| Plateau : friction / restitution | 0,075 / 0,25 |
| Métal : friction / restitution | 0,075 / 0,45 |
| Caoutchouc : friction / restitution | 0,8 / 0,8 |
| Surface des rampes | Matériau existant : friction 0,025, restitution 0 |

La bille n’impose plus sa restitution de 0,65 par combinaison Maximum à toutes les surfaces. Les surfaces choisissent le rebond ; les sols et couvercles des rampes restent sans restitution. La gravité 163,5, l’inclinaison 7°, les angles des flippers et les limites de difficulté restent ceux de Neutral.

`PinballPhysicsConfig` contient les matériaux, le pas et les vitesses. Le champ optionnel ajouté à `Flipper` ne modifie que sa vitesse de rotation ; toutes ses méthodes de lecture d’entrées, ses enums sérialisées et ses événements sont conservés. Sans profil, `Flipper` et `TableGravity` gardent leur comportement précédent. `PinballBallPhysics` applique le profil au démarrage et avertit proprement si une référence manque.

## Vérifications

- Compilation Unity terminée sans erreur de C#.
- **28 contrôles runtime réussis**, comprenant une bille réellement instanciée, l’utilisation du bon prefab, les colliders, les matériaux et le cumul clavier/manette. Le clavier est testé par les états du routeur ; la manette est un Gamepad virtuel qui emprunte les actions existantes avec des overrides temporaires, ensuite restaurés. Aucun test de bouton sur la borne physique n’est revendiqué.
- **15 contrôles de scène enregistrée réussis**, dont les poses -30°, 0° et +30° de chaque flipper sans pénétration de plus de 0,01 unité dans un collider statique, le cadrage de borne et les assets persistants.
- Lancement réel : charge 1, course 0,8, cache fixe ; bille jusqu’à **z = 17,368**.
- Balayage de 21 timings depuis chaque inlane : la session finale franchit entièrement la rampe IUT depuis le flipper gauche et la rampe Vosges depuis le droit. Les rapports détaillent chaque essai, dont les tirs manqués. Ces timings contrôlés démontrent l’accessibilité ; ils ne constituent pas un taux de réussite d’un joueur ni une preuve couvrant tous les angles d’arrivée.
- La transition de score est désactivée uniquement dans les sessions de test des contacts, pour rester sur Neutral pendant les essais. Elle est rétablie en quittant Play. Les contrôles du lanceur et les snapshots sont réalisés dans le vrai mode Play. Le mode de simulation manuelle n’est utilisé que pour les balayages reproductibles de contacts.
- Images inspectées : `Neutral-authoring-upright.png` est une vue verticale de contrôle rendue temporairement avec la caméra redressée ; `Neutral-cabinet.png` est la vraie vue du jeu avec son orientation de borne. La caméra enregistrée reste `(0 ; 23,84255 ; -16,60174)`, rotation `(40,22 ; 0 ; 90)`, taille orthographique 4,5.

La Console conserve deux messages d’import déjà présents concernant `AutodeskInteractiveTransparent.shadergraph` du package URP. Le rapport `console.json` les distingue de la compilation. Aucun nouveau build Windows ou WebGL n’a été effectué pour cette adaptation.

## Sources et reproduction

Les sources sont les pièces **déjà importées dans Industries** : meshes procéduraux des flippers VPE, matériaux et textures de `Assets/Tables/ExampleTable`, matériau de `Assets/Resources/VpeUrp/Ball.prefab`, et cartes de `Assets/Generated/Industries`. `baseline.txt`, `geometry.txt` et `material-sources.txt` enregistrent les références et mesures exactes. Les attributions des assets amont restent applicables.

Les fichiers `scripts/*.cs.txt` archivent les commandes C# exécutées par `unity command eval_file`. Les opérations d’auteur passent par l’éditeur et Undo ; l’enregistrement de scène est une commande séparée. Les helpers refusent une seconde application destructive. Pour rejouer les validations, copier les helpers `.cs.txt` dans `Tools/unity/` en `.cs`, ouvrir Neutral, entrer en Play, lancer `setup_transfer_validation.cs`, puis les validateurs. Quitter Play et rétablir les réglages temporaires de Console et d’exécution en arrière-plan après les essais.

Les modifications préexistantes de rotations de cibles, de police TMP et de ProjectSettings sont laissées dans le workspace et exclues du commit de cette adaptation.
