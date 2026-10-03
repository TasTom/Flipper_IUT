# Caméra et fronton communs

Neutral est la référence. Industries reprend sa projection, sa rotation de borne, son cadrage relatif et son `Backglass`. Les unités métriques et l'origine du plateau VPE imposent une conversion de coordonnées ; les pièces physiques ne sont pas transformées pour obtenir ce cadrage.

- `comparison.txt` : trois formats d'écran et onze textes comparés, zéro échec.
- `Neutral.png` / `Industries.png` : captures en Play Mode ; Industries affiche la session transférée à 100 000 points, puis 500 points supplémentaires, sept billes et ×2.
- `Neutral.txt` / `Industries.txt` : état de caméra, texte et coordonnées du fronton lors des captures.
- `before.txt` : inspection initiale des deux scènes.
- `scene-diff-review.txt` : géométrie des 149 maillages intégrés identique après régénération de leurs identifiants par VPE ; aucun collider ni pose physique existants modifiés. Les différences de transforms existants portent sur la caméra et les enfants de la racine de présentation.
- `probes/*.cs.txt` : sources des opérations exécutées via `unity command eval_file`. Les copies `.txt` ne compilent pas dans Unity. Pour les réutiliser, les copier vers `Tools/unity/*.cs`. Le script d'adaptation s'arrête si le fronton commun existe déjà et n'enregistre pas la scène. Les sondes de comparaison sont en lecture seule ; elles ouvrent Neutral temporairement et la ferment sans l'enregistrer.

Le fronton conserve son fonctionnement par événements de `ScoreManager` et `GameManager`. Seuls le nom de l'objectif et son compteur correspondent à la production d'Industries. Le HUD blanc historique superposé dans Neutral n'est pas copié.

La correction de visibilité maintient les seuls renderers déjà répertoriés par `IndustriesPresentation` masqués après un rechargement VPE, dans l'éditeur comme en partie.

Validation dans l'éditeur ; l'ancien exécutable Windows archivé n'intègre pas encore ce cadrage.
