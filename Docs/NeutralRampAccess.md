# Neutral — accès aux rampes depuis les flippers

Date : 2 octobre 2026. Branche : `fix/neutral-ramp-access`.

La scène enregistrée permet un parcours complet **flipper gauche → IUT** et
**flipper droit → Vosges**. Les entrées sont évasées ; IUT est rapprochée de la
trajectoire utile du flipper gauche et sa montée initiale est allongée.
La bille monte par sa vitesse physique, sans impulsion d'assistance ajoutée.

Cette correction répond au [GameDesignDocument](../GameDesignDocument.docx),
§Rampes : deux parcours de circulation, suffisamment larges pour être réalisables
tout en demandant de la précision. Le barème associé reste celui du §Barème.

## Mesures et correction

Le diagnostic initial utilisait les flippers existants, leur lecture d'entrée
réelle et leur mouvement `Rigidbody.MoveRotation`. Dans le balayage initial de
21 timings par côté, aucun tir gauche ne terminait IUT ; un tir droit terminait
Vosges. Les tirs gauches utiles passaient à gauche de la bouche IUT.
La pente maximale de sa montée atteignait 40,25°.

| Mesure dans le repère de la table | Avant | Après |
| --- | --- | --- |
| Bouche IUT, centre `(x, y, z)` | `(0,90 ; 0 ; 9,65)` | `(0,25 ; 0 ; 8,90)` |
| Largeur libre à l'entrée IUT | 0,6072 u | 0,9500 u |
| Pente maximale de montée IUT, points 0–64 | 40,25° | 33,92° |
| Bouche Vosges, centre | `(−1,65 ; 0 ; 9,65)` | Même centre |
| Largeur libre à l'entrée Vosges | 0,6072 u | 0,9000 u |
| Pente maximale de montée Vosges | 16,44° | 16,44° |

L'évasement rejoint progressivement la largeur intérieure existante de 0,6072 u.
La bille mesure toujours 0,414 u : le passage courant conserve environ 1,47
diamètre de bille. Avec l'ancre de 60 mm/u, les bouches mesurent environ 57 mm
et 54 mm. La couverture conserve un dégagement normal de 0,5428 u.

Le fond, les parois, la couverture, la protection sous la rampe et les tubes
visuels des bouches sont mis à jour ensemble. L'entrée de détection IUT et les
repères de tir suivent la nouvelle pose. Les sommets du track à partir du point
34, les sorties et les retours existants sont conservés. Les nouveaux maillages
sont isolés dans `Assets/Generated/NeutralRampAccess` ; les anciens restent disponibles.
L'application passe par Undo et l'enregistrement de scène est une action séparée.

## Validation sur la scène sauvegardée

| Essai | Résultat |
| --- | --- |
| 21 timings de bouton, gauche → IUT | 6 parcours complets ; notamment les timings voisins 3,40 à 3,30 |
| 21 timings de bouton, droit → Vosges | 2 parcours complets ; timings voisins 3,375 et 3,350 |
| 36 tirs à la bouche : 6 vitesses × 3 décalages × 2 rampes | 12 parcours complets à 35/45 u/s ; 24 retours à 15/20/25/30 u/s ; aucun blocage |
| Dégagement des centres de bille, points 0–64 | Aucune intersection avec un collider étranger sur les deux montées |
| Contacts de score, cibles et boucle | 14 contrôles réussis ; rampes à 7 500/10 000 points et compteur incrémenté une fois |
| Contrôle de scène | 48 autres colliders identiques, six cibles, flippers et diamètre de bille préservés |
| État final | Scène enregistrée, mode édition, aucun script manquant, aucune erreur ou alerte Console |

Les timings sont des seuils de position `z` de la bille descendante, pas des
secondes. Le tir de flipper commence dans le retour de lane à 12 u/s ; un bouton
Gamepad temporaire est envoyé au nouvel Input System et `Flipper.FixedUpdate`
lit cette commande à chaque pas physique de 0,005 s. La vitesse et l'angle de
tir ne sont pas imposés. Seul le plafond de vitesse normal de `BallManager`
est exécuté ; sa récupération anti-blocage ne participe pas aux essais.
La réussite exige l'événement de sortie `RampGate.Completed`.

Ces balayages incluent volontairement des tirs mal orientés : leur taux n'est
pas un taux de réussite en partie. Le premier balayage utilisait un pas de
0,050 u, le dernier 0,025 u ; leurs proportions ne se comparent pas directement.
Vosges demeure un tir de précision. Une vitesse d'entrée élevée ne garantit
pas une réussite si la bille arrive en biais ou après un ricochet.

## Reproduction et limites

Les [recettes et rapports](NeutralRampAccessValidation) contiennent les poses,
trajectoires, contrôles et une [capture finale](NeutralRampAccessValidation/neutral-ramp-access.png).
Les recettes `.cs.txt` se copient dans `Tools/unity` avec l'extension `.cs`
avant `unity command eval_file`.

Pour les essais de boutons, commencer **une nouvelle session de Play par côté**,
puis lancer `test_ramp_flipper_left` ou `test_ramp_flipper_right`. La répétition
de rebindings vers des périphériques temporaires dans une même session a
produit un défaut du banc : contrôle pressé mais action en attente. Ces essais
invalides ne servent pas de preuve. Les rapports finaux proviennent de sessions
fraîches et vérifient que le flipper lit effectivement le bouton à chaque pas.
Le balayage initial de 82 tirs a aussi dépassé le délai CLI de 5 s tout en
finissant son rapport ; les tests finaux sont séparés et terminent sans timeout.

Cette passe valide l'accès et le franchissement des rampes. Elle ne certifie
pas tous les tirs possibles, une partie complète sur la borne physique,
les performances d'un build ou les fonctionnalités restantes du GDD.
