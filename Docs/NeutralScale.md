# Neutral — réduction légère de la bille et des pièces

2 octobre 2026. Demande utilisateur : réduire légèrement la bille et les assets de la table.

La passe applique un facteur **0,92** aux pièces concernées, sans réduire la table entière. Elle est enregistrée dans `Assets/Scenes/Neutral.unity`. Correspondance GDD : bille et lanceur, flippers, bumpers, slingshots, cibles, rampes et circulation de la bille.

## Changements

- Bille de scène : diamètre de **0,45 à 0,414 unité**. À l'échelle historique du projet, cela correspond à environ 24,8 mm ; il s'agit d'un choix esthétique arcade demandé par l'utilisateur.
- Nouveau prefab `Assets/Prefabs/Ball_Neutral.prefab`, utilisé par `GameManager` et `MultiballManager`. Le prefab partagé `Ball.prefab` conserve sa taille. Les trois billes du multiball mesurent toutes 0,414.
- Bumpers, six cibles et inserts, slingshots, flippers, spinner et support, boss et rollovers réduits de 8 %. Les colliders suivent les mêmes transformations que leurs modèles. Les points de placement principaux sont conservés.
- Flippers : battes d'environ 73,6 mm au lieu de 80 mm. Pivots rapprochés de 0,124 unité de chaque côté pour conserver le rapport entre ouverture centrale et diamètre de bille. La fin des inlanes rejoint les talons ajustés. Angles, vitesses et contrôles conservés.
- Rampes : sections réduites autour des lignes centrales existantes, sans raccourcir ni déplacer les circuits. Passage intérieur de **0,66 à 0,6072 unité** ; rapport largeur/diamètre conservé à **1,467**. Rebords, rails, plafonds et protections sous les rampes sont adaptés ensemble. Les protections inférieures restent jointives avec le plateau.
- Guides : sections affinées ; parois périphériques et couloir du lanceur conservés.
- Médaillons, cerclages et numéros des bumpers rattachés aux chapeaux animés. Les plaques et cerclages des cibles ainsi que les légendes des slingshots suivent leurs assemblages.
- Hauteur du point d'apparition abaissée de 0,018 unité pour conserver la distance entre le bas de la bille et le sol.

La masse de bille existante (2), les matériaux physiques, les impulsions et le cadrage de borne restent conservés. Les maillages ajustés sont des copies dans `Assets/Generated/NeutralScale8/` ; les modèles Parts et les maillages de la passe précédente ne sont pas modifiés.

## Validation

Essais dans l'éditeur Unity 6000.6.0f1, colliders réels de Neutral, simulation contrôlée au pas physique de la partie : **5 ms**, défini au démarrage par `TableGravity`. Une série identique a été exécutée avant puis après la réduction.

| Contrôle | Résultat après réduction |
| --- | --- |
| Deux rampes à 25/35/50/70 unités/s | Six tirs à 35/50/70 terminent avec l'événement réel `Completed` ; les deux tirs faibles redescendent. Aucun échappement ni blocage durable dans ces essais. |
| Volumes le long des rampes | Aucun collider étranger relevé le long des lignes centrales avec le nouveau rayon. |
| Retours gauche/droit vers les flippers | Les six timings provoquent une frappe ; cinq relancent au-delà de Z=9. Le tir droit le plus tardif reste plus court, comme avant la passe. |
| Six cibles | Six validations obtenues par impact. |
| Lanceur à 65/80/100 % | La bille atteint le haut de la table dans les trois cas, sans échappement ni immobilisation durable de l'aire de jeu dans ces essais. |
| Trois bumpers | Un impact et 150 points pour chacun ; décor enfant du chapeau, transforms finis. |
| Deux slingshots | Un impact et 100 points pour chacun ; renvoi vers le centre observé. |
| Spinner | 500 points et environ 75,6° de rotation après impact. |
| Outlanes gauche/droite à 5/25/60 unités/s | Six essais : une seule perte chacun, deux billes restantes. |
| Multiball | Trois billes suivies, chacune de diamètre 0,414. |
| Intégrité | Six cibles, aucun script manquant, références de prefab cohérentes, table à échelle 1 et inclinaison −7°. |
| Vue de borne | Capture 1920 × 1080 inspectée ; orientation et panneau de score conservés. |

Les tests pilotent les frappes de manière contrôlée. Ils ne certifient pas le ressenti sur les boutons xin-mo ni l'équilibrage de parties humaines. Aucun nouveau build Windows ou WebGL n'a été produit.

La Console ne montre pas d'échec de compilation. L'unique erreur relevée en fin de passe vient d'une commande d'inspection `verify_scene` absente du CLI ; l'intégrité a ensuite été vérifiée par une sonde fonctionnelle. Ce message n'est pas une exception du gameplay.

## Preuves et entretien

[Capture du résultat](NeutralValidation/2026-10-02-scale8/NeutralScale.png) et [relevés de validation](NeutralValidation/2026-10-02-scale8/).

La recette d'ajustement est archivée en `.cs.txt` dans ce dossier. Elle est explicite, protégée contre une seconde application et regroupe les changements de scène dans une transaction Undo. Elle ne sauvegarde pas la scène ; l'enregistrement a été effectué séparément via la commande Unity `save_scene`, après validation.

Cette passe ne termine pas les fonctions du GDD encore manquantes, recensées dans [l'audit du cahier des charges](NeutralGddAudit.md).
