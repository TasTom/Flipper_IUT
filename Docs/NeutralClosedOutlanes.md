# Neutral — fermeture des passages extérieurs

Correction du 3 octobre 2026, selon le choix explicite « Fermer les passages
extérieurs ». Elle se rattache au GDD : agencement de la table, retours de rampe
et zone de sortie. Cette variante conserve le drain central et supprime les
pertes dans les deux outlanes.

Les maillages physiques et visuels d'origine de `InlaneOuter_Left` et
`InlaneOuter_Right` sont conservés. Six protections séparées sont ajoutées sous
`PinballTable/Rails/ClosedOutlanes` : deux fermetures courbes côté mur, deux
déflecteurs sous les retours et deux protections métalliques derrière les guides.
Ces dernières suivent exactement la courbe existante et retiennent les rebonds
au-dessus de l'ancien guide, sans modifier sa face intérieure au niveau du sol.
Les anciens triggers de drain latéral et les indications « SORTIE » sont retirés
du fonctionnement et de l'affichage ; leurs objets restent présents.

Le raccord des déflecteurs est à z=6,15, sous la première cible : à z=6,55,
un essai créait un pincement entre cette cible et le déflecteur. La partie basse
des anciens supports pleins de rampe est remplacée par ce passage diagonal.
Les rampes, rails, couvertures et capteurs de sortie restent inchangés.
La revue finale a aussi rétabli `OrbitFeedCurve_right`, présent dans la sauvegarde
initiale mais absent de la scène en mémoire, en recopiant ses composants et sa
pose depuis cette sauvegarde. Les contrôles ont été refaits après restauration.

Le menu **Pinball > Neutral > Fermer les passages extérieurs** appelle
`CloseNeutralOutlanes.Apply()`. Il utilise Undo, crée seulement les protections
manquantes, conserve les ajustements manuels lors des appels suivants et
n'enregistre jamais la scène. Son idempotence a été vérifiée par comparaison
des composants sérialisés avant et après un second appel.

Validation dans l'éditeur Unity 6000.6.0f1, avec la physique réelle de Neutral :

| Vérification | Résultat |
| --- | --- |
| 108 trajectoires extérieures, positions et vitesses identiques avant/après | 91 pertes latérales avant, 0 après ; aucun blocage après |
| 72 retours au sol, depuis la rampe et avec vitesse latérale | 72 contacts avec un flipper, 0 perte, 0 blocage |
| 21 timings de tir par flipper, commande réelle par Input System | Rampe IUT et rampe Vosges complétées chacune par un tir ; tous les timings ne réussissent pas |
| 36 entrées de rampe, vitesses 15–45 et décentrage ±0,08 | 11 parcours complets, 25 retours/pertes, 0 blocage |
| Drain central | Une seule perte décomptée ; 3 billes deviennent 2 |
| Contrat de scène et mécanismes | 130 contrôles sans échec |
| Protections, maillages d'origine, matériaux et drain | 37 contrôles sans échec en Play |

Les essais simulent des pas de 0,005 s ou le pas physique du projet. Ils appellent
explicitement la limite de vitesse et, pour les tirs, le contrôleur du flipper.
Ils vérifient des trajectoires ciblées ; ils ne garantissent pas tous les rebonds
possibles d'une partie. Les pertes constatées dans les 108 essais finaux passent
par le drain central, avec les flippers au repos.
Les recettes de tir placent temporairement l'Input System en traitement manuel
des événements et restaurent ses paramètres à la fin, pour que les entrées
simulées restent lisibles pendant la simulation exécutée dans un seul appel CLI.

Rapports, capture et recettes : [NeutralOutlaneValidation](NeutralOutlaneValidation/).
Pour reproduire une recette `.cs.txt`, la copier en `.cs` dans `Tools/unity/`, puis
l'exécuter avec `unity command eval_file <chemin absolu> --caller plugin
--skill unity-bug-investigation --format json`. Les recettes de tir doivent être
exécutées au début d'une session Play distincte et utilisent les chemins de
rampe de `Docs/NeutralMechanismValidation/mechanism-paths.json`, à copier dans
`Tools/unity/out/mechanism-paths.json` si nécessaire. Les rapports sont écrits
dans `Tools/unity/out` ; aucun harness de test n'est conservé dans la scène.
