# Neutral — couverture du guide VPE

Sommaire officiel relevé le 2 octobre 2026 : **60 pages, 60 accessibles**.
Cette matrice indique l'applicabilité et le traitement dans le projet actuel,
pas une certification de conformité intégrale à VPE. « Adapté » désigne une
méthode transposée à PhysX / URP, parfois déjà présente et validée dans les passes
précédentes. « Partiel » signale explicitement les différences restantes.
« Sans objet » n'est pas une fonctionnalité manquante du design de Neutral.
Voir le [compte rendu de finition](NeutralVpeQuality.md) pour les changements
effectués et leurs limites. Le [GDD](../GameDesignDocument.docx) reste la source
de vérité ; le guide technique ne le remplace pas.

| Chapitre officiel | Traitement | État dans Neutral |
| --- | --- | --- |
| [Overview](https://docs.visualpinball.org/creators-guide/introduction/overview.html) | Cadre | Création originale conservée ; adaptation sans migration. |
| [Features](https://docs.visualpinball.org/creators-guide/introduction/features.html) | Cadre | Les fonctions du moteur ne sont pas installées. |
| [Installing VPE](https://docs.visualpinball.org/creators-guide/setup/installing-vpe.html) | Hors moteur actuel | PhysX / URP conservés selon le choix utilisateur. |
| [Running VPE](https://docs.visualpinball.org/creators-guide/setup/running-vpe.html) | Hors moteur actuel | Import VPX et commandes VPE sans équivalent requis ici. |
| [Units and 3D Space](https://docs.visualpinball.org/creators-guide/editor/units-3d-space.html) | Adapté | Échelle mesurée ; bille réduite volontairement ; pas de conversion aveugle. |
| [Unity Components](https://docs.visualpinball.org/creators-guide/editor/unity-components.html) | Adapté | Collision, rendu et animation distincts ; références des bumpers corrigées. |
| [Wire Rails](https://docs.visualpinball.org/creators-guide/editor/wire-rails/index.html) | Adapté | Trajets, supports et canaux existants validés ; finition du métal et des liners. |
| [The Route](https://docs.visualpinball.org/creators-guide/editor/wire-rails/route.html) | Adapté | Trajets, supports et canaux existants validés ; finition du métal et des liners. |
| [Wire Layouts](https://docs.visualpinball.org/creators-guide/editor/wire-rails/layouts.html) | Adapté | Trajets, supports et canaux existants validés ; finition du métal et des liners. |
| [Fixtures](https://docs.visualpinball.org/creators-guide/editor/wire-rails/fixtures.html) | Adapté | Trajets, supports et canaux existants validés ; finition du métal et des liners. |
| [Generated Geometry](https://docs.visualpinball.org/creators-guide/editor/wire-rails/geometry.html) | Adapté | Trajets, supports et canaux existants validés ; finition du métal et des liners. |
| [Materials](https://docs.visualpinball.org/creators-guide/editor/materials.html) | Adapté | Matériaux de rendu séparés de la physique ; physique validée conservée. |
| [Asset Library Style Guide](https://docs.visualpinball.org/creators-guide/editor/asset-library-styleguide.html) | Partiel | PBR et imprimés améliorés ; densité native du plateau reste limitée. |
| [Switch Manager](https://docs.visualpinball.org/creators-guide/editor/switch-manager.html) | Hors moteur actuel | Détection par collisions, triggers et événements C# existants. |
| [Coil Manager](https://docs.visualpinball.org/creators-guide/editor/coil-manager.html) | Hors moteur actuel | Actionneurs pilotés par les composants PhysX existants. |
| [Lamp Manager](https://docs.visualpinball.org/creators-guide/editor/lamp-manager.html) | Adapté | États lumineux via blocs de propriétés ; émissions et destinataires vérifiés. |
| [Wire Manager](https://docs.visualpinball.org/creators-guide/editor/wire-manager.html) | Hors moteur actuel | InputRouter et événements directs ; aucune ROM émulée. |
| [Working with Multiple Tables](https://docs.visualpinball.org/creators-guide/editor/multiple-tables.html) | Sans objet | Une seule table jouée dans Neutral. |
| [Camera Settings](https://docs.visualpinball.org/creators-guide/editor/advanced/camera-settings.html) | Appliqué | Profondeur resserrée et vérifiée à trois résolutions. |
| [Tutorials](https://docs.visualpinball.org/creators-guide/tutorials/index.html) | Index | Les quatre familles de tutoriels sont distinguées ci-dessous. |
| [Create a Realistic Looking Playfield](https://docs.visualpinball.org/creators-guide/tutorials/realistic-playfield/index.html) | Adapté | Plateau original existant ; finition PBR ajoutée. |
| [Albedo Texture and Masks](https://docs.visualpinball.org/creators-guide/tutorials/realistic-playfield/1-textures.html) | Partiel | Illustration conservée ; masques spécifiques aux encres et inserts à approfondir. |
| [Create the Playfield Mesh](https://docs.visualpinball.org/creators-guide/tutorials/realistic-playfield/2-modeling.html) | Adapté | Géométrie existante mesurée et parcours validés ; aucun nouveau trou physique. |
| [Texturing](https://docs.visualpinball.org/creators-guide/tutorials/realistic-playfield/3-texturing.html) | Adapté | Vernis et micro-normales dans URP Complex Lit. |
| [Texturing (details)](https://docs.visualpinball.org/creators-guide/tutorials/realistic-playfield/3b-more-texturing.html) | Partiel | Variation de surface discrète ; pas de patine peinte par zone. |
| [Import Into Unity](https://docs.visualpinball.org/creators-guide/tutorials/realistic-playfield/4-import-into-unity.html) | Adapté | Matériaux locaux URP ; imports techniques linéaires avec mipmaps. |
| [Create Realistic Looking Plastics](https://docs.visualpinball.org/creators-guide/tutorials/realistic-plastics/index.html) | Adapté | Support plastique séparé de son illustration plane. |
| [Prepare Artwork](https://docs.visualpinball.org/creators-guide/tutorials/realistic-plastics/1-prepare-artwork.html) | Adapté | Motifs géométriques partagés ; aucun atlas raster nécessaire pour ces imprimés. |
| [Create Mesh](https://docs.visualpinball.org/creators-guide/tutorials/realistic-plastics/2-create-mesh.html) | Adapté | Pièces existantes conservées ; volumes des imprimés supprimés. |
| [UV-Map Mesh](https://docs.visualpinball.org/creators-guide/tutorials/realistic-plastics/3-uv-map-mesh.html) | Adapté | UV planaires des imprimés alignés sur le plastique. |
| [Import Into Unity](https://docs.visualpinball.org/creators-guide/tutorials/realistic-plastics/4-import-into-unity.html) | Adapté | Meshes imprimés locaux, matériau partagé et hiérarchie du support. |
| [Create a Backglass](https://docs.visualpinball.org/creators-guide/tutorials/realistic-backglass/index.html) | Sans objet | Fronton translucide de machine EM non demandé ; panneau des scores de borne conservé. |
| [Finding Artwork](https://docs.visualpinball.org/creators-guide/tutorials/realistic-backglass/1-prepare-artwork.html) | Sans objet | Fronton translucide de machine EM non demandé ; panneau des scores de borne conservé. |
| [Create a Backglass Mesh](https://docs.visualpinball.org/creators-guide/tutorials/realistic-backglass/2-create-mesh.html) | Sans objet | Fronton translucide de machine EM non demandé ; panneau des scores de borne conservé. |
| [Import Into Unity](https://docs.visualpinball.org/creators-guide/tutorials/realistic-backglass/3-import-into-unity.html) | Sans objet | Fronton translucide de machine EM non demandé ; panneau des scores de borne conservé. |
| [Make a 3D Scan Game-Ready](https://docs.visualpinball.org/creators-guide/tutorials/make-a-3d-scan-game-ready/index.html) | Sans objet | Aucun nouveau scan introduit ; pièces réelles et géométrie existante utilisées. |
| [Clean Up](https://docs.visualpinball.org/creators-guide/tutorials/make-a-3d-scan-game-ready/1-clean-up.html) | Sans objet | Aucun nouveau scan introduit ; pièces réelles et géométrie existante utilisées. |
| [Mesh Retopology](https://docs.visualpinball.org/creators-guide/tutorials/make-a-3d-scan-game-ready/2-mesh-retopology.html) | Sans objet | Aucun nouveau scan introduit ; pièces réelles et géométrie existante utilisées. |
| [Texture Baking](https://docs.visualpinball.org/creators-guide/tutorials/make-a-3d-scan-game-ready/3-bake-texture-maps.html) | Sans objet | Aucun nouveau scan introduit ; pièces réelles et géométrie existante utilisées. |
| [Manual](https://docs.visualpinball.org/creators-guide/manual/manual.html) | Index | Documentation des mécanismes ; leur présence dépend du design de Neutral. |
| [Gamelogic Engine](https://docs.visualpinball.org/creators-guide/manual/gamelogic-engine.html) | Adapté | GameManager, ScoreManager et missions restent séparés des mécanismes. |
| [Displays](https://docs.visualpinball.org/creators-guide/manual/displays.html) | Adapté | HUD numérique existant ; scores et affichage de borne vérifiés. |
| [Sound](https://docs.visualpinball.org/creators-guide/manual/sound.html) | Partiel | Audio existant ; mixage, variations et annonces à qualifier séparément. |
| [Nudging](https://docs.visualpinball.org/creators-guide/manual/nudging.html) | Partiel | Tilt existant conservé ; modèle inertiel VPE non porté dans PhysX. |
| [Troughs / Ball Drains](https://docs.visualpinball.org/creators-guide/manual/mechanisms/troughs.html) | Adapté | Cycle drain / réserve / lanceur géré par BallManager ; pas de ROM. |
| [Flipper](https://docs.visualpinball.org/creators-guide/manual/mechanisms/flippers.html) | Partiel | Flippers PhysX conservés ; profils de couple VPE non transposés. |
| [Plungers](https://docs.visualpinball.org/creators-guide/manual/mechanisms/plungers.html) | Adapté | Lanceur chargé à l’appui, relâché pour lancer ; mécanisme existant. |
| [Slingshots](https://docs.visualpinball.org/creators-guide/manual/mechanisms/slingshots.html) | Adapté | Poussée au centre de la grande face et ancrages fixes validés. |
| [Magnets and Turntables](https://docs.visualpinball.org/creators-guide/manual/mechanisms/magnets.html) | Sans objet | Ce mécanisme n’existe pas dans Neutral ; ajout non requis par cette finition. |
| [Spring Hinge Bash Toys](https://docs.visualpinball.org/creators-guide/manual/mechanisms/spring-hinges.html) | Sans objet | Ce mécanisme n’existe pas dans Neutral ; ajout non requis par cette finition. |
| [Light Groups](https://docs.visualpinball.org/creators-guide/manual/mechanisms/light-groups.html) | Adapté | GI existante et états lumineux ; pas de gestionnaire VPE ajouté. |
| [Teleporters](https://docs.visualpinball.org/creators-guide/manual/mechanisms/teleporters.html) | Sans objet | Ce mécanisme n’existe pas dans Neutral ; ajout non requis par cette finition. |
| [Drop Target Banks](https://docs.visualpinball.org/creators-guide/manual/mechanisms/drop-target-banks.html) | Sans objet | Les six cibles de récompense sont des cibles fixes, pas une banque escamotable. |
| [Motion Controllers](https://docs.visualpinball.org/creators-guide/manual/mechanisms/motion-controllers.html) | Sans objet | Ce mécanisme n’existe pas dans Neutral ; ajout non requis par cette finition. |
| [Rotators](https://docs.visualpinball.org/creators-guide/manual/mechanisms/rotators.html) | Sans objet | Ce mécanisme n’existe pas dans Neutral ; ajout non requis par cette finition. |
| [Score Reel Displays](https://docs.visualpinball.org/creators-guide/manual/mechanisms/score-reels.html) | Sans objet | Ce mécanisme n’existe pas dans Neutral ; ajout non requis par cette finition. |
| [Score Motors](https://docs.visualpinball.org/creators-guide/manual/mechanisms/score-motors.html) | Sans objet | Ce mécanisme n’existe pas dans Neutral ; ajout non requis par cette finition. |
| [Collision Switches](https://docs.visualpinball.org/creators-guide/manual/mechanisms/collision-switches.html) | Adapté | Impacts et parcours signalés par les composants C# ; tests de contacts réels. |
| [Tilt Bobs](https://docs.visualpinball.org/creators-guide/manual/mechanisms/tilt-bobs.html) | Partiel | Tilt logiciel existant ; simulation de pendule et capteur physique non ajoutés. |
| [Lifting Gates](https://docs.visualpinball.org/creators-guide/manual/mechanisms/lifting-gates.html) | Sans objet | Ce mécanisme n’existe pas dans Neutral ; ajout non requis par cette finition. |
