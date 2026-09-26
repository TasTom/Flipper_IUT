# Neutral — Vosges Mania

> **Historique du 25 septembre.** La géométrie décrite ci-dessous est remplacée par la [version du 26 septembre](NeutralProfessional.md). Les anciens résultats ne valident pas l'implantation actuelle.

État du 25 septembre 2026. La scène `Assets/Scenes/Neutral.unity` est enregistrée et reprend la disposition générale du plan fourni : orbit supérieur, bumpers regroupés en haut, rampe diagonale, centre dégagé et retours latéraux vers les flippers. L’orientation de la caméra de borne et les réglages des flippers sont conservés. [Capture en jeu](NeutralFinal.png).

## Implantation et corrections

- Deux rampes continues créées dans Blender : entrées évasées, virages progressifs, retours descendants, rebords et couvertures transparentes. Passage intérieur de 0,95 unité pour une bille de diamètre 0,45 ; sorties de 0,65.
- Quatre rails inférieurs continus séparent les retours des sorties latérales. Les anciens guides arrêtaient la bille près des flippers : leurs colliders et renderers sont remplacés. Les retours rejoignent désormais réellement les flippers.
- Les outlanes restent des sorties de perte normales. Elles sont séparées des inlanes, qui ramènent la bille en jeu. Chaque outlane compte une seule perte puis la partie sert la bille suivante. Le sauvetage gauche existant reste une aide limitée.
- Exactement **six cibles de récompense**, selon la demande explicite de l’utilisateur : trois Matières (ALGO, WEB, BDD), une Café, une Projet, une Java. Cette décision remplace pour Neutral le nombre de cibles du GDD. Modèle `Parts/Switches/Target_Rect_Thin.fbx`, voyants et colliders correspondants.
- Bumpers, lampes, spinner, gates et inserts repositionnés ; anciennes séparations qui traversaient les passages retirées du rendu et des collisions. Vitre à Y local 2,45 pour dégager les rampes superposées.
- Habillage Vosges Mania sans collider : bleu nuit, courbes topographiques, montagnes, bordures biseautées et boulons Parts. Repères RETOUR et SORTIE dans les couloirs inférieurs.
- Le score de rampe exige que la même bille passe l’entrée puis la sortie dans le bon sens. Une autre bille ne peut pas consommer la récompense ; une traversée ne compte qu’une fois.
- `RampSurfaceContact` applique la restitution du matériau de rampe aux seuls contacts de ces surfaces. Le mode Maximum du matériau de bille imposait autrement son rebond sur les pistes. Aucune propulsion artificielle n’est ajoutée aux retours.

Les scripts d’éditeur sont déclenchés explicitement, utilisent Undo et n’enregistrent jamais automatiquement la scène. `FinishNeutralPresentation` reste additif ; `RebuildNeutralShots` correspond à la refonte demandée ; `FinishNeutralReturns` raccorde les nouveaux rails et conserve les repères déjà présents.

## Validation

### Passe de cohérence supplémentaire

- Les premiers retours touchaient parfois l'extrémité extérieure des flippers. Les courbes finales ont été remontées pour présenter la bille sur leur face jouable.
- Les colliders rectangulaires des slingshots dépassaient des plastiques triangulaires et pinçaient le retour droit. Ils sont remplacés par des enveloppes convexes issues des maillages visibles, sans modifier les réglages des flippers.
- La vitre ne couvrait que 10,25 × 18,25 unités alors que le plateau avait été élargi et allongé. Elle couvre maintenant toute la table ; les côtés et l'arrière sont fermés jusqu'à sa hauteur. La zone de drain couvre également les pertes aériennes sous tout le bas du plateau.
- Le modèle des cibles culminait à seulement 0,135 unité pour un collider haut de 0,6. Les faces visibles sont remontées exactement à 0,6 ; les trois cibles droites sont placées à X=3,2 pour dégager leurs voyants et légendes du retour de rampe.
- Sept inserts étaient verticaux : ils sont maintenant encastrés horizontalement. Plastiques harmonisés, anciens supports isolés masqués, doublon du visuel de kickback supprimé, lettres I/U/T sur les trois rollovers existants. Il reste exactement six `SubjectTarget`.
- Les billes du multiball naissaient superposées au lanceur. Quatre sorties orientées vers les flippers sont désormais configurées, avec vérification du volume libre avant chaque création. Les deux billes supplémentaires apparaissent à 0,6 unité l'une de l'autre pour un diamètre de 0,45. Une sortie occupée est ignorée ; si toutes sont occupées, le système avertit sans créer de bille dans un obstacle.

Les essais supplémentaires utilisent huit timings de frappe sur chaque combinaison de rampe et de vitesse (40/60/85), soit 48 trajectoires. Les flippers sont actionnés via leur méthode de rotation existante dans une simulation contrôlée, avec le plafond de vitesse réel de la bille. Aucun essai de touche physique n'est simulé par ce test. Les rapports `playable-return.txt`, `return-face.txt` et `multiball-clearance.txt` détaillent les résultats.

Sondes dans Unity 6000.6.0f1, en Play, avec les colliders réels et `Physics.Simulate` au pas de 5 ms. Ces scénarios contrôlés ne couvrent pas toutes les trajectoires possibles. Les délais ne progressent pas pendant une simulation synchrone : le service de la bille suivante a donc aussi été vérifié après une attente réelle entre deux commandes.

| Contrôle | Résultat |
| --- | --- |
| Deux rampes, 40/60/85 unités/s, huit timings de frappe | 48 essais sans fuite ; 47 relances au-delà de Z=6 |
| Tirs insuffisants à 25 unités/s | Retour sans récompense |
| Outlanes G/D, 5/25/60 unités/s | 6/6, une seule perte et deux billes restantes |
| Service suivant après le délai réel | Une bille vivante, état ReadyToLaunch |
| Accessibilité physique des six cibles depuis le centre | 6/6 |
| Collisions, récompenses et voyants des six cibles | 6/6 |
| Sphères de contrôle sur les chemins des rampes | Aucun obstacle étranger aux échantillons |
| Orbit supérieur à 35 et 55 unités/s | 2/2 |
| Identité de bille, sens de passage et reset des rampes | Réussis |
| Missions Matières, Café et Java | Progression vérifiée |
| Lanceur, trois bumpers, deux slingshots, drain central | Réussis |
| Pause, trois pertes, game over, restart, reset missions/bumpers | Réussis |
| Suivi du multiball et suppression au restart | Réussis |
| Cinq sons, absence de musique doublée, slots visuels manquants | Réussis |

Rapports archivés dans `Docs/NeutralValidation/`, originaux dans `Tools/unity/out/`. `reference-return-delivery.txt` décrit la première passe ; `playable-return.txt` est la vérification renforcée de la version finale. Sondes correspondantes dans `Tools/unity/`. Les sondes temporaires de contacts ont été retirées d’Assets après les essais ; leurs sources sont conservées dans `Tools/unity/validation-support/`. Compilation réussie, aucun script manquant. Les erreurs de délai du bridge Pipeline survenues pendant ses rechargements sont des erreurs d’outillage. Les essais ont été relancés après le rechargement de scripts en Play, qui invalidait les singletons temporaires de cette session.

Pas de nouvel exécutable Windows/WebGL produit, ni de test matériel complet sur la borne. Un essai humain reste nécessaire pour régler la difficulté ressentie et les trajectoires produites par les boutons réels.

## Sources et maintenance

Pièces Parts : [vbousquet/pinball-parts](https://github.com/vbousquet/pinball-parts), CC BY-SA 4.0, licence dans `Tools/pinball-parts/LICENSE.md`. Les cibles et boulons sont repositionnés/recolorés ; attribution conservée. Aucun nouvel asset externe téléchargé pendant cette refonte.

Pistes, rails, bordures, graphismes procéduraux et sons créés pour le projet. Sources locales : `Tools/blender/prepare_neutral_ramps_v2.py`, `Tools/blender/build_neutral_trim.py`, `Tools/generate_neutral_sfx.py` et scripts d’éditeur. Fichier Blender et manifeste dans `Tools/unity/out/neutral-v2/`. `Tools/` reste ignoré par Git ; les assets importés sont dans `Assets/Generated/NeutralPresentation/`.

Correspondance GDD : rampes et loop, flippers principaux, bumpers, cibles et missions, aides contre la frustration, score, gestion des billes, contrôles et architecture modulaire. Le nombre de six cibles et le changement d’implantation proviennent des consignes explicites de l’utilisateur.
