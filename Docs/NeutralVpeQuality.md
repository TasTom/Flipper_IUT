# Neutral — méthodes VPE adaptées à PhysX / URP

Date : 2 octobre 2026. Branche : `feature/neutral-vpe-quality`.

Neutral reçoit une finition PBR et une correction des effets lumineux des bumpers.
Les contrôles et les parcours physiques validés sont conservés. Cette passe applique
les méthodes de création pertinentes du [guide VPE](https://docs.visualpinball.org/creators-guide/introduction/overview.html)
au projet actuel, conformément au choix explicite de l'utilisateur. Elle n'installe
pas VPE et ne constitue pas une certification du jeu complet décrit par le GDD.

## Changements visibles

Les rampes transparentes avaient leurs reflets et leurs brillances désactivés.
Les matériaux de plusieurs pièces peintes ou plastiques avaient aussi une composante
métallique. Vingt matériaux propres à Neutral séparent maintenant métal, peinture,
plastique, caoutchouc et imprimés. Les matériaux source de la bibliothèque restent
disponibles ; les nouvelles affectations sont enregistrées dans Neutral.

Le plateau reçoit un vernis avec le shader URP Complex Lit, une carte de normale
de détail discrète et une variation de lissage. Les métaux emploient une réponse
métallique et des micro-normales de brossage ; les caoutchoucs ont une surface plus
mate. Les quatre cartes techniques générées sont linéaires, répétables et limitées
à 256 × 256 pixels avec mipmaps. L'illustration du plateau conserve sa source native.
Ce travail transpose les principes du [guide de matériaux d'assets](https://docs.visualpinball.org/creators-guide/editor/asset-library-styleguide.html)
et du [tutoriel de texturage du plateau](https://docs.visualpinball.org/creators-guide/tutorials/realistic-playfield/3-texturing.html).
Les cartes URP utilisent leurs propres canaux ; les masques HDRP ne sont pas copiés
dans des propriétés incompatibles.

Les huit motifs et bordures des slingshots sont désormais des surfaces imprimées
planes : ils ne forment plus de petits volumes flottants et ne projettent plus de
fausses ombres. Ils sont enfants du plastique qu'ils habillent, avec des UV dans
son plan. L'assemblage, les poteaux, vis et caoutchoucs restent distincts. Cette
adaptation reprend la séparation entre support et illustration du
[tutoriel des plastiques](https://docs.visualpinball.org/creators-guide/tutorials/realistic-plastics/index.html).

La sonde de réflexion capture maintenant les pièces visibles de la table, avec
projection dans son volume et une résolution de 512 pixels par face. Elle se
rafraîchit au démarrage, pas à chaque image. La lumière principale est moins jaune,
et ses biais d'ombre sont réduits. Le volume local utilise ACES, un bloom modéré
et aucune vignette : les coins de l'écran gardent leur lisibilité.

La caméra conserve la rotation de la borne et le panneau des scores au bord de
l'écran. Son intervalle de profondeur passe de 0,1–100 à 0,5–environ 47–49 selon
le format ; il couvre les objets de la table et le panneau situé à deux unités.
Les limites sont calculées depuis les renderers, puis mises en cache. Cela applique
le conseil de précision du [guide caméra](https://docs.visualpinball.org/creators-guide/editor/advanced/camera-settings.html)
sans couper le HUD.

## Défaut fonctionnel corrigé dans les effets

Les trois tableaux `Bumper.flashRenderers` pointaient vers les anciennes pièces
importées et halos dont les renderers étaient masqués. Les collisions et le score
fonctionnaient, mais les flashes atteignaient des objets invisibles.

Ils pointent maintenant vers le chapeau visible de chaque bumper. Le champ de
chapeau vide est également renseigné explicitement. Le médaillon, le cerclage et
le numéro restent ses enfants et suivent sa course. Les pièces métalliques fixes
ne reçoivent pas le flash du plastique.

URP 17.6 réévalue le mot-clé d'émission selon les indicateurs du matériau. Cinq
matériaux utilisés par les lumières de jeu gardent maintenant l'émission active
après cette validation, y compris ceux qui reçoivent une couleur temporaire via
`MaterialPropertyBlock`. Leur très faible valeur au repos est remplacée par les
états des scripts pendant le jeu. L'indicateur d'émission ne signifie pas qu'une
illumination globale dynamique a été ajoutée. Cette correction suit la séparation
entre mécanisme, animation et affichage expliquée dans les
[composants Unity de VPE](https://docs.visualpinball.org/creators-guide/editor/unity-components.html).

## Validation

Les recettes et résultats sont archivés dans [NeutralVpeQualityValidation](NeutralVpeQualityValidation).
Les essais utilisent l'éditeur ouvert et la physique réelle de la scène, à 5 ms.

| Contrôle | Résultat |
| --- | --- |
| Scène, shaders, émissions, imprimés, sonde et caméra | 77 contrôles réussis |
| Impacts des 3 bumpers, 2 slingshots et 6 cibles ; flashes sur les objets visibles | 25 contrôles réussis |
| Caoutchoucs : poussée centrale, ancrages, pause, réimpact et retour au repos | 26 contrôles réussis |
| Cibles, parcours complets des deux rampes, boucle et scores | 14 contrôles réussis |
| Relecture de la scène enregistrée dans une scène de prévisualisation indépendante | 11 contrôles réussis ; références résolues, scène de travail et Undo préservés |
| Empreinte physique | Les 56 colliders actifs, leurs propriétés, meshes et matrices sont identiques au relevé initial, aussi après Play |
| Affichage | Panneau des scores et profondeur des objets vérifiés en 1920 × 1080, 1920 × 1200 et 1080 × 1920 ; orientation de la borne conservée |
| Contrôle final | 0 erreur, 0 avertissement ; scène enregistrée, mode édition, `dirty=False` |

Les scores de parcours restent 7 500 pour Vosges et 10 000 pour IUT. Six cibles
de récompense, trois bumpers, deux slingshots et deux flippers sont conservés.
Le rubber est poussé au centre de sa grande face ; cette passe ne remplace pas
le mécanisme validé dans [NeutralRubberAnimation](NeutralRubberAnimation.md).

L'inventaire final compte 201 MeshRenderers actifs et 116 082 triangles. C'est un
inventaire, pas une mesure de FPS. Aucun build Windows/WebGL ni essai humain sur
la borne n'est certifié par ces contrôles.

## Traçabilité et limites

Le [GameDesignDocument](../GameDesignDocument.docx) reste la source de conception :
sections « Direction artistique », « Bumpers », « Slingshots », « Cibles fixes »,
« Architecture Unity » et « Performances WebGL ». Les six cibles et l'orientation
de la borne sont les adaptations explicitement demandées par l'utilisateur.

Les 60 pages du sommaire du guide ont été recensées. La
[matrice de couverture](NeutralVpeGuideCoverage.md) indique les méthodes adaptées,
les étapes propres au moteur VPE, les chapitres sans mécanisme correspondant dans
Neutral et les sujets seulement partiels. Consulter tous les chapitres ne signifie
pas importer les mécanismes d'une autre machine ni reprendre ses réglages physiques.
Les valeurs VPX ne sont pas des valeurs PhysX interchangeables ; voir le
[guide des matériaux physiques](https://docs.visualpinball.org/creators-guide/editor/materials.html).

Pour dépasser cette finition, les principaux sujets de qualité restants sont une
illustration de plateau réellement plus définie avec masques propres aux encres
et inserts, ainsi que la qualification sur la borne et en WebGL.
L'[audio/mixage et les UV](NeutralVpeAudio.md) ont depuis été améliorés et
mesurés dans un player Windows local. Les [nouveaux mécanismes](NeutralMechanisms.md)
complètent l’agencement sans constituer une certification de toute la table.
La source actuelle de 887 × 1 774 pixels ne devient pas plus détaillée en augmentant
uniquement sa taille d'import. Le GDD demande aussi des fonctions et parcours
encore ouverts, notamment les déverrouillages de missions, la porte IUT, le flipper
secondaire et les menus : voir [l'audit GDD](NeutralGddAudit.md) et ses mises à jour.

## Utilisation de l'outillage

`Pinball > Neutral > Installer la finition PBR VPE` installe la finition sur une
Neutral qui ne la possède pas encore. Les modifications de scène passent par Undo.
L'installation ne sauvegarde pas la scène ; l'enregistrement de cette passe a été
une action distincte après contrôle. La présence de `ProfessionalArt/PbrFinish`
empêche une seconde installation de remettre à zéro les réglages ajustés à la main.
Les assets sont dans `Assets/Generated/NeutralVpeFinish`.

Les pièces réutilisées gardent leurs attributions antérieures, notamment
[pinball-parts](https://github.com/vbousquet/pinball-parts) sous CC BY-SA. Aucun asset
d'une table commerciale ni fichier du guide VPE n'a été copié dans la scène.

## Captures vérifiées

[Avant](NeutralVpeQualityValidation/neutral-vpe-before.png) ·
[Après, au repos](NeutralVpeQualityValidation/neutral-vpe-after.png) ·
[En Play Mode](NeutralVpeQualityValidation/neutral-vpe-runtime.png) ·
[Assemblages du bas](NeutralVpeQualityValidation/vpe-lower-details.png) ·
[Rampes et retours](NeutralVpeQualityValidation/vpe-ramp-details.png).
