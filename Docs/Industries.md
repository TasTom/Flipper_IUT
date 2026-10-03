# Industries — Atelier des Vosges

Adaptation de `Assets/Scenes/Industries.unity`, à partir de la table VPE **Full Example Table** importée par l’utilisateur. Branche : `feature/industries-gameplay`. Unity 6000.6.0f1, URP 17.6.0, VPE 0.0.1-preview.204. Neutral conserve sa physique PhysX ; Industries utilise la physique native VPE.

## Passage à la seconde table

Le seuil demandé est **100 000 points**, réglable dans `Assets/Resources/Progression/ProgressionConfig.asset`. Seule Neutral déclenche cette progression. Le traitement attend la fin du contact et des abonnés au score, afin d’inclure les éventuels bonus et billes supplémentaires.

Le jeu se suspend, le carton « Atelier des Vosges » apparaît, Industries se charge derrière le voile, puis la partie reprend. L’orientation de la borne est conservée. Le score, les billes restantes et le multiplicateur sont transférés une seule fois. Le moteur VPE distribue une nouvelle bille physique pour poursuivre la même bille logique ; les objets PhysX de Neutral ne sont pas transportés. Si le seuil est atteint avec zéro bille restante, le compteur reste à zéro et la partie reste terminée.

`TableSessionTransfer` porte l’instantané ; `ScoreProgressionGate` surveille le score ; `SceneTransition` anime le changement de scène. Les pauses manuelles et pertes de bille sont ignorées pendant la transition. Les scènes Main, Neutral et Industries restent activées dans les Build Settings.

## Table et règles

La géométrie jouable du modèle de départ est conservée : flippers VPE, lanceur, rampes, guides, slingshots, cinq bumpers, spinner et rollovers. Les maillages manquants des bumpers et cibles sont fournis avec les pièces déjà présentes dans `Assets/Models/Parts`. Les colliders des cibles sont alignés sur leur position visible : la face de contact et le reste du boîtier sont distincts.

La récompense de production utilise **six cibles** : trois tombantes et trois fixes. Les indicateurs représentent les cibles déjà touchées ; terminer la série attribue le bonus puis relève les cibles tombantes. Les contacts répétés avec une cible fixe continuent de marquer, mais ne comptent pas comme une nouvelle cible pour compléter la série.

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

Habillage original : plateau imprimé « Atelier des Vosges », motifs de transmission et de production, peinture pétrole, laiton, métal et ivoire. Les deux cartes sur l’apron indiquent les commandes et les règles. Les textures sont générées à leur résolution source par les scripts archivés dans `IndustriesValidation/art-source/` ; aucun éclairage n’est peint dans le plateau. La microtexture utilise une normal map séparée.

La caméra de borne, le HUD et les cartes ont été inspectés en Game View à 1920 × 1080. La table occupe le cadre et le panneau supérieur reste réservé au score, aux billes et à la production. Les anciennes pièces de présentation et les ombres peintes du modèle sont masquées par renderer ; leurs hôtes natifs restent actifs.

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
- [7 contrôles de transition](IndustriesValidation/transition.txt) : aucune bascule à 99 999, déclenchement à 100 000, voile persistant, reprise du score/billes/multiplicateur, distribution VPE, consommation unique et absence de boucle depuis Industries. La session test conserve sept billes : trois initiales, une accordée par le scénario, trois accordées par les paliers déjà actifs dans Neutral.
- Audit après rechargement : 0 script manquant, 6 cibles natives, 1 caméra ; huit sons assignés. Capture : [Industries](IndustriesValidation/industries-final.png), [transition](IndustriesValidation/transition.png).
- Build Windows64 Development Mono, D3D11/D3D12, scènes Neutral + Industries : [rapport](IndustriesValidation/windows-build.txt). Unity indique `Succeeded`, avec des diagnostics de packages détaillés dans ce rapport. L’exécutable est dans `Tools/build/IndustriesValidation/VosgesMania.exe` ; cette compilation ne prouve pas une partie complète dans l’exécutable.

L’erreur d’import `AutodeskInteractive.shadergraph` de URP était déjà présente avant ces modifications. Le build conserve également des avertissements de shaders des packages, et six maillages de collision de Neutral sans précalcul d’import. Aucun budget de performance sur la borne, trajectoire exhaustive depuis les flippers, test du contrôleur physique ou partie longue en Player n’est revendiqué.

Les premiers résultats conservés dans `IndustriesValidation/` expliquent deux erreurs de protocole : compteur attendu avant l’attribution des billes bonus de Neutral, puis scénario de partie neuve lancé sur une session transférée avec sept billes et ×2. Les tests finaux emploient les conditions appropriées ; aucun de ces écarts n’a été masqué en désactivant une règle de jeu.

## Origine des assets

- Table de départ et mécanismes : [VisualPinball.Engine](https://github.com/freezy/VisualPinball.Engine), table exemple importée par l’utilisateur ; ressources nécessaires enregistrées dans `Assets/Tables/ExampleTable` avec leurs `.meta`.
- Bumpers et cibles : modèles existants exportés de [vbousquet/pinball-parts](https://github.com/vbousquet/pinball-parts), Vincent Bousquet, CC BY-SA. Les maillages dérivés sont mis à l’échelle métrique pour VPE et associés aux matériaux de cette adaptation. Voir la [licence archivée](NeutralValidation/2026-09-26/pinball-parts-LICENSE.md).
- Plateau, cartes, cabinet et matériaux nouveaux : création pour Vosges Mania. Le moteur VPE conserve sa licence et ses notices de package.
