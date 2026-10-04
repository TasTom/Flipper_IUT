# Industries — Atelier des Vosges

Adaptation de `Assets/Scenes/Industries.unity`, à partir de la table VPE **Full Example Table** importée par l’utilisateur. Branche : `feature/industries-gameplay`. Unity 6000.6.0f1, URP 17.6.0, VPE 0.0.1-preview.204. Neutral conserve sa physique PhysX ; Industries utilise la physique native VPE.

## Passage à la seconde table

Le seuil demandé est **100 000 points**, réglable dans `Assets/Resources/Progression/ProgressionConfig.asset`. Seule Neutral déclenche cette progression. Le traitement attend la fin du contact et des abonnés au score, afin d’inclure les éventuels bonus et billes supplémentaires.

Le jeu se suspend, le carton « Atelier des Vosges » apparaît, Industries se charge derrière le voile, puis la partie reprend. L’orientation de la borne est conservée. Le score, les billes restantes et le multiplicateur sont transférés une seule fois. Le moteur VPE distribue une nouvelle bille physique pour poursuivre la même bille logique ; les objets PhysX de Neutral ne sont pas transportés. Si le seuil est atteint avec zéro bille restante, le compteur reste à zéro et la partie reste terminée.

`TableSessionTransfer` porte l’instantané ; `ScoreProgressionGate` surveille le score ; `SceneTransition` anime le changement de scène. Les pauses manuelles et pertes de bille sont ignorées pendant la transition. Les scènes Main, Neutral et Industries restent activées dans les Build Settings.

## Table et règles

La géométrie jouable du modèle de départ est conservée : flippers VPE, lanceur, rampes, guides, slingshots, cinq bumpers, spinner et rollovers. Les maillages manquants des bumpers et cibles sont fournis avec les pièces déjà présentes dans `Assets/Models/Parts`. Les colliders des cibles sont alignés sur leur position visible : la face de contact et le reste du boîtier sont distincts.

La récompense de production utilise **six cibles** : trois tombantes et trois fixes. Les indicateurs représentent les cibles déjà touchées ; terminer la série attribue le bonus puis relève les cibles tombantes. Les contacts répétés avec une cible fixe continuent de marquer, mais ne comptent pas comme une nouvelle cible pour compléter la série.

La correction des cibles reprend le gabarit de `sw1–sw3` pour les six visuels : largeur de 19 mm, sommet à 27 mm au-dessus du plateau et même matériau laiton. `sw11–sw13` conservent leurs composants de cibles fixes et reçoivent un collider correspondant au nouveau gabarit. Leur banque est placée en miroir de celle des cibles tombantes, autour de l'axe moyen des banques existantes. Les chiffres ne sont plus peints dans le plateau : chacun est centré à 18 mm devant sa cible, dans un voyant de 15 mm lié aux lampes natives `l1–l3` et `l11–l13`. Les numéros restent en place quand une cible tombe. Le recul, le rayon et la lumière se règlent dans `IndustriesConfig`. Le plateau d'origine reste conservé ; la variante sans numéros se régénère avec `python Docs/IndustriesValidation/art-source/generate_industries_art.py --without-target-numbers`.

`Flipper > Industries > Corriger les six cibles et leurs numéros` applique cette correction explicitement, avec Undo, sans enregistrer la scène. Les autres pièces restent en place. Voir les [captures et validations de cette correction](IndustriesTargetsValidation/README.md).

| Élément | Points de base |
| --- | ---: |
| Bumper | 100 |
| Cible | 500 |
| Rollover / inlane | 250 |
| Spinner / slingshot | 50 |
| Scoop | 1 000 |
| Série complète de six cibles | 5 000, sans multiplication supplémentaire |

Le scoop natif `Kicker1` capture et rééjecte la bille après 0,65 s. L’ouverture de démonstration, trop petite pour une bille de 27 mm, est portée à 32,4 mm ; la coupelle décorative mesure environ 45 mm et ne double pas le collider natif. Trois billes au démarrage d’une partie directe ; après transfert, Industries reprend le nombre réellement disponible. Les drains provoquent une nouvelle distribution jusqu’au Game Over. Entrée redémarre une partie.

Les valeurs et les huit sons mécaniques se règlent dans `Assets/Generated/Industries/IndustriesConfig.asset`. Le score passe toujours par `ScoreManager`. `IndustriesVpeGame` relie les événements VPE au cycle de partie existant, sans ajouter de Rigidbody PhysX à la bille. `IndustriesAudio` s’occupe des sons.

Contrôles : Q/A à gauche, D à droite, Espace maintenu puis relâché pour le lanceur, Échap pour la pause, Entrée pour une nouvelle partie. Les actions gauche, droite et lanceur de la borne xin-mo sont lues en parallèle du clavier. Le matériel xin-mo n’a pas été testé physiquement. Le flipper secondaire n’est pas présent dans cette table de départ.

## Présentation et méthode

Le [remodelage de l'atelier](IndustriesRemodel/README.md) remplace désormais l'apparence des plastiques, rampes et flippers du template : carters 3D chanfreinés avec bordures cuivre, portique industriel, cinq chapeaux de bumpers à manomètre, flippers assortis et nouvelle signalétique du plateau/apron. Les colliders et hôtes VPE restent séparés de ces visuels. `Flipper > Industries > Remodeler l'habillage atelier (Undo)` applique ce remplacement explicite une seule fois, sans sauvegarder la scène ; les relances préservent les réglages manuels. Les sources et captures avant/après sont archivées dans `Docs/IndustriesRemodel`.

Habillage original : plateau imprimé « Atelier des Vosges », motifs de transmission et de production, peinture pétrole, laiton, métal et ivoire. Les deux cartes sur l’apron indiquent les commandes et les règles. Les textures sont générées à leur résolution source par les scripts archivés dans `IndustriesValidation/art-source/` ; aucun éclairage n’est peint dans le plateau. La microtexture utilise une normal map séparée.

La caméra et le fronton reprennent désormais ceux de **Neutral** : projection orthographique, angle, rotation de borne à 90°, cadrage relatif et panneau des scores occupant 15 % de l'écran. `Backglass` a été copié depuis la scène source, avec ses polices, couleurs, positions, pastilles de billes, record, multiplicateur et messages. Le libellé de mission indique « CHAÎNE DE PRODUCTION », avec le compteur des six cibles d'Industries. L'ancien HUD d'Industries est conservé mais désactivé.

Le modèle `Pinball_Cabinet` de Neutral est aussi repris sous `Backglass` : ses pièces `Backbox`, `Screen` et `Marquee`, avec les maillages et matériaux d'origine, encadrent l'affichage. Elles suivent le rectangle du fronton lors des changements de format. Le fond noir est légèrement rentré dans le cadre ; les textes gardent leur placement. Les autres renderers du modèle restent masqués comme dans Neutral et les colliders décoratifs de la copie sont retirés. Capture : [cabinet de Neutral dans Industries](IndustriesValidation/neutral-cabinet/Industries.png).

La largeur extérieure des deux tables est désormais **9,02 unités Unity**, mesurée entre les bords extérieurs de leurs montants latéraux. La racine `ExampleTable` d'Industries reçoit une échelle **uniforme de 15,60554**, avec une translation qui centre la table sur X et aligne le bas du plateau à celui de Neutral. Le décor de fond suit cette échelle. Les positions relatives des pièces mécaniques, les dimensions VPX et les réglages de physique natifs restent inchangés : VPE continue de simuler dans le repère local du playfield.

`CabinetFraming` reprend maintenant le repère de Neutral : position zéro, rotation X de −7°, échelle unitaire. `CabinetViewport` utilise ce repère pour le cadrage et la géométrie native pour les plans de coupe. À 1920 × 1080, la caméra se trouve à environ `(0 ; 23,84 ; -16,60)`, avec une rotation `(40,22 ; 0 ; 90)` et une taille orthographique de `4,5`, comme Neutral. L'adaptation au format d'écran reste commune aux deux scènes. L'outillage de réparation mesure les pièces dans le repère local du playfield pour respecter l'échelle de la table.

Neutral reprend aussi le visuel du lanceur, le cache `Shooter` et la barre métallique `Ramp17` d'Industries. Ces copies réutilisent leurs matériaux ; leurs meshes sont archivés dans `Assets/Generated/SharedIndustriesHardware`. Le mécanisme PhysX et son collider de lanceur restent en place. Voir les [positions et validations](SharedMechanicsValidation/README.md).

Les anciennes pièces de présentation et les ombres peintes du modèle sont masquées par renderer ; leurs hôtes natifs restent actifs. `IndustriesPresentation` maintient cette visibilité aussi dans l'éditeur, car VPE peut réactiver ses renderers au rechargement.

Les choix suivent le [styleguide VPE](https://docs.visualpinball.org/creators-guide/editor/asset-library-styleguide.html) : dimensions réelles, pivots cohérents, collider indépendant du visuel, UV de plateau et matériaux PBR. Les [tutoriels](https://docs.visualpinball.org/creators-guide/tutorials/index.html), l’[installation](https://docs.visualpinball.org/creators-guide/setup/installing-vpe.html) et le [lancement VPE](https://docs.visualpinball.org/creators-guide/setup/running-vpe.html) servent de référence. URP est conservé avec l’adaptateur du projet.

`Flipper > Industries > Adapter la table importée` fournit l’outillage d’éditeur : Undo, création des éléments manquants et arrêt si l’adaptation existe déjà. `Réparer les liaisons de l’adaptation` rétablit les références manquantes. Ces commandes ne sauvegardent pas la scène automatiquement. Le décor ne reçoit pas de colliders.

## Lien avec le GDD

| Source de vérité | Application |
| --- | --- |
| GDD §Contrôles | InputRouter et commandes de borne |
| GDD §Déroulement d’une partie / §Billes | Trois billes, lancement, drains et Game Over |
| GDD §Score / §Interface | Gestionnaire partagé, multiplicateur, record et HUD |
| GDD §Architecture Unity | Responsabilités séparées, configuration ScriptableObject |
| Demande utilisateur complémentaire | Seconde table VPE et transition à 100 000 points |
| Choix utilisateur antérieur | Six cibles de récompense et orientation de borne |

Cette livraison ne prétend pas implémenter tout le GDD sur Industries : les cinq groupes de matières, les missions, le boss, le multiball et les difficultés restent des étapes distinctes. Le ball lock et le magnet ajoutés précédemment dans Neutral ne sont pas reproduits artificiellement ici.

## Validation

**Fonctionnelle avec limites documentées.** Les tests d’intégration exécutent réellement la physique VPE dans l’éditeur ; ils ne constituent pas une suite exhaustive ni une certification de comportement sur borne.

- [16 contrôles de partie](IndustriesValidation/runtime.txt) : initialisation, commandes des deux flippers, lancement hors du chenal, pause, six contacts physiques de cibles, bonus de série, capture/éjection du scoop, trois drains et nouvelle partie.
- [7 contrôles de transition](IndustriesValidation/transition.txt) : aucune bascule à 99 999, déclenchement à 100 000, voile persistant, reprise du score/billes/multiplicateur, distribution VPE, consommation unique et absence de boucle depuis Industries. Le compteur attendu est relevé après les paliers de récompense de Neutral, puis comparé à l’arrivée.
- [14 comparaisons de présentation](IndustriesValidation/shared-presentation/comparison.txt) : même angle, pose normalisée, zoom et coins du fronton à 1920 × 1080, 2560 × 1440 et 1920 × 1200 ; mêmes styles et rectangles pour les onze textes du fronton. La transition a été rejouée avec cet affichage. Captures après harmonisation des largeurs : [Industries](SharedMechanicsValidation/Industries.png), [Neutral](SharedMechanicsValidation/Neutral.png). La référence contient aussi le HUD blanc historique de Neutral : ce second affichage superposé n'est pas dupliqué dans Industries.
- Audit après rechargement : 0 script manquant, 6 cibles natives, 1 caméra ; huit sons assignés. Les sondes de caméra et le script d'adaptation sont archivés dans [shared-presentation](IndustriesValidation/shared-presentation/README.md).
- Build Windows64 Development Mono, D3D11/D3D12, scènes Neutral + Industries : [rapport](IndustriesValidation/windows-build.txt). Unity indique `Succeeded`, avec des diagnostics de packages détaillés dans ce rapport. L'exécutable est dans `Tools/build/IndustriesValidation/VosgesMania.exe` ; il précède la modification de caméra/fronton, validée dans l'éditeur. Cette compilation ne prouve pas une partie complète dans l'exécutable.

L’erreur d’import `AutodeskInteractive.shadergraph` de URP était déjà présente avant ces modifications. Le build conserve également des avertissements de shaders des packages, et six maillages de collision de Neutral sans précalcul d’import. Aucun budget de performance sur la borne, trajectoire exhaustive depuis les flippers, test du contrôleur physique ou partie longue en Player n’est revendiqué.

Les premiers résultats conservés dans `IndustriesValidation/` expliquent deux erreurs de protocole : compteur attendu avant l’attribution des billes bonus de Neutral, puis scénario de partie neuve lancé sur une session transférée avec sept billes et ×2. Les tests finaux emploient les conditions appropriées ; aucun de ces écarts n’a été masqué en désactivant une règle de jeu.

## Origine des assets

- Table de départ et mécanismes : [VisualPinball.Engine](https://github.com/freezy/VisualPinball.Engine), table exemple importée par l’utilisateur ; ressources nécessaires enregistrées dans `Assets/Tables/ExampleTable` avec leurs `.meta`.
- Bumpers et cibles : modèles existants exportés de [vbousquet/pinball-parts](https://github.com/vbousquet/pinball-parts), Vincent Bousquet, CC BY-SA. Les maillages dérivés sont mis à l’échelle métrique pour VPE et associés aux matériaux de cette adaptation. Voir la [licence archivée](NeutralValidation/2026-09-26/pinball-parts-LICENSE.md).
- Plateau, cartes, cabinet et matériaux nouveaux : création pour Vosges Mania. Le moteur VPE conserve sa licence et ses notices de package.
