# Flipper_IUT — Vosges Mania

Flipper 3D arcade (Unity 6000.6.0f1 + URP 17.6.0). La référence de conception est
`GameDesignDocument.docx` à la racine : **c'est la source de vérité**. Toute fonctionnalité
implémentée doit pouvoir être reliée à une section du GDD.

## Répartition du travail

| Qui | Quoi |
| --- | --- |
| **L'utilisateur** | Construit la scène dans l'éditeur Unity : layout de la table, placement des pièces, colliders, câblage des références dans l'Inspector. |
| **Codex** | Écrit tous les scripts C# (`Assets/Scripts/`), les ScriptableObjects de config, les scripts d'éditeur, et pilote l'éditeur via le CLI Unity pour vérifier/outiller. |

Conséquence pratique : **les scripts ne doivent jamais supposer une scène qu'ils construisent
eux-mêmes.** Ils se lient par nom/type/tag avec repli sur champ `[SerializeField]`, et échouent
proprement (warning clair, pas de NullReferenceException) quand une pièce n'existe pas encore.

### Deux décisions cadrant le travail

**1. Construction de scène : hybride et non destructive.** Un script d'éditeur génère la
structure (pivots, colliders, positions) ; l'utilisateur ajuste ensuite à la main. Le script
**ne recrée que ce qui manque et ne touche jamais aux réglages existants**. Tout objet portant
déjà le nom attendu est laissé intact — composants, colliders et valeurs sérialisées compris.
Seuls les champs restés *vides* peuvent être remplis. Chaque création passe par
`Undo.RegisterCreatedObjectUndo` : Ctrl+Z annule tout. Aucun script n'enregistre la scène
lui-même.

**2. Ordre : MVP jouable d'abord.** Objectif immédiat — une bille réellement jouable :
2 flippers, 2 slingshots, 3 bumpers, drain, 3 billes, score simple, caméra fixe, game over,
contrôles clavier. Le multiball, le boss, le décor final et la sauvegarde viennent après.

## Contrôles (GDD §Contrôles)

Table extraite du GDD, appliquée par `InputRouter` — **source unique** des entrées :

| Action | Touche GDD |
| --- | --- |
| Flipper gauche | `Q` ou `A` |
| Flipper droit | `D` |
| Flipper secondaire | `E` ou `R` |
| Lanceur | `Espace` (maintenir pour charger, relâcher pour lancer) |
| Secouer à gauche / à droite | *« touche dédiée à définir »* |
| Pause | `Échap` |
| Valider | `Entrée` |

⚠️ Le GDD signale lui-même que **les flippers et le tilt se disputent `A` et `D`** :
« Pour éviter un conflit entre les flippers et le tilt, il est préférable de retenir une seconde
configuration claire. » `InputRouter` fournit donc deux dispositions, échangeables par la case
`alternateLayout` sans recompiler :

- **principale** (fidèle au GDD) : flippers `Q`/`A`/`D`, tilt sur les **flèches** ← → ;
- **alternative** : flippers sur les **flèches**, tilt sur `A`/`D`.

Le routeur vérifie le conflit au démarrage et avertit si une même touche sert aux deux.

### Borne physique (xin-mo) — nouvel Input System

La table se joue aussi sur une **borne physique** : l'asset d'actions `PinballControls`
(map `GamePlayPF`) pilote le bornier *xin-mo Xinmotek*. Ses quatre actions sont
`LeftFlipper` (`/trigger`), `RightFlipper` (`/button5`), `LaunchBall` (`/button4`) et
`Quit` (`/button2`).

`Flipper` lit les **trois sources en parallèle** et s'arrête à la première qui presse :

| Ordre | Source | Quand |
| --- | --- | --- |
| 1 | `PinballControls.GamePlayPF.LeftFlipper` / `RightFlipper` | case `useCabinetController` cochée (défaut) |
| 2 | `InputRouter` | routeur présent dans la scène |
| 3 | `activationKey` (KeyCode) | repli pour une scène sans routeur |

Elles **se cumulent** au lieu de s'exclure : la borne n'a aucune liaison clavier dans son
asset, donc s'en remettre à elle seule rendrait le jeu injouable sans matériel. C'est ce qui
permet de mettre au point au clavier sur un poste où la borne n'est pas branchée.

Le **flipper secondaire n'existe pas sur la borne** — l'asset ne déclare pas d'action
`UpperFlipper`. `FlipperSide.Upper` reste donc clavier, et ne retombe volontairement sur
**aucune** action voisine : un appui sur la borne ne doit pas faire sauter un flipper que le
joueur n'a pas demandé.

`ProjectSettings.asset` est en `activeInputHandler: 2` (**Both**) : `Input.GetKey` et le nouvel
Input System fonctionnent ensemble, c'est ce qui permet aux deux sources de cohabiter.

La lecture se fait par `IsPressed()` au pas physique, **jamais** par un abonnement à
`performed` : un rappel manqué pendant un pic de charge laisserait le flipper baissé alors que
le joueur appuie encore.

⚠️ **L'asset source `Assets/PinballControls.inputactions` n'existe pas.** Seul le fichier
généré `Assets/Scripts/PinballControls.cs` est là — et il **n'est pas suivi par Git**. Il
compile seul (le JSON des actions est embarqué dans le `.cs`), mais deux conséquences :

- les liaisons ne sont **pas éditables** dans la fenêtre *Input Actions* ;
- `Flipper.cs` en dépend désormais : **si ce fichier disparaît, le projet tombe en Safe Mode.**

Pour rétablir une situation saine : **supprimer d'abord** `Assets/Scripts/PinballControls.cs`
(et son `.meta`), **puis** créer `Assets/Scripts/PinballControls.inputactions`. Unity régénère
le `.cs` au même chemin, le nom de classe ne change pas, `Flipper.cs` reste valide. Faire
l'inverse — créer l'asset en laissant le `.cs` orphelin — produit **deux fois la même classe
partielle** et une erreur de compilation.

## Contrat de scène

Interface entre le travail de l'utilisateur et les scripts. Les noms ci-dessous sont **normatifs** :
les scripts les cherchent par ce nom exact (puis, en repli, par composant).

### Racines

```
PinballTable/
  Gameplay/          tous les hôtes de gameplay (colliders + scripts)
  Table/             la table importée : Pinball_Table (colliders) + Pinball_Cabinet (décor)
  Furniture/         décor de la table (généré par BuildPinballTable)
  Rails/             inlanes, orbit, rampe wireform
  TableLights/       éclairage de table
  Environment/       décor CC0 (généré par BuildArcadeEnvironment)
Managers/            GameManager, ScoreManager, MissionManager, AudioManager, ...
UI/                  Canvas + HUD
```

`Table/` ne figure pas dans le contrat d'origine : il a été ajouté parce que la table est
désormais un **asset importé** et non plus de la géométrie procédurale. Les deux modèles y sont
séparés par usage — `Pinball_Table` reçoit les `MeshCollider` que la bille touche,
`Pinball_Cabinet` n'en reçoit **aucun** (c'est du décor).

### Hôtes de gameplay

| Nom attendu | Composant attendu | Notes |
| --- | --- | --- |
| `Flipper_Left_Pivot` / `Flipper_Right_Pivot` | `Flipper` + `HingeJoint` + `Rigidbody` | pivot ; l'enfant `Flipper_Bat` porte le collider |
| `Flipper_Upper_Pivot` | `Flipper` (secondaire) | optionnel, GDD §Flipper secondaire |
| `Slingshot_Left` / `Slingshot_Right` | `Slingshot` + collider | |
| `Bumper_01` `Bumper_02` `Bumper_03` | `Bumper` | |
| `Target_Programmation`, `Target_Reseau`, `Target_Web`, `Target_BDD`, `Target_Projet` | `DropTarget` | noms imposés par `BuildPinballTable` — voir la divergence ci-dessous |
| `Ramp_Vosges_In` / `Ramp_Vosges_Out` | `RampGate` | rampe gauche |
| `Ramp_IUT_In` / `Ramp_IUT_Out` | `RampGate` | rampe droite |
| `Loop_At_Entry` / `Loop_At_Exit` | `LoopGate` | loop @ |
| `Door_IUT` | `Door` | porte IUT |
| `Boss_ProjetFinal` | `BossTarget` | boss |
| `Plunger` | `Plunger` | |
| `BallSpawnPoint` | — | Transform seul ; porte le point de pose de la bille |
| `Ball` | — (le prefab `Ball.prefab`) | **bille posée dans la scène**, réutilisée par `BallManager` — voir « La bille est posée » ; frère de `BallSpawnPoint`, jamais son enfant |
| `DrainZone` | `DrainZone` | trigger, tag `Ball` |

### Groupes de cibles — divergence GDD / code à trancher

| Source | Groupes |
| --- | --- |
| **GDD** §Cibles fixes et §Missions | `Matières` (4) · `Café` (3) · `Java` (3) · `Réseau` (3) · `Projet` (3) — **16 cibles** |
| **Code existant** (`MissionManager`, `BuildPinballTable`) | `Programmation` · `Reseau` · `Web` · `BaseDeDonnees` · `Projet` — 5 cibles |

Le GDD étant la source de vérité, c'est le découpage du GDD qu'il faut viser. Mais le code
actuel est bâti sur l'autre : `MissionManager` a ces 5 matières en dur, et `BuildPinballTable`
place 5 cibles. **Trancher avant l'étape 3 (missions)** — reprendre les 5 groupes du GDD
demande de retoucher `MissionManager` et la banque de cibles du script de build.

En attendant, `SubjectTarget` et `MissionManager` restent tels quels : ils fonctionnent, ils ne
sont simplement pas conformes au GDD.

### Tags et layers

Tags déjà présents : `Ball`, `Flipper`, `Pinball`, `GameController`.
Layers : ajouter `Ball`, `Table`, `TableElement`, `Environment` (le GDD §Tags et layers le demande).

## Outillage

### 0. Prérequis — les assets sont en Git LFS

**864 fichiers** sont suivis par Git LFS : tout le binaire d'asset (`*.fbx`, `*.glb`, `*.gltf`,
`*.png`, `*.jpg`, `*.hdr`, `*.tga`, `*.wav`, `*.dll`…). Sans hydratation, ils arrivent sur le
disque sous forme de **pointeurs texte de 131 octets** et Unity ne peut pas les importer.

C'est la cause n°1 d'un projet « qui s'ouvre mal ». Symptômes :

- `ImportFBX Errors: Couldn't read file .../Plunger.fbx. None of the registered readers can
  process the file`
- `Failed to import Assets/Models/Kenney/.../*.glb (see inspector for details)` (GLTFast)
- Objets absents ou roses ; ouverture très longue, éditeur qui semble figé.

Diagnostic puis correction :

```powershell
git lfs ls-files | Select-String '^\w+ - '   # '-' = pointeur, '*' = vrai binaire
Remove-Item ".git\lfs\tmp\*" -Recurse -Force # purge les tmp d'un pull interrompu
git lfs install --local
git lfs pull
```

Un `.fbx` valide pèse des centaines de Ko. **131 octets = pointeur LFS.**

⚠️ Après le pull, l'import ne se déclenche pas tout seul si la fenêtre Unity n'a pas le focus.
Forcer via `eval_file` : `AssetDatabase.ImportAsset(path, ForceUpdate | ForceSynchronousImport)`
sur chaque asset. Compter ensuite les échecs réels —
`AssetDatabase.LoadMainAssetAtPath(p) == null` — plutôt que se fier à la console :

| Type | Attendu | Échecs |
| --- | --- | --- |
| FBX / OBJ | 726 | 0 |
| GLB / GLTF | 96 | 0 |
| Textures | 101 | 0 |

### 1. Unity CLI (Pipeline) — outil principal

Pilote l'éditeur Unity **ouvert** via le package `com.unity.pipeline` 0.6.0-exp.1.
C'est la voie privilégiée : les modifications s'appliquent à la scène active réelle.

```bash
unity status --format json          # état : {"state":"ready","port":7800,...}
unity command --format json         # 149 commandes disponibles
unity command <nom> <arg1> <arg2>   # ⚠️ arguments POSITIONNELS, pas du JSON
```

**Pièges vérifiés :**

- Les arguments sont **positionnels** : `unity command eval 'return 1+1;'`.
  Passer `'{"code":"..."}'` fait compiler le JSON comme du C# → `; expected`.
- `eval_file` exige un chemin absolu vers un fichier **`.cs`**.
- `eval` casse dès que le code contient des guillemets internes ou des `+` non nommés :
  `Invalid arguments for eval: --timeout expects Int32 but got ok;`. **Passer par `eval_file`**
  pour tout ce qui dépasse une ligne triviale.
- ⚠️ `eval` et `eval_file` **expirent au bout de 5 s** sur le thread principal, même pour
  `return 1+1;`. Ce n'est pas un plantage : le titre de la fenêtre Unity dit ce qu'il fait.
  Guetter sa disparition au lieu de conclure à un blocage :
  ```powershell
  (Get-Process -Id <pid>).MainWindowTitle   # "Importing (iteration 2) - Compress 50% (busy for 04:48)..."
  ```
  Un réimport de ~900 modèles prend plusieurs minutes et enchaîne deux passes.
- Si `unity status` ne répond pas, l'éditeur est probablement en **Safe Mode** (erreur de
  compilation) : `unity pipeline list` le confirme. Corriger le C# puis redémarrer Unity.
- Ne **jamais** éditer `.unity` / `.prefab` / `.asset` à la main quand l'éditeur est joignable.

**Commandes utiles :**

| Besoin | Commande |
| --- | --- |
| Exécuter du C# arbitraire | `unity command eval '<code>'` |
| Exécuter un fichier C# | `unity command eval_file D:/.../probe.cs` |
| Lire la hiérarchie | `unity command get_scene_hierarchy --format json` |
| Erreurs console | `unity command console --format json` |
| Compteurs console | `unity command console_status --format json` |
| Créer / modifier un GameObject | `create_gameobject`, `set_transform`, `add_component`, `set_component_properties` |
| Créer / attacher un script | `create_script`, puis `recompile`, puis `attach_script` |
| Lire/écrire un champ sérialisé | `get_serialized_fields`, `set_serialized_field` |
| Recompiler | `unity command recompile` (puis `recompile_status`) |
| Play / Stop | `unity command editor_play` / `editor_stop` |
| Capture d'écran | `unity command capture_game_view` |
| Batch transactionnel | `unity command batch` (max 200 ops, `$0.instanceId` pour référencer) |

`eval` et `eval_file` sont le levier le plus puissant : ils compilent le C# via Roslyn et
partagent la capacité d'exécution de code du projet. `run_script` compile un fichier du projet
en mémoire (sans domain reload) et exécute un point d'entrée statique — la voie « builder pattern »
pour construire en masse.

### 2. UnityMCP — secondaire

`.vscode/mcp.json` → `unityMCP`, HTTP `http://127.0.0.1:8080/mcp`. Exige le package
`com.coplaydev.unity-mcp` + éditeur ouvert + bridge démarré (`Window > MCP for Unity`).
**Le CLI Unity le remplace** ; ne l'utiliser que si le bridge est explicitement démarré.

### 3. Blender — assets 3D

Blender 5.2 : `C:/Program Files/Blender Foundation/Blender 5.2/blender.exe`

Headless (script Python) :

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --python Tools\blender\script.py
```

Usages : export/optimisation de GLB, décimation, génération procédurale de pièces
(rampes, guides de lane, sapins low-poly), vérification d'échelle.

#### Le plateau : `Tools/blender/build_table.py`

Génère la table elle-même — le dépôt de pièces n'en contient pas. Tout y est écrit en
**dimensions réelles**, donc vérifiable, par opposition à une génération d'image vers maillage
(Modly) qui ne donne aucune cote et sort 0,2 à 1 M de triangles pour un objet de précision.

```powershell
& "C:\...\blender.exe" --background --factory-startup `
  --python Tools\blender\build_table.py -- --verify
```

Écrit `Assets/Models/Table/Pinball_Table.fbx` et `Pinball_Cabinet.fbx`, et affiche en fin de
course les **positions des hôtes de gameplay** en unités Unity : c'est ce que recopie
`PlaceImportedTable.cs`, et ce qui doit être relu après toute modification de géométrie.

Quatre règles de conception y sont appliquées, apprises du coin qui bloquait la bille :

1. Deux murs ne se chevauchent jamais — leur recouvrement crée un coin interne qui coince.
2. Aucun cul-de-sac en V : tout coin concave est rempli d'un congé.
3. Aucun passage large d'environ une bille : un intervalle est soit plus étroit que son diamètre
   (elle n'y entre pas), soit au moins 1,3 fois plus large (elle y circule et ressort).
4. Tout mur qui retient la bille dépasse son rayon (0,0135) ; en deçà, elle roule par-dessus.

La géométrie est construite **à plat** ; l'inclinaison de 7° est posée dans Unity sur le root
`PinballTable` (voir `TableGravity.cs`). Aucun solide n'est supposé bien orienté :
`bmesh.ops.recalc_face_normals` recalcule chaque maillage et son volume signé est reporté.

#### Sonde d'axes : `Tools/blender/probe_axis.py`

Établit par lecture des octets du FBX ce que le couple `axis_forward` / `axis_up` fait réellement
— c'est-à-dire rien aux sommets. Voir le piège du miroir dans « État d'avancement ».

#### Bibliothèque de pièces `vbousquet/pinball-parts`

Source des assets de la table (clonée dans `Tools/pinball-parts`, licence CC BY-SA). Le dépôt
ne contient **que des `.blend`** — aucun FBX/GLB/OBJ — d'où les deux scripts ci-dessous.

```powershell
# 1. Inventorier : ouvre chaque .blend et relève pièces, polygones, dimensions réelles
& "C:\...\blender.exe" --background --factory-startup `
  --python Tools\blender\inventory_parts.py -- --repo Tools\pinball-parts `
  --out Tools\pinball-parts-inventory.json

# 2. Exporter un FBX par pièce dans Assets/Models/Parts/<Catégorie>/, avec vérification
& "C:\...\blender.exe" --background --factory-startup `
  --python Tools\blender\export_parts.py -- --only Flipper,Bumpers --verify
```

**Ancre d'échelle : la bille.** Les pièces du dépôt sont à l'échelle réelle ; la table Unity
utilise un rayon de 0,225 (`BuildPinballTable.BallRadius`), soit 0,45 de diamètre pour 27 mm
réels → **16,667 unités Unity par mètre**. Le dépôt confirme l'ancre par ses propres données :
sa pièce `Ball` (`Miscellaneous`) mesure 27,0 mm et sort à 0,450 × 0,450 × 0,450 dans Unity.

Trois pièges vérifiés :

- **Mettre une hiérarchie à l'échelle : une seule écriture, l'échelle de la racine.** Les
  descendants en héritent — taille *et* décalage. Toucher **en plus** à leur `location`
  applique `k` deux fois (une fois par héritage, une fois à la main), soit `k²`. Le piège est
  sournois parce que la plupart des pièces du dépôt sont **plates** — un maillage sans enfant,
  `Flippers.blend` comptant 58 objets pour 58 racines — et que sur celles-là les deux méthodes
  donnent le même résultat. Ce sont les pièces à hiérarchie qui tranchent : mesuré sur
  `Plunger`, annoncé 0,39×2,76×0,39 et réimporté 159×251×3,7.
  ⚠️ **Ces derniers chiffres ne sont pas les dimensions de la pièce** — re-mesure faite à la pose
  du lanceur dans `Neutral.unity` : le FBX importé donne **0,3917 × 0,3917 × 2,7522 u**, soit
  23,5 × 23,5 × 165,1 mm, contre `size_m = [0,0235, 0,16531, 0,0235]` à l'inventaire du dépôt —
  **accord à 0,1 %**. `159×251×3,7` n'a d'ailleurs pas les proportions de la pièce (159/251 = 0,63
  là où 23,5/165,1 = 0,14) : c'est la **signature** d'une mise à l'échelle en `k²`, pas un
  relevé. La leçon de la puce tient — une pièce à hiérarchie distingue les deux méthodes — mais
  ses chiffres ne se lisent pas comme une taille.
- **Une pièce est son propre maillage.** `inventory_parts.py` ne parcourait que les `children`
  de la racine : une pièce plate était invisible à son propre inventaire, et `Flippers.blend`
  annonçait « 0 exportable » sur 58 pièces. La racine compte.
- `export_scene.fbx` a bougé entre Blender 4.x et 5.x : les paramètres sont filtrés à l'appel
  sur ceux que l'installation connaît réellement, plutôt que supposés.

**Filtre de collections.** Le dépôt range ses rebuts dans `Trash` et ses gabarits de référence
dans `WIP` (repères de 5 et 15 mm, pas des pièces). `export_parts.py` les écarte par défaut
(`--skip-collections Trash,WIP`) et **nomme** ce qu'il écarte. Symétriquement, un nom demandé
qui ne correspond à rien est signalé : un nom recopié d'un affichage tronqué à 40 caractères
ne correspond à rien, et l'export le taisait.

`--verify` réimporte chaque FBX dans un fichier vide et mesure sa boîte englobante : l'échelle
est constatée, pas supposée. Mais cela ne teste que l'aller-retour **Blender** — la conversion
d'unités de Unity ne se vérifie que dans Unity, en mesurant les `Renderer.bounds` du modèle
importé. Fait : les 19 pièces sortent à la taille attendue (bille 0,450 au millième) avec
leurs matériaux. Le `useFileScale = 0,01` de l'importeur Unity (cm → m) est la signature
normale d'un FBX Blender ; il laisse une échelle de racine à ≈ 1666 sur le prefab importé, ce
qui est correct mais **ne se réinitialise pas** : poser le modèle tel quel, sans « Reset » sur
son transform.

#### Assemblage de la table depuis le dépôt : `Tools/blender/assemble_parts.py`

`build_table.py` engendre le plateau, les murs, l'orbit et les slingshots — ce que le dépôt
**ne contient pas**. Tout le reste (bumpers, cibles, poteaux) y est modélisé à l'échelle réelle,
et c'est ce que ce script pose à la place de la version engendrée. Le partage est explicite dans
`KEPT_SECTIONS` :

| Origine | Objets |
| --- | --- |
| engendré | `Playfield`, `Walls`, `Orbit`, `Slingshots` — 12 maillages |
| dépôt | `Bumpers` (socle + anneau + chapeau) × 3, `Drop Target` × 16, `Post - Metal - 1"1/4 - Round` × 4 — 29 maillages |

```powershell
& "C:\...\blender.exe" --background --factory-startup `
  --python Tools\blender\assemble_parts.py -- --verify
```

Écrit le **même** `Assets/Models/Table/Pinball_Table.fbx` que `build_table.py`, dont il prend le
relais pour le lot « table ». Le `Pinball_Cabinet.fbx` reste à `build_table.py`.

Écrire dans le même FBX est délibéré : le menu 4 pose un `MeshCollider` sur **chaque** maillage
de `Pinball_Table`, donc les pièces du dépôt deviennent physiques sans intervention. Un second
FBX n'aurait pas eu ce traitement.

**Les flippers ne sont pas dans ce FBX**, et ce n'est pas un oubli : ils tournent. Ce sont des
`Rigidbody` + `HingeJoint` pilotés par `Flipper.cs`, pas des colliders statiques — les figer
dans le maillage de la table les rendrait inanimés. Ils restent une pièce à part, posée dans
Unity sous `Flipper_Bat`.

⚠️ **Piège mesuré : lire les coordonnées du dépôt comme des coordonnées de table est une
identité de *nombres*, pas d'orientation.** Un `.blend` a son axe vertical en `z`, la table en
`y`. Sans correction, chaque pièce sort **couchée sur le dos** — le chapeau de bumper de 76 mm
s'étale sur 76 mm de long et ne fait plus que 12,8 mm de haut. D'où `REDRESSEMENT`, un quart de
tour `Rotation(-90°, "X")` inséré **entre** `frame` et le lacet :

```
bake = frame @ REDRESSEMENT @ Rotation(spin, "Z") @ Translation(ancre)
```

L'ordre compte : le lacet est appliqué **avant** le redressement, donc autour de l'axe qui
deviendra la verticale de la table — c'est bien un lacet, pas un roulis.

⚠️ **Piège mesuré : `bpy.data.libraries.load(..., link=False)` et non `wm.open_mainfile`.**
L'append importe les objets d'un `.blend` **sans ouvrir le fichier**, et c'est ce qui **conserve
les matériaux** — `export_parts.py` peut se passer de l'append parce qu'il repart d'une scène
vide, pas nous. Mesuré : 0,03 à 0,16 s sur les fichiers de 8 à 85 Mo du dépôt.

Corollaire : **une pièce est souvent une hiérarchie**, et il faut appender **tous** les objets du
fichier. N'apporter que la racine laisse ses parents derrière et les matrices monde deviennent
fausses — la sonde `probe_append.py` l'a montré sur `Plunger`, annoncé à 0 maillage.

**Purge obligatoire entre deux fichiers.** `export_scene.fbx` n'est pas sélectif : les 93 objets
de `Bumpers.blend` — caméra, repères `Setup`, rebuts — partiraient dans le FBX avec les pièces.
`purge_unlinked()` supprime ce que l'append a laissé sans collection, **après chaque fichier** et
pas seulement à la fin : deux `.blend` du dépôt partagent des noms d'objets, et un `Bumper Ring`
resté en mémoire serait repris par le suivant.

**La géométrie est figée, pas portée par un transform.** La matrice de passage est une
**réflexion** (déterminant −1) — c'est elle qui annule le miroir d'Unity — et un transform de
déterminant négatif se propage mal : Unity en retourne le sens des faces. Les sommets sont donc
écrits, et `recalc_face_normals` rétablit les normales. `frame_matrix()` interroge `to_blender`
sur les trois axes au lieu de recopier la conversion : deux copies divergeraient.

**L'ancre dit où tombe le point de pose** sur la pièce — `"base"` (dessous de la boîte, une pièce
posée sur le plateau), `"plan"` (recentrée dans le plan mais hauteur d'origine conservée, pour un
anneau ou un chapeau concentrique), `"origin"` (l'origine du dépôt, pour un empilement),
`"center"`. Mesuré : le socle de bumper en `"origin"` sortait décentré (`x -0,000→+0,030`) ; c'est
ce qui a fait ajouter `"plan"`.

Une cible relevée du dépôt est un ensemble monté qui **traverse** le plateau : 59 mm de haut dont
la fixation. `TARGET_SINK = 0,032` l'enfonce d'autant plutôt que de retailler la pièce — elle
reste l'asset du dépôt, c'est sa pose qui change. Il en reste 27 mm visibles.

⚠️ **Piège mesuré : `obj.matrix_world` n'est valide qu'après `bpy.context.view_layer.update()`.**
Sans cet appel, `world_bounds` relit la pose précédente et le rapport décrit une autre scène —
les pièces sortaient toutes mesurées centrées sur 0.

**Sonde : `Tools/blender/probe_append.py`** — mesure le temps d'append, les objets et matériaux
ramenés, les dimensions réelles de pièces témoins. Elle ne décide rien, elle constate. C'est elle
qui a établi les deux faits ci-dessus (l'append conserve les matériaux ; une pièce à hiérarchie
rend 0 maillage si on n'appende que sa racine).

### 4. ComfyUI — images (Krea 2 Turbo)

Port **8189** (installation standalone, la seule à avoir `ComfyUI-GGUF`).
⚠️ Le port 8188 appartient à Comfy Desktop, qui n'a pas GGUF — ne pas confondre.

```powershell
Set-Location "C:\Users\tomta\ComfyUI"
& "C:\Users\tomta\ComfyUI\.venv\Scripts\python.exe" main.py --port 8189 --listen 127.0.0.1 --preview-method none
```

```powershell
python Tools/comfyui/krea2_generate.py --name bumper --prompt "..." --seed 777
python Tools/comfyui/krea2_generate.py --batch
```

Sortie : `Tools/comfyui/out/<name>.png`. ~35-70 s par image 1024². Voir
`.github/skills/local-asset-pipeline/SKILL.md`.

### 5. Modly — image → mesh (Hunyuan3D 2 Mini)

Port **8765**. L'app desktop doit tourner (`%LOCALAPPDATA%\Programs\Modly\Modly.exe`).
La CLI est `Tools/modly-cli/agent.py`.

```powershell
python Tools/modly-cli/agent.py health
python Tools/modly-cli/agent.py generate --image <png normalisé> `
  --output Tools/modly-output/krea_bumper.glb --no-texture --remesh quad
```

**Normaliser l'image avant, c'est obligatoire** (`Tools/normalize_part_image.py`) : une image
avec ombre portée douce fait mourir le bridge FastAPI de Modly sans stack trace.

### 6. Pipeline complet en une commande

`Tools/make_part.py` enchaîne Krea 2 → normalisation → Hunyuan3D → décimation → `Assets/Models/Parts/`.

```powershell
python Tools/make_part.py --name bumper --subject "a pinball bumper cap, round red mushroom, product shot"
python Tools/make_part.py --all
```

⚠️ **Une seule tâche GPU lourde à la fois.** Krea 2 garde ~8.8 GB résidents : libérer avec
`POST /free {"unload_models":true,"free_memory":true}` sur 8189 avant Hunyuan3D, sinon le
convertisseur plante. Vérifier avec `nvidia-smi`.

### 7. Contraintes GPU et pièges d'assets

- Hunyuan3D émet 0.2-1M triangles par pièce → `Tools/decimate_glb.py` (pymeshlab) ramène à 8 000.
  pymeshlab n'existe que dans le venv de Modly.
- Les GLB décimés importent **sans matériau** (trimesh n'en écrit pas) → réassigner depuis le set
  URP existant (`BumperRed_Mat`, `Metal_Mat`, `FlipperOrange_Mat`, `BallChrome_Mat`).
- Les GLB Kenney référencent `Textures/colormap.png` en externe : copier l'atlas à côté des meshes
  puis forcer un réimport (`ImportAsset` + `ForceUpdate`).
- URP **GPU Resident Drawer** est désactivé sur `Assets/Settings/PC_RPAsset.asset` : actif, il
  inonde la console d'erreurs `BatchDrawCommand` à chaque changement de matériau.

## Architecture des scripts

Le GDD §Architecture Unity impose de **ne pas avoir un script central unique**. Une
responsabilité par fichier, et les valeurs d'équilibrage dans des données configurables
(ScriptableObjects), jamais en dur.

**État actuel : les scripts sont à plat dans `Assets/Scripts/`** (pas encore rangés en
sous-dossiers). C'est volontaire pour le MVP : déplacer un `.cs` sans son `.meta` change son
GUID et casse toutes les références de la scène. Le rangement se fera en déplaçant `.cs` **et**
`.cs.meta` ensemble.

```
Assets/Scripts/                  ← état actuel
  InputRouter      entrées clavier, source unique (GDD §Contrôles)
  GameManager      état de partie, billes restantes, game over
  ScoreManager     score, multiplicateur, record local
  BallManager      billes physiques, plafond de vitesse, anti-blocage
  HudController    affichage, abonné aux deux gestionnaires
  Flipper          flipper (HingeJoint à ressort), 3 côtés
  Plunger          lanceur à ressort
  Bumper           bumper actif
  Slingshot        slingshot
  DrainZone        zone de sortie
  TableGravity     gravité inclinée
  ScoreTarget      cible générique (score + impulsion)
  SubjectTarget    cible de mission
  MissionManager   missions
  MultiballManager multiball
```

```
Assets/Scripts/                  ← cible, une fois le MVP stabilisé
  Core/        GameManager, ScoreManager, BallManager, DifficultyManager, SaveManager
  Config/      ScoreConfig, DifficultyConfig, MissionConfig, BallConfig,
               FlipperConfig, AudioConfig, TableElementConfig
  Table/       Flipper, Plunger, Bumper, Slingshot, DropTarget, TargetGroup,
               RampGate, LoopGate, Door, BossTarget, DrainZone, TiltController
  Missions/    MissionManager, MissionDefinition, NuitDeLInfo, MultiballManager
  UI/          HudController, MessageDisplay, MenuController, PauseMenu, GameOverScreen
  Audio/       AudioManager
  FX/          TableLightFx, ...
```

**Reste à faire côté GDD §Architecture :** les valeurs d'équilibrage sont encore en champs
`[SerializeField]` dans les composants, pas dans des ScriptableObjects `*Config`. C'est
acceptable pour le MVP (elles se règlent dans l'Inspector) mais doit migrer avant l'étape 5.

### Règles

- Cacher les références en `Awake` ; **jamais** de `Find`/`GetComponent` dans `Update`.
- Toujours null-check les singletons (`GameManager.Instance`) et l'UI avant usage.
- `ForceMode.Impulse` pour la bille ; `direction.y` entre 0.2 et 0.25 pour les rebonds.
- Tags : `CompareTag("Ball")` — jamais `== "Ball"`.
- Le score passe par `ScoreManager`, jamais écrit directement.
- Toute valeur d'équilibrage vient d'un `*Config` (ScriptableObject).

## Conventions de scène (vérifiées)

- `-Z` = **bas** de la table (apron, drain, flippers) ; `+Z` = haut (orbit, cibles).
- `-X` = gauche, `+X` = droite. **Le chenal du plongeur est à droite**, hors de l'aire de jeu.
- L'aire de jeu va de `-HalfX` à `+HalfX` (3.475). `TableCenterX` (0.565) centre table+chenal.
- Les largeurs de chenal se mesurent **entre les faces des murs**, pas entre leurs centres
  (sinon la bille se coince).
- Un mur qui en chevauche un autre crée un coin interne qui coince la bille et l'éjecte
  verticalement.
- Le décor est **sans collider** : un collider oublié casse la physique validée de la table.
- Masquer une pièce de décor = désactiver son `MeshRenderer` uniquement, **jamais** le GameObject
  (sinon scripts et colliders cessent de fonctionner).
- Le repère de la table importée **n'est pas centré** : `z = 0` au bord bas (drain), `z ≈ 17,5` au
  mur du fond, `x = 0` au centre de l'aire de jeu, `y = 0` à la surface du plateau. C'est le
  repère de `build_table.py`, celui dans lequel `PlaceImportedTable` pose les dix hôtes.
- ⚠️ **`PinballTable/Table` doit rester à `y = 0`.** Mesuré : la face supérieure de la plaque du
  plateau est à `z = 0,0000 cm` dans le repère du FBX — le plan d'origine du modèle *est* la
  surface de jeu. Tout `y` non nul fait flotter la table au-dessus des hôtes, qui sont posés
  depuis cette surface.
- Le sol de la table s'arrête à `z = 0,50` : de `z 0` à `z 0,50` il n'y a **rien**, ni au centre
  (c'est l'ouverture de drain) ni dans le couloir (c'est le logement du lanceur). Ce n'est pas un
  trou de géométrie, c'est la place de `DrainZone` et de `Plunger`.

## Workflow

- **Git** : ne jamais travailler sur `main` ; une branche par fonctionnalité
  (`feature/flippers`, `fix/ball-stuck`, ...). Commits petits et fréquents.
  Scènes et prefabs modifiés par une seule personne à la fois.
  Exclus : `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`.
- **Vérification** : ne jamais affirmer qu'un script compile sans `unity command recompile`
  puis `recompile_status` / `get_console_logs`.
- **Test physique** : l'éditeur n'avance que ~2 frames quand la fenêtre n'a pas le focus.
  Pour tester via le CLI, passer `Physics.simulationMode = Script` et appeler
  `Physics.Simulate(dt)` en boucle — c'est ce que fait `Tools/unity/test_ball.cs`, qui instancie
  `Ball.prefab` au point d'apparition, relève la trajectoire et détruit la bille ensuite.
  Un rayon (`Physics.Raycast`) est souvent plus parlant qu'une capture : c'est lui qui a montré
  que le sol du couloir était 2,36 u au-dessus du point d'apparition.
- **Build** : Windows + WebGL (GDD §Optimisation). Low-poly, peu de lumières temps réel,
  matériaux réutilisés, pas de shaders coûteux.

## Ordre de développement (GDD §Plan de production)

1. **MVP** — table simple, bille, lanceur, 2 flippers, 2 slingshots, 3 bumpers, zone de sortie,
   3 billes, score simple, game over, caméra fixe, contrôles clavier.
2. Score complet — multiplicateur, combos, bonus de fin de bille, high score local.
3. Missions — 7 missions, cibles par groupe, Nuit de l'Info.
4. Modes avancés — boss Projet Final, multiball, bille supplémentaire, tilt.
5. Habillage — UI complète, menus, audio, difficultés, décors IUT/Vosges.

Ne pas commencer par le multiball, le boss, le décor final ou la sauvegarde.

## Workflow de scène

Quatre menus :

1. **`Flipper > Assurer la structure MVP`** (`Assets/Editor/EnsureMvpStructure.cs`) — crée
   **uniquement ce qui manque** : hôtes de gameplay, gestionnaires, Canvas + HUD. Non destructif,
   annulable, n'enregistre pas la scène.
2. **`Flipper > Rebuild Table Layout`** (`Assets/Editor/BuildPinballTable.cs`) — construit le
   mobilier (plateau, murs, décors) et **positionne** les hôtes créés à l'étape 1.
3. **`Flipper > Rendre la scène neutre (hôtes vides)`** (`Assets/Editor/NeutralScene.cs`) —
   conserve les hôtes et leurs scripts mais leur retire toute la géométrie générée (colliders,
   filtres, rendus, échelle), et complète les gestionnaires manquants (`GameManager`,
   `MissionManager`, `MultiballManager`, `BallSpawnPoint`) ainsi que les références de bille.
4. **`Flipper > Placer la table importée`** (`Assets/Editor/PlaceImportedTable.cs`) — **le menu
   à utiliser**. Pose `Assets/Models/Table/` sous `PinballTable/Table/`, met un `MeshCollider`
   sur chaque maillage de `Pinball_Table` (et **aucun** sur `Pinball_Cabinet`), incline le root
   à −7° et place les 10 hôtes aux coordonnées du générateur.

`Flipper > MVP : structure puis table complète` enchaîne les menus 1 et 2.

Le menu 4 est idempotent et non destructif : relancé, il ne recrée rien, ne rajoute aucun
collider déjà présent, et laisse le root incliné tel quel. Il rattache à `Gameplay` un hôte
trouvé ailleurs dans la scène — sans quoi `BallSpawnPoint`, resté racine, n'aurait pas suivi
l'inclinaison et la bille serait apparue à côté du couloir.

⚠️ **Le menu 2 régénère la géométrie procédurale, et avec elle le coin qui coinçait la bille**
(le disque `Skillshot` et le rail `Orbit_Outer_10` formaient un V où elle s'arrêtait net). Ne pas
le lancer sur une scène construite avec des assets importés : il replacerait les hôtes et
rajouterait le mobilier généré par-dessus. Les scripts restent dans le dépôt, mais la scène de
travail est `Assets/Scenes/Neutral.unity`.

**Un hôte ne peut pas être vide si son script exige un collider.** `Bumper`, `Slingshot` et
`DrainZone` portaient `[RequireComponent(typeof(Collider))]` : Unity refuse alors de supprimer le
collider tant que le script est présent, et le rajoute dès qu'on ajoute le script. L'attribut
en a été retiré — aucun des trois ne déréférence son collider, sauf `DrainZone` qui a reçu des
gardes dans `Reset` et `Awake`. Sans collider, ces composants sont simplement muets.

**Pourquoi cet ordre est obligatoire :** `BuildPinballTable` *crée* le mobilier mais se contente
de *déplacer* les hôtes de gameplay via son helper `Move()`, qui affiche
`[BuildPinballTable] missing host '<nom>'` quand l'objet n'existe pas. La table se construit donc
sans ses flippers si l'étape 1 n'a pas été faite avant.

Noms d'hôtes attendus par `BuildPinballTable` (à ne pas renommer à la légère) :
`Bumper_01..03`, `Slingshot_Left/Right`, `Flipper_Left_Pivot`, `Flipper_Right_Pivot`, `Plunger`,
`Target_Programmation`, `Target_Reseau`, `Target_Web`, `Target_BDD`, `Target_Projet`,
`Ramp_Center_PB`, plus les textes du HUD `ScoreText`, `HighScoreText`, `BallsText`, `MissionText`,
`MessageText` sous un objet nommé `Canvas`.

## État d'avancement

Dernière vérification de compilation : éditeur 6000.6.0f1. **Scène de travail :
`Assets/Scenes/Neutral.unity`.**

**La table existe.** Le plateau procédural de `BuildPinballTable` reste abandonné — il produisait
un coin où la bille s'arrêtait net (le disque `Skillshot` et le rail `Orbit_Outer_10` formaient un
V, équilibre stable dans les trois axes), et `Assets/Scenes/Main.unity` en garde la trace. Il est
remplacé par une table **générée dans Blender** (`Tools/blender/build_table.py`) à partir des
dimensions réelles du standard : 20,25" × 42" = 0,51435 × 1,06680 m, soit 8,573 × 17,78 unités
Unity. Les deux fichiers sortent dans `Assets/Models/Table/` :

| Fichier | Contenu | Colliders |
| --- | --- | --- |
| `Pinball_Table.fbx` | 41 maillages — plateau, murs, orbite, slingshots (12, engendrés) + 3 bumpers, 16 cibles, 4 poteaux (29, **pièces du dépôt**) | `MeshCollider` par maillage |
| `Pinball_Cabinet.fbx` | 11 maillages, 408 triangles — caisson, @ gravé | **aucun** (décor) |

Le contenu de `Pinball_Table` est donc bien les **pièces du dépôt** pour tout ce que le dépôt
sait fournir ; seuls le plateau, les murs, l'orbit et les slingshots restent engendrés, faute
d'équivalent dans `pinball-parts`. Assemblé par `Tools/blender/assemble_parts.py` — voir la
section correspondante dans « Outillage ».

Mesuré **dans Unity**, pas supposé : 9,44 × 1,30 × 17,55 u pour la table (41 maillages,
5 944 sommets, empreinte `8ec6c9a22ad33f96`), 10,77 × 9,58 × 21,18 u pour le caisson, échelle de
racine 1666,67 (la signature normale de l'importeur, à ne pas réinitialiser). Les sections
engendrées sortent aux coordonnées **identiques** à celles de la table d'avant l'assemblage — la
compensation du miroir tient donc toujours, chenal de lancement à droite. Relevé après repose :
`Pinball_Table` 41 maillages / 41 colliders, `Pinball_Cabinet` 11 maillages / **0** collider,
root à `(353.00, 0.00, 0.00)`.

⚠️ **Piège mesuré : les matériaux Blender survivent par leur nom mais perdent leur couleur.**
Les 29 pièces du dépôt arrivent avec leurs matériaux (`Bumper - Plastic White` ×3,
`Bumper - Cap Red` ×3, `Metal - Steel with AO dirt` ×7, `Target - Plastic White` ×16) et les
12 maillages engendrés avec le `Lit` par défaut. **Aucun n'est magenta** : les 41 sont sur
`Universal Render Pipeline/Lit`, donc l'import FBX s'est bien fait vers URP. Mais leur
`_BaseColor` est uniformément gris clair — `Bumper - Cap Red` mesure `0,906` et non du rouge.
C'est pourquoi la vue Scène sort en camaïeu : ce n'est pas un défaut de shader, c'est une couleur
absente. Le remède est de **réassigner depuis le set URP du projet** (`BumperRed_Mat`,
`Metal_Mat`, `TargetYellow_Mat`, `Playfield_Mat`, `FlipperOrange_Mat`, `BallChrome_Mat`), où la
couleur est éditable — c'est de toute façon sa place.

⚠️ **Piège mesuré : Unity miroite l'axe x des FBX Blender.** La conversion qu'Unity applique en
lecture est `blender (x, y, z) -> unity (-x, z, -y)`, quelle que soit la valeur d'`axis_forward` /
`axis_up`. Ce couple ne touche d'ailleurs **pas** aux sommets : il n'est que déclaré dans
`GlobalSettings`, et les quatre valeurs essayées écrivent exactement les mêmes coordonnées. La
compensation se fait donc à la source, dans `to_blender` de `build_table.py`, qui nie x. Sans
elle la table sortait miroitée — chenal de lancement à gauche au lieu de droite, contre la
convention du projet. `Tools/blender/probe_axis.py` établit ces faits par lecture des octets du
FBX ; ni la comparaison d'empreintes (le FBX embarque un horodatage) ni le réimport dans Blender
(l'importeur défait ce que l'exportateur a fait) ne pouvaient le montrer. `assemble_parts.py`
**réutilise** `to_blender` au lieu de le recopier, donc les pièces du dépôt sont miroitées dans
Blender pour ressortir droites dans Unity — ce qui règle au passage leur chiralité.

Non vérifié : le choix de pièces actuel (bumpers, poteaux, cibles plates) est **achiral**, le
miroir ne peut donc pas être falsifié avec elles. Ce contrôle repose toujours sur le flipper.

**`Neutral.unity`** contient les 9 hôtes (`Flipper_Left/Right_Pivot`, `Slingshot_Left/Right`,
`Bumper_01..03`, `Plunger`, `DrainZone`) — **sans géométrie générée** : ce sont les pièces
importées de `vbousquet/pinball-parts` qui les habillent, pas le générateur. `Plunger` est le
premier à l'être (`Plunger_Rod` + son `BoxCollider`) ; les huit autres restent nus. S'y ajoutent
`Flipper_Bat` vide sous son pivot et les gestionnaires (`GameManager`, `MissionManager`,
`MultiballManager`, `InputRouter`, `ScoreManager`, `BallManager`).

La table y est posée par le menu 4 : 41 colliders, root incliné à −7°, et les 10 hôtes aux
coordonnées du générateur. S'y ajoute la **bille posée** sous `Gameplay`, aux coordonnées de
`BallSpawnPoint` (`Tools/unity/place_scene_ball.cs`) — elle se voit et se déplace dans l'éditeur,
et c'est elle que la partie réutilise.

⚠️ **La scène est modifiée et non enregistrée.** Aucun menu ne sauvegarde : **Ctrl+S** pour la
conserver.

Un point reste à la main de l'utilisateur :

- `DrainZone` n'a toujours pas de collider : il lui en faut un **en déclencheur**, sinon aucune
  bille n'est comptée comme perdue. Sa forme est un choix de jeu — sa taille décide des billes
  comptées perdues. Le composant le signale d'un avertissement au démarrage.

**Corrections compilées et vérifiées** — par réflexion sur les types chargés, et non sur la
parole de `recompile` qui répondait `up_to_date` : `GameManager.NotifyBallReturnedToLane()`
existe, `stateBeforePause` aussi, et `Bumper`/`Slingshot`/`DrainZone` ne portent plus aucun
`RequireComponent`. L'anti-blocage de `BallManager` bouclait sans fin — `Respawn` ramenait la
bille dans le couloir sans repasser l'état à `ReadyToLaunch`, donc l'anti-blocage relançait
indéfiniment une bille légitimement immobile ; `TogglePause` recréait la même incohérence en
restaurant `Playing` en dur.

**Pièces disponibles** — 19 FBX dans `Assets/Models/Parts/<Catégorie>/`, exportés du dépôt et
vérifiés **à l'import dans Unity** (tailles exactes, matériaux inclus) :

| Dossier | Pièces |
| --- | --- |
| `Flipper/` | `Flipper_Williams_3` — bat 3", 80 mm, le standard |
| `Bumpers/` | base Williams/Bally, cap, ring, socket |
| `Miscellaneous/` | `Ball` (27 mm : l'ancre d'échelle), `Plunger`, `Leveler`, `Solenoid` |
| `Posts/` | 2 métal + 1 plastique |
| `Lane_Guides/` | guide de couloir, bas de couloir, rail plat |
| `Switches/` | `Drop_Target`, `Gate_Flap`, `Gate_A-8096_-_Bracket_Right` |

⚠️ **Le dépôt ne contient pas de table.** Plateau, murs et couloir de lancement n'y sont pas :
`pinball-parts` est un magasin de pièces, pas un décor. En revanche il **fournit bien les pièces
posées sur la table** — bumpers, cibles, poteaux — et c'est ce que fait `assemble_parts.py`. Le
plateau, lui, reste engendré (`Tools/blender/build_table.py`) ; c'est la seule partie que le
dépôt ne peut pas fournir.

Les objets `Playfield.001/.002/...` qu'on trouve dans `Guides.blend` et `Inserts.blend` ne sont
pas un plateau : ce sont des **fonds d'inserts** de 44 à 88 polygones. Ne pas s'y tromper.

⚠️ **Piège mesuré : les 19 pièces sont miroitées en x dans Unity.** Le marqueur de
`probe_axis.py`, exporté avec les réglages d'`export_parts.py` et mesuré dans Unity, ressort à
`x −0,300 -> −0,100` alors qu'il est écrit à `x 0,10 -> 0,30` — et `y` / `z` passent tels quels.
C'est la même conversion que celle qui a mis le chenal de lancement de la table à gauche. Sur une
pièce chirale, la chiralité est donc retournée : `Gate_A-8096_-_Bracket_Right` est un support
gauche, et un bat de flipper arrive en version gauche-main. `export_parts.py` n'a pas de
conversion de repère à corriger — les pièces sont exportées dans les coordonnées Blender natives,
et le miroir vient de la lecture. Le corriger demande de mettre chaque maillage en miroir **dans
Blender** avant export, puis de recalculer ses normales, et de vérifier le résultat pièce par
pièce dans Unity (`--verify` ne le verrait pas : l'importeur Blender défait ce que l'exportateur
a fait).

Ce miroir ne concerne plus que les pièces exportées **individuellement** dans
`Assets/Models/Parts/`. Les bumpers, cibles et poteaux passent désormais par `assemble_parts.py`,
qui réutilise `to_blender` et annule donc le miroir à la source.

**La bille est posée et vérifiée en physique.** Le prefab `Assets/Prefabs/Ball.prefab` existait
déjà et était déjà câblé (`GameManager.ballPrefab` et `ballSpawnPoint` remplis) : sphère Unity
intégrée (choix retenu plutôt que `Ball.fbx`), échelle 0,45 pour un `SphereCollider` de rayon 0,5,
soit **0,2250 u** réels ; `Rigidbody` masse 1, `ContinuousDynamic`, `Interpolate` ; matériau
`BallPhysics` (rebond 0,65, combinaison « Maximum ») ; tag `Ball`, layer `Ball` (index 8). Les
quatre layers du GDD (`Ball`, `Table`, `TableElement`, `Environment`, index 8 à 11) ont été
ajoutés à `TagManager.asset` par `Tools/unity/setup_layers.cs`.

Test de chute (`Tools/unity/test_ball.cs`, `Physics.simulationMode = Script` + `Physics.Simulate`) :
la bille roule **0,427 u en 1 s** dans le couloir, soit exactement `g·sin 7° / (1 + 2/5)` — une
sphère pleine qui roule sans glisser. La pente, le matériau physique et l'échelle sont donc bons.

⚠️ **Piège mesuré, corrigé : la table flottait 2,36 u au-dessus des hôtes.** `PinballTable/Table`
était à `y = 2,360` (14,16 cm) — un réglage à la main, aucun script ne l'écrit
(`PlaceImportedTable.EnsureGroup` pose `Vector3.zero`). La bille naissait alors *sous* la table et
tombait en chute libre sans rien toucher : `x` et `z` constants au millième, 2,578 u en 0,70 s,
soit `½·g·t²` au chiffre près. C'est cette chute parfaitement libre qui a mis sur la piste — une
bille qui *traverse* et une bille qui tombe *à côté* se ressemblent à l'écran. Corrigé à `y = 0` ;
le sol du couloir est remonté à `y 0,2168` sous le point d'apparition (`y 0,5109`), et la bille
tombe maintenant de **0,069 u — 4,2 mm** avant de rouler.

⚠️ **Piège mesuré, corrigé : la bille posée faisait 60 mm.** L'échelle d'un prefab **n'est pas une
coordonnée** : elle ne se recopie pas du point d'apparition. `Ball.prefab` porte 0,45 (27 mm, l'ancre
d'échelle du projet) ; le premier jet de `Tools/unity/place_scene_ball.cs` y avait écrit l'échelle du
point d'apparition — `(1, 1, 1)` — soit une bille de **1,00 u = 60 mm**. Les trois conséquences
mesurées, toutes cohérentes avec ce seul chiffre : plus large que le couloir (34 mm), donc **éjectée
vers le haut** par la physique en Play mode (relevée à `y 1,2097`) ; le bas de la bille à `y 0,0109`
pour un sol à `y 0,2168`, donc **enfoncée de 0,206 u** dans le plateau ; et un rapport largeur de
l'aire de jeu / diamètre tombé de 19,05 à 8,67 — d'où l'impression, très juste, qu'elle était trop
grosse. Deux pertes de bille parasites en découlaient (`billes restantes` à 1 sur 3). Corrigé à 0,45 :
le rapport remonte à **19,05** et la partie démarre à 3 billes.

**La bille est posée dans la scène, et `BallManager` la réutilise.** `PinballTable/Gameplay/Ball`,
en frère de `BallSpawnPoint` (et non son enfant : un `Rigidbody` sous un parent incliné hériterait de
son mouvement), aux mêmes coordonnées locales que lui. `BallManager.sceneBall` la référence — le champ
est un repli, la bille se trouve par son tag. Elle est **parquée** hors jeu (désactivée, cinématique)
et **réveillée** par `SpawnBall`, qui la réutilise au lieu d'instancier une seconde ; elle n'est
**jamais détruite**, ni par un drain ni par `ClearAll`. Vérifié en Play mode : une seule bille en jeu,
et c'est la même référence que celle de la scène. Sans bille posée, `sceneBall` reste vide et le
comportement d'avant est conservé (instanciation depuis le prefab).

⚠️ Le piège que cela ferme : `FindAnyBall()` (repli de `LiveBallCount`) ne doit **pas** compter la
bille parquée comme vivante — sinon `HandleBallLoss` verrait toujours une bille en jeu, aucune perte
ne serait décomptée, et la partie ne s'arrêterait jamais.

**La largeur de l'aire de jeu est au standard** : 514,3 mm entre les faces internes du mur gauche et
du séparateur, soit **19,05 bille** — une table réelle donne 19,06 (20,25" pour une bille de 1"1/16).
La table n'est donc pas trop petite. Le couloir de lancement fait 34,0 mm (1,26 bille ; réel ~32 mm).
Un balayage `RaycastAll` de gauche à droite mesure 520 mm et laisse croire à un écart : il ne relève
que les faces d'**entrée**, donc la face *externe* du mur. Mesurer un intervalle se fait **depuis le
centre vers chaque mur**.

**Le lanceur est posé et éprouvé en physique.** `PinballTable/Gameplay/Plunger` porte désormais
l'instance `Plunger_Rod` (`Assets/Models/Parts/Miscellaneous/Plunger.fbx`, instance de prefab)
et son `BoxCollider`. Le couloir est fermé : mesuré, la bille partie du point d'apparition roule
et s'arrête à `(4,6700 ; 0,3921 ; 0,5264)` — contre `(4,6700 ; 0,3902 ; 0,5250)` annoncés — et ne
quitte plus la table. `PushBalls(1)` appelée par réflexion lui donne **17,956 u/s** et la fait
grimper les 16,87 u du couloir jusqu'à `z 17,3953`.

La cote n'est pas esthétique, elle est contrainte des deux côtés :

- **fermer le couloir** — le sol s'arrête à `z 0,3187`, le centre de la bille doit rester au-delà ;
- **rester dans la boîte de `PushBalls`** — `catchOffset` 0,6 devant la position de repos,
  `maxPull` 0,8 derrière. Au point d'apparition la bille est à **0,8330 u** devant : hors zone.
  C'est pourquoi un relâchement ne poussait rien, indépendamment du couloir ouvert.

D'où un bouchon à `z 0,30` en monde, la bille au repos à 0,4033 u devant le lanceur. Les deux
cotes sont écrites en **local de l'hôte** (`z 0,17828`, `y −0,041436`) et non en monde : l'hôte
est incliné avec la table, une cote en monde deviendrait fausse au premier changement.

Deux pièges mesurés, tous deux de la même famille que la bille à 60 mm — **une dimension n'est pas
une coordonnée** :

- **`BoxCollider.size` s'exprime échelle de la pièce comprise.** La racine du FBX porte 0,0167 :
  une taille écrite en unités de scène y devient 60 fois plus petite. Mesuré au premier essai,
  `Extents (0,0042 ; 0,0048 ; 0,0019)` — un collider de 8 mm au lieu de 500.
- **Une taille en axes de l'hôte se reporte avec les axes de l'hôte.** `Vector3.up` /
  `Vector3.forward` sont ceux du **monde** ; l'hôte incliné de 7° a les siens. Les confondre
  répartit une cote sur deux axes : une épaisseur de 0,1 sortie à 0,1666, sans que la boîte monde
  le montre — c'est une englobante alignée sur le monde, elle étale toute cote fausse et la noie.
  Le contrôle qui vaut est la boîte **ramenée dans les axes de l'hôte**, comparée à la cote voulue.

Le collider est posé **sur la pièce, pas sur l'hôte** : `NeutralScene.Strip` retire les composants
*directs* de chaque hôte, donc un collider sur l'hôte serait effacé au prochain passage du menu 3.
C'est déjà le motif du projet — c'est `Flipper_Bat` qui porte le collider, pas son pivot.

⚠️ **Il manque la porte à sens unique en haut du couloir.** La bille lancée grimpe tout le couloir,
bascule dans l'aire de jeu, mais **redescend le couloir** au lieu d'y être retenue : rien ne
l'empêche d'y rentrer. C'est la pièce `Gate_Flap` du dépôt (`Switches/`), à poser. Sans elle le
lancement fonctionne mais la bille revient au lanceur.

⚠️ **La tige dépasse du caisson.** La pièce mesure 165,1 mm et le bouchon est à `z 0,30` : son
extrémité arrière sort à `z ≈ −2,44` en monde, soit ~10 cm devant le caisson (dont le bord est à
`z −0,72`). Sur une machine réelle la tige dépasse effectivement — c'est à juger à l'œil, et à
retoucher en déplaçant `Plunger_Rod` (`Tools/unity/place_scene_plunger.cs` repositionne le bouchon,
`Tools/unity/destroy_plunger_rod.cs` retire l'instance pour rejouer la pose).

**Ensuite, la bille sortait par le bas du couloir** (`z −0,505`, hors de l'emprise de la table) :
c'était le comportement juste tant que l'hôte était vide. Les autres hôtes restent **vides** —
leur géométrie est dans la table, mais ils n'ont pas de collider propre. Le prochain jalon est
`Flipper_Williams_3` sous `Flipper_Bat`, puis de donner à `Bumper_01..03`, aux slingshots et au
drain un collider posé sur la pièce — en veillant à ce que le `MeshCollider` de la table et les
colliders des hôtes ne se chevauchent pas.

⚠️ **`DrainZone` n'a toujours pas de collider** et le composant le signale au démarrage. Il lui en
faut un **en déclencheur**, sinon aucune bille n'est comptée perdue. Sa forme est un choix de jeu.

| Bloc GDD | Scripts | Scène |
| --- | --- | --- |
| Contrôles clavier | ✅ `InputRouter` (2 dispositions) | présents |
| Contrôles borne xin-mo | ✅ `Flipper.useCabinetController` lit `PinballControls` (cumulé au clavier) | bornier détecté, liaisons résolues |
| Bille, lanceur | ✅ `Plunger`, `BallManager` | **bille posée (27 mm) et réutilisée ; lanceur posé et éprouvé en physique** — reste la porte à sens unique en haut du couloir |
| Flippers principaux | ✅ `Flipper` (3 côtés, miroir auto) | hôtes posés (2), **bats à habiller** |
| Flipper secondaire | ✅ `Flipper` (côté `Upper`) | absent |
| Slingshots | ✅ `Slingshot` | hôtes posés (2), colliders à poser |
| Bumpers | ✅ `Bumper` (désactive `ScoreTarget` en doublon) | hôtes posés (3), colliders à poser |
| Drain | ✅ `DrainZone` | hôte posé, **trigger à poser** |
| Score, record local | ✅ `ScoreManager` (multiplicateur prêt) | gestionnaire présent |
| État de partie, game over | ✅ `GameManager` (6 états) | présent, à recâbler |
| HUD | ✅ `HudController` | présent (Canvas) |
| Plateau, murs, couloir | — (géométrie, pas script) | ✅ `Pinball_Table.fbx` posé + 41 colliders |
| Bumpers, cibles, poteaux (pièces) | ✅ `Bumper`, `SubjectTarget` | ✅ géométrie du dépôt posée — **colliders des hôtes à poser** |
| Cibles par groupe | ⚠️ `SubjectTarget` existe, structure en groupes manquante | les 16 cibles du GDD sont dans le plateau |
| Matériaux des pièces | — | ⚠️ importés sans couleur → **à réassigner** depuis le set URP |
| Rampes, loop @, porte IUT, boss | ❌ | absents |
| Missions, Nuit de l'Info, multiball | ⚠️ versions simplifiées | présents |
| Tilt, difficultés, bille supplémentaire, audio, config SO | ❌ | absents |

Les objets `*Config` (ScriptableObjects) ne sont pas encore écrits : les valeurs d'équilibrage
sont en champs `[SerializeField]`.
