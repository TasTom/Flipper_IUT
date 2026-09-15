# Flipper_IUT — Project Guidelines

Unity 6000.6.0f1 + URP 17.6.0. Pinball 3D. Scripts C# dans `Assets/Scripts/`, scène `Assets/Scenes/Main.unity`.

## Table layout
`Assets/Editor/BuildPinballTable.cs` reconstruit toute la table (menu `Flipper > Rebuild Table Layout`).
Il repositionne les objets de gameplay au lieu de les recréer, donc scripts, colliders et pièces Krea survivent.

Conventions de coordonnées — les inverser a causé plusieurs bugs :
- `-Z` = **bas** de la table (apron, drain, flippers). `+Z` = haut (orbit, cibles).
- `-X` = gauche, `+X` = droite. **Le chenal du plongeur est à droite**, hors de l'aire de jeu.
- L'aire de jeu va de `-HalfX` à `+HalfX` (3.475). Le chenal va de `LaneInnerFace` (3.825) à `LaneOuterX`.
- `TableCenterX` (0.565) centre l'ensemble table+chenal ; ne pas utiliser l'axe du chenal comme centre.
- Les largeurs de chenal se mesurent **entre les faces des murs**, pas entre leurs centres (sinon la bille se coince).
- Un mur qui chevauche un autre crée un coin interne qui coince la bille et l'éjecte verticalement.

Pièces décoratives générées : `Assets/Models/Parts/Krea_*.glb` (voir `.github/skills/local-asset-pipeline/SKILL.md`).
Leurs matériaux sont réassignés par le script de build — trimesh n'écrit pas de matériaux, donc les GLB importent sans matériau.

## Environment (décor CC0)
`Assets/Editor/BuildArcadeEnvironment.cs` construit le monde autour de la table (menu `Flipper > Rebuild Arcade Environment`, appelé aussi à la fin de `Rebuild Table Layout`).
Tout est sous un root `Environment`, **sans aucun collider** : c'est du décor, un collider oublié casserait la physique validée de la table.
Voir `Assets/References/ASSET_CATALOG.md` pour le détail.

- Kenney Mini Arcade → `ArcadeFloor` (24 dalles), `ArcadeWalls` (31 panneaux), `ArcadeProps` (13 bornes + 2 personnages).
- Kenney City Kit Industrial → `CampusExterior` : 10 bâtiments + château d'eau, moulin, cheminées, conteneurs.
- Kenney Mini Forest → 26 arbres en anneau (98–150 unités) + rochers.
- Poly Haven `snowy_park_01_1k.hdr` → skybox `Skybox/Panoramic` + réflexions.

Pièges vérifiés :
- **Les GLB Kenney référencent `Textures/colormap.png` en fichier externe.** Copier seulement les `.glb` laisse tous les matériaux sans texture ; il faut l'atlas à côté des meshes puis forcer un réimport (`ImportAsset` + `ForceUpdate`) — un simple `Refresh` garde l'échec en cache.
- Les échelles Kenney (« 1 unité = 1 tuile ») sont bien trop grandes : `EnvScale` 6.5 / `CityScale` 30 / `ForestScale` 36 sont calées sur le frustum réel de la caméra, pas sur le monde réel.
- Le brouillard d'origine était un navy quasi noir `(0.05, 0.10, 0.14)` qui noyait tout au-delà de ~50 unités. Passé en brume hivernale `(0.80, 0.86, 0.94)`, densité 0.0022.
- `Snow015` est de la **neige sur herbe** : son albédo tire au vert. Réservé aux bandes étroites, le sol extérieur utilise une couleur unie.
- Le HUD était en boîtes de 420 px fixes → chevauchement au centre en 637×471. Il s'étale maintenant sur une moitié d'écran chacune avec 46 px de marge.

## Architecture
- `GameManager` (singleton) : score, billes, spawn, `PlayerPrefs` key `VosgesTilt_HighScore`.
- `MissionManager` (singleton) : 5 matières (`Programmation`, `Reseau`, `Web`, `BaseDeDonnees`, `Projet`), bonus 500, NuitDeLInfo +2000 + `MultiballManager.TriggerMultiball()`.
- Gameplay : `Flipper` (HingeJoint + JointSpring), `Plunger` (Space, OverlapSphere tag `Ball`), `ScoreTarget` / `SubjectTarget` (OnCollisionEnter tag `Ball`, AddForce impulsion), `DrainZone` (OnTriggerEnter → `LoseBall()`), `TableGravity` (Physics.gravity incliné).
- Le décor de table est visuel : seuls leurs `MeshRenderer` sont masqués quand une pièce Krea les remplace. Ne jamais désactiver le GameObject hôte ou ses scripts/colliders cessent de fonctionner.
- Tags requis : `Ball`. Input : `KeyCode` sérialisés (pas d'InputSystem pour flippers/plunger), `R` pour restart.

## Code Style
- C# Unity : `SerializeField private`, `[Header]`, PascalCase méthodes publiques, camelCase privés.
- Toujours null-check `Instance` et `missionText` / UI avant usage.
- `ForceMode.Impulse` pour billes, `direction.y` 0.2–0.25 pour rebonds.
- Pas de `Find`/`GetComponent` en `Update` ; cacher en `Awake`.

## Build and Test
- Ouvrir avec Unity 6000.6.0f1. Scène : `Assets/Scenes/Main.unity`.
- Tests : `Window > General > Test Runner` (EditMode). Package `com.unity.test-framework` présent.
- Recompile scripts via Unity ou MCP `recompile_scripts`.
- L'éditeur n'avance que ~2 frames quand la fenêtre n'a pas le focus : pour tester la physique via MCP, passer `Physics.simulationMode = Script` et appeler `Physics.Simulate(dt)` en boucle.
- URP **GPU Resident Drawer** est désactivé sur `Assets/Settings/PC_RPAsset.asset` : actif, il inonde la console d'erreurs `BatchDrawCommand` à chaque changement de matériau.

## MCP / Docs
- Serveur MCP Unity : `unityMCP` dans `.vscode/mcp.json` (HTTP `http://127.0.0.1:8080/mcp`, `type: http`). Nécessite package Unity `com.coplaydev.unity-mcp` + Editor ouvert + bridge HTTP démarré.
- Certains outils MCP sont désactivés côté client : `batch_execute` reste utilisable comme contournement.
- Docs Unity à jour via Context7 (`/websites/unity3d_manual`) : vérifier API `MonoBehaviour`, `HingeJoint`, `Rigidbody`, URP avant d'affirmer.
- Logs Unity via MCP `read_console`, hiérarchie via `find_gameobjects`.
