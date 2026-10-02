# Neutral — animation du caoutchouc des slingshots

Date : 2 octobre 2026. Branche : `feature/slingshot-rubber-animation`.

Les deux bandes se déforment maintenant lorsqu'une bille déclenche leur
slingshot : compression brève, poussée vers l'aire de jeu, puis retour au repos
avec une légère oscillation amortie. L'effet se concentre autour du point
d'impact ; les portions autour des poteaux restent fixes.

Correspondance : [GameDesignDocument](../GameDesignDocument.docx), **§Slingshots**
(renvoi de la bille, dynamisme, points) et **§Objectif de stabilité**.
Référence mécanique : [documentation officielle VPE](https://docs.visualpinball.org/creators-guide/manual/mechanisms/slingshots.html),
qui décrit la bande, son bras actionné par une bobine et l'animation entre
repos et activation. La courbe et les amplitudes sont des choix de présentation
du projet, sans prétention de mesurer l'élasticité d'un caoutchouc réel.
Aucun code de VPE n'est importé.

## Intégration et réglages

`Slingshot.Kicked` émet le point de contact et la vitesse relative après un
impact accepté. `SlingshotRubberAnimation` écoute cet événement et déforme une
copie privée du maillage visible, sans changer le collider ni l'impulsion.
Les références sont câblées dans Neutral ; aucune installation manuelle n'est
nécessaire pour jouer.

L'asset `Assets/Generated/NeutralRubberAnimation/NeutralSlingshotRubberConfig.asset`
regroupe les réglages : **180 ms**, maximum **0,10 u**, diffusion et courbe.
À 15 u/s dans les essais, la compression atteint environ **0,025 u** (1,5 mm)
et la poussée **0,084–0,085 u** (5,1 mm), avec l'ancre de 60 mm/u du projet.

Les bandes FBX d'origine étaient non lisibles en runtime et leurs longs côtés
n'avaient pas assez de subdivisions. Deux maillages dédiés, lisibles et plus
subdivisés, comptent chacun **4 656 sommets et 9 312 triangles**.
Leur forme au repos est conservée. Une conversion réciproque des sommets et
de l'échelle FBX normalise leur taille : dérive maximale **0,00000152 u**.
Les normales lisses importées suivent la déformation par sa transformation
différentielle pour éviter les facettes dues aux triangles de tailles variées.

Les buffers sont créés à l'initialisation et réutilisés. Au repos, le composant
ne réécrit pas le maillage. Un nouvel impact repart de la forme courante, sans
retour brusque au repos. `Time.deltaTime` respecte la pause ; désactiver le
composant restaure le repos ; quitter Play détruit les maillages temporaires.
Une référence/config manquante produit un warning clair et désactive l'effet.

Le menu **Pinball → Neutral → Installer l'animation du caoutchouc** utilise
Undo, préserve une installation existante et ne sauvegarde jamais la scène.
Les assets dérivent des bandes de [la finition mécanique](NeutralMechanical.md),
sans nouveau téléchargement ni package.

## Validation

Les [recettes, rapports et captures](NeutralRubberValidation) sont archivés.

| Vérification | Résultat |
| --- | --- |
| Impacts PhysX gauche et droit | Un événement par impact initial, 100 points, rebond vers l'intérieur |
| Déformation, ancrages, repos, pause et impacts rapprochés | **26 contrôles réussis**, arcs fixes et transition continue |
| Frames normales de Play, sans appeler l'animation manuellement | Gauche : 81 frames, pic 0,07973 u ; droite : 86 frames, pic 0,08448 u ; retour à zéro |
| Assets et indépendance | Références intactes ; copies runtime indépendantes |
| Installation répétée | Composants, paramètres, maillages, poses et état de scène préservés |
| Scène sauvegardée | Colliders et poses identiques ; six cibles ; aucun script manquant ni maillage runtime résiduel |
| Compilation et Console finale | Aucune erreur et aucun avertissement |

Les essais contrôlés déclenchent de vrais contacts à 5 ms puis avancent la
présentation pour mesurer les phases. Les deux essais de frames normales
confirment séparément que `LateUpdate` déroule automatiquement l'effet après
un contact réel. Six captures montrent repos, compression et poussée des
deux côtés ; leurs caméras et textures temporaires sont détruites après usage.

Un premier test envoyé pendant le démarrage de Play a expiré avant exécution.
Le diagnostic CLI est archivé ; les essais ultérieurs et le contrôle final
terminent normalement. Cette passe ne mesure pas les performances WebGL et
n'implémente pas de physique de matériau souple : la déformation est visuelle,
le rebond garde la physique validée de Neutral.
