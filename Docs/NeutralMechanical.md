# Neutral — finition des assemblages

Passe du **2 octobre 2026**, après la [réduction de 8 %](NeutralScale.md).
Scène enregistrée : `Assets/Scenes/Neutral.unity`.
[Vue de borne](NeutralValidation/2026-10-02-mechanical/NeutralMechanical.png) ·
[Assemblages inférieurs](NeutralValidation/2026-10-02-mechanical/LowerAssemblies.png) ·
[Terminal](NeutralValidation/2026-10-02-mechanical/Terminal.png).

## Changements

Les deux slingshots reçoivent un caoutchouc arrondi, trois poteaux métalliques,
un plastique biseauté et surélevé, trois fixations et un motif imprimé discret.
Les contours reprennent les plastiques du dépôt Parts et la face active suit la
bande existante. Les poteaux mesurent 23,81 mm de haut dans l'ancre historique
de 60 mm par unité. Le dessous du plastique est à 23,73 mm ; l'épaisseur est
de 2,70 mm. Les têtes de fixation dépassent le dessus du plastique de 1,81 mm.
Seul le plastique réagit au flash ; le métal et le caoutchouc conservent leur matériau.

Les retours des deux rampes reçoivent douze brides de maintien et leurs fixations,
posées sur les sections existantes. Cette passe finit les assemblages des retours :
elle ne reconstruit pas leurs trajectoires.

Le boss remplace ses blocs visuels superposés par un terminal compact, biseauté,
avec cadre métallique, écran incliné et pieds. Ses couleurs d'état affectent l'écran,
sans repeindre le boîtier. Le volume reste celui du collider existant.

Le contrôle visuel a également révélé que les noms JAVA et CAFÉ étaient éloignés
de leurs cibles. Les **six légendes** sont maintenant centrées sur leurs plaques,
avec une orientation commune. Les six cibles restent ALGO, WEB, BDD, JAVA, PROJET, CAFÉ.

## Validation

Rapports et recettes : [2026-10-02-mechanical](NeutralValidation/2026-10-02-mechanical/).

| Contrôle | Résultat |
| --- | --- |
| Colliders actifs, propriétés et matrices avant/après | Strictement identiques, y compris après Play |
| Nouveaux assemblages | Aucun collider, aucun matériau absent |
| Slingshots, impacts physiques réels au pas de 5 ms | 100 points chacun, retour vers l'intérieur, flash du nouveau plastique |
| Boss, trois impacts physiques | Détection puis état Correction ; écran rouge puis ambre |
| Notification de correction et impact final | Compilation bleue puis victoire verte ; score existant de 165 000 points |
| Six légendes | Écart de centrage inférieur à 0,000001 u sur les plaques |
| Intégrité | Six cibles, aucun script manquant ; scène sauvegardée, hors Play |
| Console Unity après essais | Aucune erreur actuelle, `compilationFailed=false` ; le journal du bridge conserve une ancienne erreur d'outillage |

Les ajouts représentent 57 renderers et **13 296 triangles**. Le plateau visible
compte 101 428 triangles dans cette mesure. Les pièces restent séparées pour
l'édition ; cette passe n'est pas une validation de performance WebGL.

Les trajectoires et les réglages physiques n'ont pas changé ; les résultats des
[essais de proportions](NeutralScale.md) restent la référence pour les rampes,
le lanceur, les cibles, les outlanes et le multiball. Les essais de cette passe
portent sur les systèmes dont les références visuelles ont changé. Ils ne
remplacent pas une partie humaine ni un essai sur la borne xin-mo.

## Références et maintenance

Correspondance GDD : **slingshots**, **rampes**, **Boss / Projet Final**, direction
artistique Vosges/IUT et lisibilité des cibles. Le nombre de six récompenses et
l'orientation de borne restent les choix explicites de l'utilisateur.

Référence de construction : [manuel officiel Stern Jurassic Park LE/Premium](https://sternpinball.com/wp-content/uploads/2023/04/JurassicPark_LE_Pre_web.pdf),
§5.24–5.25 pour plastiques, entretoises, rondelles et fixations, et §5.20 pour
l'assemblage wireform. L'adaptation de ces principes au volume déjà jouable de
Neutral est un choix de construction du projet, pas une reproduction cotée de Jurassic Park.

Pièces : [vbousquet/pinball-parts](https://github.com/vbousquet/pinball-parts), CC BY-SA 4.0.
Le nouvel FBX et ses adaptations suivent cette licence ; voir
[NOTICE](../Assets/Models/NeutralMechanical/NOTICE.md). Les matériaux sont propres
à cette passe ; les modèles Parts d'origine n'ont pas été modifiés.

Les assemblages des slingshots et du terminal sont enfants de leurs hôtes de
gameplay. Les brides sont dans `ReferencePlayfield/MechanicalFinish/RampMountings`.
Les anciens rendus sont masqués par leurs renderers ; leurs hôtes restent actifs.
Les créations et modifications de scène passent par Undo. Les recettes archivées
sont des traces de cette passe explicite, avec garde de réapplication, et ne
doivent pas être relancées automatiquement. La sauvegarde a été effectuée
séparément par la commande Unity.

Cette finition ne clôt pas le GDD. La prochaine étape de production est le
**score complet** : combos, bonus de fin de bille et affichage du multiplicateur.
Les autres écarts restent recensés dans [l'audit du GDD](NeutralGddAudit.md).
