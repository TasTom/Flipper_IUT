# Neutral — combos, multiplicateur et bonus de bille

Date : 2 octobre 2026. Branche : `codex/neutral-score-rules`.

Neutral utilise maintenant `Assets/Config/NeutralScore.asset`. Cette passe complète les
combos chronométrés, la progression du multiplicateur, le bonus de fin de bille et leurs
affichages. Elle suit les sections **Score**, **Barème de points proposé**, **Combos**,
**Multiplicateur de score** et **Bonus de fin de bille** du
[GameDesignDocument](../GameDesignDocument.docx).

## Règles installées

| Action physique | Points de base avant multiplicateur |
| --- | ---: |
| Slingshot | 100 |
| Bumper | 1 000 |
| Cible Projet / cible simple | 500 |
| Cible Café | 1 500 |
| Cible Java | 2 000 |
| Cible Matières | 2 500 |
| Loop validé | 5 000 |
| Rampe Vosges validée | 7 500 |
| Rampe IUT validée | 10 000 |

La réussite d'une rampe reste déterminée par ses deux détecteurs, avec le suivi de la même
bille et le contrôle de sens existants. Un passage à la sortie seule ne rapporte rien.
Les six cibles demandées par l'utilisateur restent en place.

Les actions s'enchaînent dans une fenêtre de **4 secondes**, réglable dans la configuration.
Deux, trois, quatre et cinq actions donnent respectivement **2 500, 5 000, 10 000 et
25 000 points de combo**, avant le multiplicateur courant. Le panneau affiche
« COMBO ×1 », « COMBO ×2 », « COMBO ×3 », puis « SUPER COMBO ». Un impact immédiat répété
sur le même bumper ou la même cible ne prolonge pas la fenêtre. Une nouvelle réussite de
loop ou de rampe peut prolonger la chaîne, comme prévu par le GDD. Le Super Combo ne paie
qu'une fois par chaîne ; les impacts suivants continuent à rapporter leurs points de base.
Le son de cible existant accompagne les paliers avec une hauteur croissante.

Le multiplicateur part de **×1** et se plafonne à **×5**. Les paliers indicatifs du GDD sont
appliqués : série de cibles / rollovers → au moins ×2 ; deux missions → au moins ×3 ;
activation du boss → au moins ×4 ; activation réussie du multiball → ×5. Une récompense de
mission ne réduit plus un multiplicateur déjà supérieur. Les deux seuils complémentaires
sont des choix d'équilibrage configurables : quatre actions de combo ou trois rampes dans
la bille augmentent le multiplicateur d'un cran. Une perte retire un cran à partir de ×3 ;
×2 reste acquis. Une nouvelle partie revient à ×1.

Les bonus d'activation du boss, de compilation et de multiball sont ajoutés directement,
sans seconde multiplication : **50 000**, **100 000** et **75 000**. Les deux valeurs du boss
restent dans ses champs existants ; le nouveau bonus multiball est dans `ScoreConfig`.
Il est attribué après une création réelle de billes supplémentaires, pas si toutes les
sorties sont occupées, ni lors d'un nouvel appel pendant un multiball déjà actif.

Le bonus de bille suit la formule du GDD :

`matières validées × 5 000 + rampes réussies × 2 000 + combo maximal × 1 000`.

Une matière ne compte qu'une fois par bille. Le « combo maximal » désigne ici le palier
affiché, de 0 à 4, avec Super Combo au palier 4. Cette interprétation et la fenêtre de
4 secondes sont des choix d'équilibrage explicités, le GDD laissant ces détails ouverts.
Le bonus est versé directement, sans multiplicateur, puis affiché pendant le délai de
relance existant de **1,2 seconde**. L'annulation de ce bonus au tilt est un choix configurable
(`forfeitBonusOnTilt`), cohérent avec la pénalité de tilt existante.

## Perte de bille et affichage

Une bille logique se termine uniquement quand la dernière bille physique est perdue.
Les drains intermédiaires du multiball n'enlèvent ni bille de réserve ni bonus. La clôture
du score est idempotente et les signaux de drain répétés pendant la relance sont ignorés.
Le tilt retire aussi les billes physiques restantes ; son rappel différé est annulé à la
nouvelle bille ou au redémarrage pour ne pas perdre la bille suivante.

Le bonus est calculé **avant** la décrémentation finale et l'enregistrement du record.
Si ce bonus franchit un palier de bille supplémentaire, la réserve est créditée avant
de décider du game over. Un gros bonus franchissant plusieurs paliers crédite tous les
paliers concernés immédiatement, dans la limite configurée des billes supplémentaires.

Le panneau de la borne conserve son orientation et son cadrage. Deux textes ont été ajoutés :
`MultiplierValue` et `ScoreFeedbackValue`. Le second sert au combo, puis au bonus de bille,
y compris au game over. Le score principal et son fond de chiffres ont été repositionnés
pour réserver cet espace. Le HUD alternatif peut également recevoir ces deux références,
facultatives. Aucun objet de gameplay, collider ou réglage physique n'a été modifié par cette
installation ; les empreintes de tous les colliders actifs sont identiques avant et après.

Les scènes sans `ScoreConfig` gardent leurs points de composants et leurs multiplicateurs
de mission historiques. Elles n'activent pas les combos ou le bonus de bille. L'absence
de textes optionnels ne provoque pas d'exception. La clé de record locale existante est conservée.

## Validation

Les recettes et les rapports sont archivés dans [NeutralScoreValidation](NeutralScoreValidation).
Les recettes `.cs.txt` sont des sources de sonde à copier dans `Tools/unity/` avec l'extension
`.cs`, puis à exécuter par `unity command eval_file` dans l'éditeur connecté. Elles ne sont
pas des composants de gameplay.

- Compilation Unity réussie et aucun script manquant dans la scène.
- Contrôles des seuils de combo, de l'expiration, des répétitions, de la pause, des
  multiplicateurs, du bonus unique et du redémarrage.
- Trois billes de multiball réellement créées ; drains intermédiaires et dernière perte
  vérifiés. Tilt et annulation de son rappel vérifiés.
- Record final testé sur une dernière bille réelle : rampe 7 500 + bonus 2 000 = 9 500.
  Le record utilisateur dans `PlayerPrefs` est sauvegardé puis restauré par la recette.
- Bonus 75 000 franchissant deux paliers de bille supplémentaire et plafond de trois
  récompenses vérifiés ; bonus de bille sauvant la dernière réserve également vérifié.
- Collisions physiques des six cibles : 2 500 / 2 000 / 1 500 / 500 selon leur groupe.
  Chaque cible Matières touchée contribue effectivement au bonus.
- Parcours complets Vosges et IUT à 35 u/s : une validation et respectivement 7 500 et
  10 000 points. Détecteurs du loop traversés physiquement : sortie seule ignorée,
  puis 5 000 et 7 500 avec le combo de répétition.
- Contacts des trois bumpers à 1 000, des deux slingshots à 100, et déroulement physique
  du boss vérifiés. Six essais de drain latéral comptent une perte chacun.
- Trajectoires de rampes, retours aux flippers, lancement et validation des cibles
  revérifiés avec la bille de diamètre 0,414 u. Aucun nouveau blocage ou échappement
  observé dans ces essais contrôlés ; les tirs trop faibles redescendent normalement.
- Textes du score maximal, du multiplicateur, du Super Combo et du bonus vérifiés et
  inspectés visuellement en 1920 × 1080 et 1920 × 1200. Aucune troncature ni sortie de caméra.
- Nouveau lancement final avec Console propre : zéro erreur de jeu et compilation valide.

Un appel de sonde au démarrage a expiré dans le bridge Pipeline (limite de 5 secondes).
Le diagnostic a été archivé ; la recette corrigée a ensuite été exécutée avec succès.
Cela est distinct d'une erreur du gameplay. Une première sonde visuelle supposait un
`MeshRenderer` alors que les textes sont uGUI ; elle a été corrigée pour utiliser les limites
réelles des glyphes. Aucun de ces diagnostics ne subsiste dans le lancement final.

Cette validation se fait en Play Mode, avec simulation physique contrôlée. Elle ne
remplace pas une session humaine sur la borne, un build final ou une validation de tous
les modes du GDD.

## Reste du cahier des charges

Cette passe ferme les écarts principaux de combos, bonus de bille et multiplicateur de
l'audit précédent. **Le projet entier n'est pas terminé.** Restent notamment le mode
Nuit de l'Info avec jackpots et fin de mode lisible, les récompenses spécifiques Café/Java/
Réseau, le parcours de menus et difficultés, les statistiques de fin de partie et les
retours audio/éclairage de chaque mode. Les configurations de missions, bille, flippers
et audio restent à consolider ; les nouvelles règles de score sont déjà dans un
ScriptableObject. La dérogation à six cibles et les autres écarts de l'audit restent explicités.
