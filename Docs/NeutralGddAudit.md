# Neutral — audit des proportions et du GameDesignDocument

Date : 2 octobre 2026. Révision examinée : `8df1903`.

Source de conception : [GameDesignDocument.docx](../GameDesignDocument.docx). Les décisions explicites de l'utilisateur prévalent : **six cibles de récompense**, caméra tournée pour la borne, plateau et scores occupant l'écran.

Cet audit porte sur la scène ouverte `Assets/Scenes/Neutral.unity`, ses composants et leurs références, les scripts et les configurations présents. Il comprend une capture avec cadrage adapté à 1920 × 1080 et des mesures dans le repère du plateau. Aucune modification persistante des objets ou réglages de scène et aucun enregistrement de scène n'ont été effectués. La scène est restée non modifiée dans l'éditeur. La présence d'un système dans le code ne constitue pas une validation de son parcours complet en partie.

## Conclusion

**Neutral possède un socle jouable et une identité graphique, mais n'est pas encore le flipper terminé décrit dans le GDD.** La finition mécanique, la lisibilité des tirs et des récompenses, les règles de score avancées et le parcours utilisateur complet restent à terminer.

Il n'y a pas de surdimensionnement général des pièces importées. L'impression de masse vient surtout de l'emprise cumulée des rampes et guides, de leurs surfaces continues et de certains assemblages simplifiés. Réduire uniformément tous les objets modifierait des pièces déjà correctement proportionnées et leurs passages physiques.

## Dimensions constatées

L'ancre d'échelle est la bille : diamètre de 0,45 unité pour 27 mm, soit 60 mm par unité Unity. Les dimensions suivantes sont des équivalents physiques calculés depuis cette ancre.

| Élément | Mesure | Interprétation |
| --- | --- | --- |
| Bille | 27 mm | Correspond à la bille standard de 1 1/16 pouce, environ 27 mm. |
| Batte de chaque flipper | 80,0 mm dans l'axe du modèle | Pièce issue de la bibliothèque de pièces réelles ; cohérente avec un flipper standard de 3 pouces et son caoutchouc. |
| Chapeaux des bumpers, au repos | Environ 66 mm de diamètre | Le modèle source mesure environ 76,6 mm : ils ont déjà été réduits d'environ 14 %. Leur animation ajoute un gonflement temporaire. |
| Socle de bumper | Environ 55 mm de diamètre | Cohérent avec la pièce source. |
| Cibles | Environ 16 mm de largeur visuelle ; 31 mm de hauteur au-dessus du plateau | La hauteur totale du modèle inclut sa fixation sous le plateau. Elles ne sont pas globalement trop grandes. |
| Passage libre des rampes | Environ 40 mm | Environ 1,47 diamètre de bille. Le réduire pour l'esthétique affecterait la marge de circulation. |
| Tubes du retour en fil métallique | Environ 2,5 mm de diamètre | L'épaisseur seule n'explique pas leur présence visuelle ; leur trajet et leur recouvrement des éléments comptent davantage. |
| Surface de sol modélisée | Environ 514 × 1007 mm | Inclut le couloir du lanceur ; ce n'est pas la largeur libre de l'aire principale. |
| Aire principale entre les faces des parois, à trois hauteurs | 456 mm | Mesurée entre `LeftBoundary` à x = −4,15 et `ShooterDivider` à x = 3,45, aux positions z = 8, 10 et 12. |

Références : [bille standard Stern](https://shop.sternpinball.com/collections/spare-parts/products/515-7081-06-standard-pinball-1-1-16-pack-of-6), [batte standard de 3 pouces](https://www.pinballlife.com/flipper-bat-and-shaft-assemblies-no-logo.html), [bibliothèque pinball-parts](https://github.com/vbousquet/pinball-parts). Les dimensions de la scène et des modèles source ont été mesurées localement.

## Pourquoi la finition reste insuffisante

- **Rampes et guides :** les deux grandes rampes présentent des surfaces et rebords continus assez dominants. Les retours métalliques passent visuellement devant des bumpers. La séparation physique en hauteur ne garantit pas la lisibilité depuis la caméra fixe.
- **Assemblages mécaniques :** les caoutchoucs de slingshots, les guides, certains inserts et le boss gardent des formes simples. Il manque une présentation cohérente des poteaux, fixations, supports, caoutchoucs et pièces transparentes. Des éléments adaptés existent déjà dans `Assets/Models/Parts`.
- **Boss :** plusieurs pièces des anciens et nouveaux habillages restent visibles ensemble. Son écran est partiellement masqué par le passage supérieur et son assemblage paraît moins abouti que les bumpers importés.
- **Décor des bumpers :** les médaillons, cerclages et indices visibles sont sous `FlowRefinement`, tandis que `Bumper.WriteCapPose` anime uniquement le chapeau. Leurs transforms ne suivent donc pas cette animation. L'effet visuel exact doit être vérifié lors d'un impact en Play Mode avant correction.
- **Cibles :** la largeur de leurs colliders est d'environ 20 mm contre 16 mm pour la face visible. Cela mérite une vérification des tirs de bord : une collision peut se produire au-delà du visuel.
- **Lecture graphique :** les plaques noires et petits textes se superposent à un plateau très détaillé. Les récompenses, entrées de rampes et chemins utiles devraient se reconnaître plus rapidement.
- **Identité du GDD :** le paysage Vosges est identifiable. Le bâtiment IUT, le logo officiel et le grand `@` en 3D ne sont pas présents comme éléments reconnaissables dans cette scène. Les sapins en volume et la neige restent aussi à traiter selon le budget visuel.

Le GDD vise une 3D stylisée à semi-réaliste ; il n'impose pas le photoréalisme. Une finition professionnelle reste néanmoins nécessaire : assemblages crédibles, matériaux cohérents, chemins lisibles et retours visuels compréhensibles.

## État par rapport au cahier des charges

Les intitulés ci-dessous correspondent aux sections du GDD. « Présent » signifie identifié dans la scène et le code ; les validations de jeu et de livraison restantes sont indiquées séparément.

| Domaine du GDD | État constaté | Écart ou validation restante |
| --- | --- | --- |
| Physique, lanceur et contrôles | Présents : bille, lanceur chargé, deux flippers, clavier et borne, protection de vitesse et anti-blocage | Valider des parties complètes au pas physique réel et sur la borne. Les anciens tests de trajectoires ne suffisent pas à certifier tout le gameplay. |
| Bumpers et slingshots | Trois bumpers et deux slingshots, score, impulsions, effets et sons | Finition des assemblages ; vérifier les habillages pendant l'animation récente des bumpers. |
| Flipper secondaire | Absent de Neutral | Prévu par le GDD, à intégrer avec un tir utile et un contrôle clavier distinct. |
| Deux rampes et boucle | Présentes ; paires de portes d'entrée/sortie reliées dans l'éditeur | Lisibilité et finition. Le grand `@` et le déverrouillage de la boucle après Java sont absents. |
| Porte IUT | Aucun composant `Door` dans Neutral | Le script existe, mais la porte et son ouverture après la mission réseau ne sont pas intégrées. |
| Cibles et progression | Six cibles : trois Matières, une Café, une Java, une Projet ; états de progression gérés | Adaptation volontaire à la demande des six cibles. Le nombre initial du GDD n'est pas à restaurer automatiquement. |
| Partie et billes | Trois billes initiales, drain, remplacement, fin de partie et rejeu | Équilibrer la durée réelle des parties, visée de 3 à 8 minutes. |
| Score et record local | Score centralisé et record via `PlayerPrefs` | Combos absents ; pas de bonus de fin de bille ; multiplicateur partiel, sans progression complète jusqu'à ×5 ni affichage permanent. |
| Sept missions | Sept missions définies par défaut ; événements de cibles, rampes, boucle et boss exploités | Récompenses et déverrouillages incomplets : bonus combo Café, ouverture Java et porte Réseau. Tester la chaîne entière dans une partie. |
| Boss Projet final | États d'initialisation, trois impacts, correction par rampe/boucle puis compilation présents | Finition visuelle et validation du parcours réel jusqu'à sa défaite. |
| Nuit de l'Info et multiball | Annonce et création de deux billes supplémentaires prévues après le boss | Pas de mode complet avec jackpots, valeurs augmentées, musique/éclairage spécifiques et signal clair de fin lorsque seule une bille reste. |
| Tilt et difficultés | Tilt présent ; configurations Novice/Standard/Expert | Pas de sélecteur ni de mémorisation du choix. Les différences portent surtout sur vitesse maximale, tilt et extra balls ; plusieurs différences prévues par le GDD ne sont pas appliquées. |
| Extra balls | Seuils et limites configurables | Standard : première récompense à 30 000, maximum trois extras ; beaucoup plus généreux que les valeurs indicatives du GDD, qui suggère leur rareté et une limite de une ou deux. À équilibrer, pas à changer arbitrairement. |
| HUD, menus et résultats | Score, record, billes, mission et messages ; panneaux simples de pause et game over | Pas de menu principal, sélection de difficulté, menu de pause complet, statistiques finales ou crédits dans le parcours utilisateur. |
| Audio | Musique et plusieurs effets présents ; son de collision et impacts de bumpers | Retours dédiés aux combos, missions, tilt, boss et modes ; adaptation de la musique aux événements. |
| Configuration et architecture | Responsabilités réparties en plusieurs scripts ; `DifficultyConfig` existe | De nombreuses valeurs d'équilibrage restent dans les composants. Configurations score, missions, bille, flippers, audio et éléments de table encore à consolider. |
| Livraison Windows et WebGL | Non validée par cet audit | **Les Build Settings n'activent que `Assets/Scenes/Main.unity`, pas Neutral.** Corriger la scène livrée avant un build, puis tester les deux plateformes. |
| Performance, crédits et compte rendu | Des sources et anciennes validations sont documentées dans `Docs` | Pas de mesure récente des performances WebGL ni de vérification d'un compte rendu final couvrant l'état actuel. Vérifier les crédits des assets et contributions. |

## Priorités recommandées

1. **Achever les assemblages et la lisibilité de Neutral.** Garder l'échelle de la bille et des pièces réelles ; retravailler les rampes, guides, slingshots, supports et boss. Séparer les améliorations visuelles des marges physiques déjà utiles. Vérifier le décor animé des bumpers et les colliders de cibles.
2. **Terminer les éléments de table et l'identité IUT.** Flipper secondaire, porte IUT et boucle `@`, bâtiment/logo et habillage Vosges cohérent, sans masquer les tirs ni ajouter des colliders décoratifs.
3. **Achever les règles et l'interface.** Combos, bonus de fin de bille, progression et affichage du multiplicateur, jackpots/Nuit de l'Info ; menus, sélection de difficulté, résultats et retours audio.
4. **Équilibrer et livrer.** Tests de parties humaines, chaîne des missions, boss/multiball et tilt ; vérifier le cadrage de la borne, les scènes de build, Windows et WebGL, les performances et les crédits.

## Limites et preuves

La Console ne signalait aucune erreur lors du relevé ; des avertissements de dépréciation restent présents. Cela ne certifie ni un build ni l'absence de bugs en partie. Aucun nouveau build ou test complet de gameplay n'a été exécuté pendant cet audit.

Correction après vérification en Play Mode : le réglage lu hors partie était de 20 ms, mais `TableGravity.Awake` impose **5 ms au démarrage**. Les essais des trajectoires à 5 ms utilisent donc bien le pas physique de la partie. Leur pilotage contrôlé des flippers reste distinct d'une partie humaine sur la borne.

Après cet audit, la réduction de 8 % demandée par l'utilisateur a été appliquée et testée : voir [la passe de réduction](NeutralScale.md). Les mesures précédentes décrivent la révision auditée avant cette réduction.

Relevés reproductibles, stockés sous `Tools/unity/out/` :

- `gdd-audit-2026-10-02.txt` : extraction du document de conception avec indices de paragraphes.
- `gdd-scene-2026-10-02.txt` : composants, dimensions, matériaux, physique et scènes de build.
- `gdd-links-2026-10-02.txt` : références directes, liaisons des portes, textes et configurations.
- `gdd-dimensions-2026-10-02.txt` : mesure des faces de parois et des dimensions propres aux flippers.
- `neutral-audit-fit-2026-10-02.png` : capture de l'état au repos, caméra temporairement ajustée à la résolution de capture puis restaurée.

Les rapports historiques de `Docs/NeutralValidation` concernent leurs révisions respectives. Les notes d'avancement anciennes d'`AGENTS.md` ne décrivent pas toutes l'état actuel de Neutral ; elles ne doivent pas remplacer les relevés de scène.
