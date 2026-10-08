# Industries — inscriptions et ambiance lumineuse, 8 octobre 2026

Branche `feature/industries-lighting`. Cette intervention répond à la demande d'habillage de Carter_Wall30, 31, 33 et 34 et d'effets de flipper sur le plateau. Elle prolonge le GDD : univers et direction artistique, ambiance chaude/froide, indications lumineuses des cibles et des rampes, rôle des FX et optimisation des lumières temps réel.

Les quatre carters portent maintenant VOSGES, MANIA, TEXTILE et BOIS, en ivoire sur émail pétrole avec accents cuivre/turquoise. Chaque impression dispose de sa propre texture et de ses UV locaux : l'ancien atlas projeté sur toute la table découpait les lettres. Les sommets, normales et triangles des panneaux restent identiques. Le placement du texte évite les rails et les maquettes aussi bien en vue verticale que depuis la caméra de la borne. Les anciens petits textes BOIS et TEXTILE sont masqués uniquement par renderer via IndustriesPresentation.

49 éléments associent un insert et un halo doux sur le plateau : 16 éclairages généraux, 5 anneaux de bumper, 6 inserts de cible, 3 flèches de rampe, un anneau de livraison, 2 indications de slingshot et 16 LED de bord. Les contacts VPE produisent des flashes avec extinction progressive ; les cibles chargées restent éclairées, la rampe indique la transformation à effectuer et la livraison produit une impulsion générale. L'ambiance respire lentement, et les animations se figent en pause. Ces effets ne calculent aucun score et n'ajoutent aucun collider.

Quatre lumières Point sans ombres éclairent les zones de jeu. Les 36 anciennes sources Unity de LightComponent, initialement directionnelles, sont désactivées ; leurs rendus et voyants VPE restent actifs. Les deux lumières d'atelier existantes sont conservées. Un profil URP dédié ajoute bloom HDR, tonemapping ACES et une vignette légère, sans modification du pipeline ou ajout de package.

- [Vue supérieure avant](before.png) et [après](after.png).
- [Détail des quatre carters](carters.png), [caméra de la borne](game-camera.png).
- [15 contrôles d'édition](editor.txt), dont géométrie conservée, références, colliders, lumières, post-traitement et idempotence.
- [31 contrôles en Play](runtime.txt), dont contacts physiques/FX, pause, abonnements, lancement, six cibles, Ramp1 à plusieurs vitesses, plafond, livraison, drains et nouvelle partie.
- [Données physiques natives et règles inchangées pendant l'installation](gameplay-preserved.txt).
- [Contrôle du placement des lettres](lettering.txt).

La compilation réussit sans erreur. La console conserve l'erreur d'import URP préexistante `AutodeskInteractiveTransparent.shadergraph`, les avertissements de câblage VPE et ceux de lecture physique des outils de validation ; aucun nouvel avertissement du composant lumineux ni exception de celui-ci n'a été observé. Les tests physiques ont été repris après la pause déclenchée par cette erreur d'import. Aucun budget de performance mesuré sur la borne n'est revendiqué : l'ajout représente 98 renderers simples et quatre lumières locales, aucune ombre supplémentaire.

## Reproduire et régler

Dans Industries hors Play : `Flipper > Industries > Refaire les carters et ajouter les lumières arcade`. L'installation crée uniquement son habillage, enregistre les changements de scène dans Undo et laisse la scène non enregistrée. Une relance conserve le résultat et les ajustements existants. **Ctrl+S dans Unity est nécessaire pour conserver l'application dans Industries.unity.**

`Assets/Generated/Industries/Lighting/IndustriesLightingConfig.asset` règle les émissions de repos, d'activation et de flash, leur durée, les halos et la respiration. Les couleurs individuelles et les références sont sur `IndustriesArcadeLighting`. Les valeurs de bloom/vignette du config servent à créer le profil ; les réglages ultérieurs se font dans `IndustriesArcadeVolume.asset`. Les intensités des deux lumières d'atelier et les positions restent ajustables dans la scène.

Les textures originales sont générées par [generate_art.py](source/generate_art.py), avec Pillow et Bahnschrift sous Windows. [panels.json](source/panels.json) contient les contours mesurés dans Unity ; les deux masques d'occlusion représentent la projection des rails/maquettes dans la pose actuelle, avec une marge autour des contours. Après un déplacement important de ces pièces ou de la caméra, il faut réévaluer le placement du texte. `python Docs/IndustriesLighting/source/generate_art.py` régénère les cinq PNG sans modifier la scène. Les attributions des mécanismes et maquettes restent dans [Industries.md](../Industries.md).
