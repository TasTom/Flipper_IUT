# Pièces et largeur communes

Demande utilisateur : reprendre dans Neutral le lanceur d'Industries, son cache `Shooter` et `Ramp17`, et donner à Industries la même largeur de table que Neutral. Référence GDD : bille et lanceur, interface, présentation de la table.

## Neutral

- `PinballTable/Gameplay/Plunger/Plunger_IndustriesVisual` : meshes Rod et Spring du lanceur VPE, en pose de repos, attachés au lanceur PhysX existant. La pointe est alignée sur le collider actuel. Les anciens renderers Rod/Spring sont masqués ; leur collider et les réglages de lancement sont conservés.
- `PinballTable/ReferencePlayfield/IndustriesHardware/Shooter` : cache fixe repris de `ExampleTable/Playfield/Primitives/Shooter`. Son bord avant est à Z=1,918, derrière la bille au repos pour la laisser visible. Il reste fixe pendant la charge.
- `PinballTable/ReferencePlayfield/IndustriesHardware/Ramp17` : mesh et matériau du cache métallique inférieur d'Industries. Largeur d'environ 8,74, profondeur de 0,5, centre Z=0,35 et surface à Y=0,56, au-dessus de l'apron existant à Y=0,55. Il n'empiète pas sur les flippers.

Aucun collider ni composant de gameplay VPE n'est ajouté dans Neutral. Les copies contiennent uniquement la géométrie et les renderers. Le mouvement provient du lanceur existant ; les animations procédurales VPE ne sont pas importées dans le moteur PhysX. Les meshes dérivés sont enregistrés par Unity dans `Assets/Generated/SharedIndustriesHardware` ; les matériaux d'origine d'Industries sont réutilisés.

## Industries

La largeur extérieure de Neutral, calculée sur `LeftCabinet` et `RightCabinet`, est 9,02 unités. Celle du modèle VPE, calculée sur `LeftRail` et `RightRail` avant adaptation, est 0,578 m. Une échelle uniforme de 15,60554 à la racine `ExampleTable` donne la même largeur extérieure de 9,02. Les pièces mécaniques gardent leurs proportions et leurs positions locales. VPE recalcule en unités mondiales la hauteur de cinq lampes décoratives attachées à une surface ; cette différence de sérialisation est détaillée dans l’audit. La physique VPE utilise son repère local ; les paramètres VPX, la gravité, les forces, le rayon de bille et les colliders ne sont pas redimensionnés séparément.

Le repère de caméra reprend celui de Neutral, avec échelle 1, position zéro et rotation X=−7°. À 1920×1080, les deux caméras ont la même position `(0 ; 23,84 ; −16,60)`, rotation `(40,22 ; 0 ; 90)` et taille orthographique 4,5. Les géométries des tables restent différentes en longueur et en layout. Le fronton de scores conserve son cadrage.

`AdaptIndustriesTable` mesure désormais les limites de présentation et la coupelle du scoop dans le repère local du playfield. Une réparation des liaisons ne doit pas prendre l'échelle de présentation pour une modification des dimensions natives.

## Validation et sources

Les [16 contrôles de partie VPE](industries-runtime.txt) passent après la mise à l'échelle. Le [test de Neutral](neutral-launch.txt) vérifie une charge complète, la course de 0,8, le cache fixe et un lancement réel jusqu'à Z≈17,4. Les [7 contrôles de transition](transition.txt) passent également. Les [13 contrôles des scènes rechargées](saved-scenes.txt) confirment les largeurs, les positions, l’absence de scripts manquants et la conservation des références. Les [14 comparaisons caméra/fronton](camera-score-comparison.txt) passent sur trois formats d’écran. Les captures [Neutral](Neutral.png), [lanceur chargé](Neutral-charged.png) et [Industries](Industries.png) montrent les scènes en Play Mode. L’[audit des différences](scene-diff-audit.txt) vérifie que les paramètres existants de physique et les maillages natifs d’Industries n’ont pas changé.

Les meshes et matériaux proviennent de la table exemple VPE déjà importée dans Industries. Les références originales et les scripts d'auteur sont archivés ici ; les notices de licence du projet sont conservées. Les scripts d'adaptation utilisent Undo, ne sauvegardent pas les scènes et s'arrêtent si les pièces existent déjà. L'enregistrement est une opération distincte.

Ces contrôles ne constituent pas une partie longue ni une validation du contrôleur physique xin-mo. L'exécutable Windows antérieur n'intègre pas cette adaptation.

Compilation C# réussie. La Console conserve l’erreur d’import antérieure `AutodeskInteractiveTransparent.shadergraph` du package URP ; aucune autre erreur n’apparaît dans les entrées consultées après ces contrôles.
