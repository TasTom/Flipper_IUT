# Neutral — animation du caoutchouc des slingshots

Date : 2 octobre 2026. Branche : `feature/slingshot-rubber-animation`.

Les deux bandes se déforment lorsqu'une bille déclenche leur slingshot :
un poussoir fixe agit **au milieu de la face la plus longue**, vers l'aire de
jeu, puis revient au repos avec une légère oscillation amortie. Deux portions
tendues relient ce centre aux poteaux ; le contact du poussoir est arrondi.
Les trois arcs autour des poteaux restent fixes.

La première version suivait à tort le point d'impact. Cette correction remplace
ce comportement : un impact près d'une extrémité déclenche la même poussée
centrale qu'un impact au milieu, avec la même course de bobine.

Correspondance : [GameDesignDocument](../GameDesignDocument.docx), **§Slingshots**
(renvoi de la bille, dynamisme, points) et **§Objectif de stabilité**.
Référence mécanique : [documentation officielle VPE](https://docs.visualpinball.org/creators-guide/manual/mechanisms/slingshots.html),
qui décrit le bras fixe entre les deux contacteurs, actionné par une bobine,
et l'animation entre repos et activation. Le [manuel Stern James Bond 007 Pro,
§5.11, p. 42](https://sternpinball.com/wp-content/uploads/2022/12/JamesBond007_Pro_web.pdf)
montre également l'assemblage bras/poussoir, ses deux contacteurs et son ressort.
La courbe et les amplitudes sont des choix de présentation
du projet, sans prétention de mesurer l'élasticité d'un caoutchouc réel.
Aucun code de VPE n'est importé.

## Intégration et réglages

`Slingshot.Kicked` émet le point de contact et la vitesse relative après un
impact accepté. `SlingshotRubberAnimation` écoute cet événement comme un
déclenchement de bobine ; la position et la vitesse du contact ne déplacent
pas le poussoir et ne changent pas sa course. Le composant identifie la plus
grande distance entre les trois poteaux et déforme une copie privée du maillage
visible, sans changer le collider ni l'impulsion.
Les références sont câblées dans Neutral ; aucune installation manuelle n'est
nécessaire pour jouer.

L'asset `Assets/Generated/NeutralRubberAnimation/NeutralSlingshotRubberConfig.asset`
regroupe les réglages : **180 ms**, course **0,10 u** (6 mm), demi-largeur de
l'arrondi du poussoir **0,055 u** (3,3 mm), ancrages et courbe. La poussée commence
vers l'extérieur ; seule la fin du retour comporte un faible dépassement amorti.
`kickerHalfWidth` remplace `impactSpread` avec `FormerlySerializedAs` ; le réglage
de Neutral a été corrigé via l'éditeur avec Undo. La dépendance de l'ancienne
course à la vitesse d'impact a été supprimée.

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

Les [recettes, rapports et captures de la correction](NeutralRubberCentralKickerValidation)
sont archivés. Les [rapports initiaux](NeutralRubberValidation) restent conservés
comme historique ; ils décrivent l'ancienne déformation au contact.

| Vérification | Résultat |
| --- | --- |
| Contacts PhysX à 25 %, 50 % et 75 % de chaque face, à 12/20/30 u/s | **44 contrôles réussis** ; contact réellement déplacé, forme de poussée identique |
| Face active et course | Grande face de 1,06960 u ; maximum à 49,95 % ; course 0,10000 u des deux côtés |
| Déformation, rebonds, ancrages, repos, pause et impacts rapprochés | **26 contrôles réussis**, transition continue et 100 points par impact |
| Frames normales de Play, sans appeler l'animation manuellement | Vérifiées à gauche et à droite ; poussée automatique puis retour à zéro |
| Assets et indépendance | Références intactes ; copies runtime indépendantes |
| Scène sauvegardée | Colliders et poses identiques ; six cibles ; aucun script manquant ni maillage runtime résiduel |
| Compilation et Console finale | Aucune erreur et aucun avertissement |

Les essais contrôlés déclenchent de vrais contacts à 5 ms puis avancent la
présentation pour mesurer les phases. Les deux essais de frames normales
confirment séparément que `LateUpdate` déroule automatiquement l'effet après
un contact réel. Six captures montrent repos, début de poussée et course maximale des
deux côtés ; leurs caméras et textures temporaires sont détruites après usage.

Cette passe ne mesure pas les performances WebGL et
n'implémente pas de physique de matériau souple : la déformation est visuelle,
le rebond garde la physique validée de Neutral.
