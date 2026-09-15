using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Crée la structure minimale du MVP sans jamais toucher à ce qui existe déjà.
///
/// Complément de <see cref="BuildPinballTable"/> : ce dernier <b>construit</b> le mobilier
/// (plateau, murs, décors) mais se contente de <b>déplacer</b> les hôtes de gameplay — il
/// affiche « missing host » quand ils n'existent pas. C'est ce script qui les crée.
///
/// Ordre d'utilisation :
///   1. <c>Flipper &gt; Assurer la structure MVP</c>   (ce script)
///   2. <c>Flipper &gt; Rebuild Table Layout</c>       (BuildPinballTable, positionne et habille)
///
/// Règle de non-destruction : un objet portant déjà le nom attendu est laissé intact, y
/// compris ses composants, ses colliders et ses réglages. Seuls les champs sérialisés restés
/// vides sont remplis. Tout est annulable (Ctrl+Z).
/// </summary>
public static class EnsureMvpStructure
{
    private const string MenuEnsure = "Flipper/Assurer la structure MVP (non destructif)";
    private const string MenuEnsureAndBuild = "Flipper/MVP : structure puis table complète";
    private const string MenuRepair = "Flipper/Réparer la zone de drain";
    private const string MenuTilt = "Flipper/Incliner la table à 7°";

    private const string TableRootName = "PinballTable";
    private const string SpawnName = "BallSpawnPoint";

    // Pente réelle de la table : le root PinballTable reçoit -7° autour de X (le haut de la table
    // monte, la bille roule vers les flippers). La gravité, elle, reste verticale — voir
    // TableGravity, qui portait autrefois une fausse pente en penchant la gravité.
    private const float TableTiltDegrees = 7f;

    // Point d'apparition, exprimé dans le repère de la table une fois celui-ci incliné. Il doit
    // tomber sur le plancher surélevé du couloir (y = 0.27) et non sur le plateau (y = 0.07),
    // sinon la bille apparaîtrait sous le plancher et serait éjectée.
    private static readonly Vector3 SpawnLocalPosition = new Vector3(4.215f, 0.505f, -5.55f);

    // Géométrie de repli. BuildPinballTable repositionne tout : ces valeurs ne servent qu'à
    // obtenir une scène lisible avant de l'avoir lancé. Elles reprennent ses constantes.
    private const float FlipperX = 2.30f;
    private const float FlipperZ = -4.80f;
    private const float LaneCentreX = 4.20f;
    private const float PlayY = 0.60f;
    private const float BatLength = 1.10f;
    private const float BatOffset = 0.55f;

    // Largeur intérieure du couloir de lancement, entre Cab_LaneDivider (x 3.48-3.83) et
    // Cab_Right (x 4.61-4.96). La bille fait 0.45 de diamètre : elle y passe sans flotter.
    private const float LaneWidth = 0.78f;
    private const float PlungerBlockDepth = 0.20f;

    // Zone de drain, exprimée dans le repère de la table — c'est-à-dire celui du parent du
    // déclencheur — et non plus en world : la table étant inclinée, un centre décrit en world
    // n'aurait plus aucun sens.
    //
    // BuildPinballTable met DrainZone à l'échelle (5.56, 2, 1) : la taille du collider est
    // multipliée d'autant et le déclencheur finit enterré sous la surface de jeu, où la bille ne
    // le touche jamais. ApplyDrainBox repart donc d'une échelle unitaire.
    //
    // La gouttière est l'ouverture entre Apron_L et Apron_R (x de -0.95 à 0.95) ; la bille s'y
    // pose sur la surface de jeu, son centre est alors à y = 0.30.
    private static readonly Vector3 DrainLocalSize = new Vector3(2.40f, 0.50f, 1.00f);
    private static readonly Vector3 DrainLocalCentre = new Vector3(0f, 0.30f, -6.95f);

    private static readonly List<string> Created = new List<string>();
    private static readonly List<string> Kept = new List<string>();

    [MenuItem(MenuEnsure)]
    public static void Ensure()
    {
        Created.Clear();
        Kept.Clear();

        GameObject table = EnsureObject("PinballTable", null, null, null);
        GameObject gameplay = EnsureObject("Gameplay", table.transform, null, null);

        EnsureManagers();
        EnsureFlipper("Flipper_Left_Pivot", gameplay.transform, -1);
        EnsureFlipper("Flipper_Right_Pivot", gameplay.transform, +1);
        EnsureSlingshot("Slingshot_Left", gameplay.transform, -2.90f);
        EnsureSlingshot("Slingshot_Right", gameplay.transform, +2.90f);
        EnsureBumpers(gameplay.transform);
        EnsurePlunger(gameplay.transform);
        EnsureDrain(gameplay.transform);
        EnsureHud();
        FillEmptyGameManagerFields();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            $"[EnsureMvpStructure] Terminé : {Created.Count} objet(s) créé(s), {Kept.Count} conservé(s).\n" +
            (Created.Count > 0 ? "Créés : " + string.Join(", ", Created) + "\n" : string.Empty) +
            "Étape suivante : Flipper > Rebuild Table Layout pour positionner et habiller la table.\n" +
            "Rien n'a été enregistré : la scène est marquée modifiée, à toi de sauvegarder (Ctrl+S).");
    }

    /// <summary>
    /// Enchaîne la création, la construction complète de la table, puis la réparation du drain.
    /// C'est le chemin recommandé : <see cref="RepairDrainZone"/> doit passer <b>après</b>
    /// <see cref="BuildPinballTable.Rebuild"/>, qui remet DrainZone à l'échelle.
    /// </summary>
    [MenuItem(MenuEnsureAndBuild)]
    public static void EnsureThenBuildTable()
    {
        Ensure();
        BuildPinballTable.Rebuild();
        RepairDrainZone();

        // En dernier, impérativement : Rebuild place tout en coordonnées monde en supposant la
        // table droite. Incliner avant lui donnerait une table de travers et un mobilier à plat.
        TiltTable();
    }

    /// <summary>
    /// Donne à la table sa pente réelle : rotation du root <c>PinballTable</c>, puis rattachement du
    /// point d'apparition pour qu'il suive cette pente.
    ///
    /// À lancer <b>après</b> <see cref="BuildPinballTable.Rebuild"/>, qui place tout en coordonnées
    /// monde en supposant la table droite. <see cref="EnsureThenBuildTable"/> respecte cet ordre.
    /// </summary>
    [MenuItem(MenuTilt)]
    public static void TiltTable()
    {
        GameObject root = GameObject.Find(TableRootName);

        if (root == null)
        {
            Debug.LogWarning($"[EnsureMvpStructure] Aucun '{TableRootName}' dans la scène : impossible " +
                             "de l'incliner. Lance d'abord « Assurer la structure MVP ».");
            return;
        }

        Undo.RecordObject(root.transform, "Incliner la table");
        root.transform.rotation = Quaternion.Euler(-TableTiltDegrees, 0f, 0f);

        string spawnNote = AttachSpawnPoint(root.transform);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"[EnsureMvpStructure] '{TableRootName}' incliné de -{TableTiltDegrees}° autour de X : " +
                  "le haut de la table monte, la bille roule vers les flippers.\n" +
                  spawnNote +
                  "Rien n'a été enregistré : Ctrl+S pour conserver.");
    }

    /// <summary>
    /// Rattache le point d'apparition à la table pour qu'il suive son inclinaison, et le pose sur le
    /// plancher surélevé du couloir.
    ///
    /// Sans ce rattachement il resterait droit dans le monde : la bille apparaîtrait à côté du
    /// couloir, et sous son plancher.
    /// </summary>
    private static string AttachSpawnPoint(Transform table)
    {
        GameObject spawn = GameObject.Find(SpawnName);

        if (spawn == null)
        {
            return $"Aucun '{SpawnName}' à rattacher : la bille apparaîtra à côté du couloir.\n";
        }

        if (spawn.transform.parent != table)
        {
            // SetParent(…, false) conserve les coordonnées locales telles quelles : le point se met
            // donc à suivre la table au lieu de rester droit dans le monde. L'Undo est enregistré
            // sur toute la hiérarchie, la surcharge SetTransformParent conservant la position monde.
            Undo.RegisterFullObjectHierarchyUndo(spawn, "Rattacher le point d'apparition");
            spawn.transform.SetParent(table, false);
        }

        Undo.RecordObject(spawn.transform, "Relever le point d'apparition");
        spawn.transform.localPosition = SpawnLocalPosition;
        spawn.transform.localRotation = Quaternion.identity;

        return $"'{SpawnName}' rattaché à la table et posé sur le plancher du couloir à {SpawnLocalPosition}.\n";
    }

    /// <summary>
    /// Redonne à la zone de drain ses dimensions en world.
    ///
    /// Nécessaire parce que <see cref="BuildPinballTable"/> met DrainZone à l'échelle
    /// (5.56, 2, 1) : la taille du collider est multipliée d'autant, la boîte atteint 36 unités
    /// de large et son centre descend à y = -0.55 — entièrement sous la surface de jeu. La bille
    /// roule au-dessus sans jamais déclencher le drain, et la partie se bloque : la bille reste
    /// dans la gouttière, l'anti-blocage la relance indéfiniment sans qu'elle puisse sortir.
    ///
    /// À relancer après chaque « Rebuild Table Layout » fait seul.
    /// </summary>
    [MenuItem(MenuRepair)]
    public static void RepairDrainZone()
    {
        GameObject host = GameObject.Find("DrainZone");

        if (host == null)
        {
            Debug.LogWarning("[EnsureMvpStructure] Aucun 'DrainZone' dans la scène : rien à réparer. " +
                             "Lance d'abord « Assurer la structure MVP ».");
            return;
        }

        ApplyDrainBox(host);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"[EnsureMvpStructure] DrainZone réparée : boîte {DrainLocalSize} centrée sur " +
                  $"{DrainLocalCentre} dans le repère de la table (échelle ramenée à " +
                  $"{host.transform.lossyScale}).\n" +
                  "Rien n'a été enregistré : Ctrl+S pour conserver.");
    }

    /// <summary>
    /// Applique la boîte de drain en raisonnant dans le repère de la table.
    ///
    /// L'échelle de l'hôte est d'abord ramenée à 1 : c'est elle qui rendait la boîte absurde, et
    /// raisonner en unités locales évite d'avoir à la diviser. Le centre passe ensuite par le
    /// parent, ce qui reste juste même une fois la table inclinée.
    /// </summary>
    private static void ApplyDrainBox(GameObject host)
    {
        BoxCollider box = EnsureComponent<BoxCollider>(host);

        // Toujours un déclencheur : un collider solide à cet endroit retiendrait la bille
        // au lieu de la compter comme perdue.
        box.isTrigger = true;

        host.transform.localScale = Vector3.one;

        Vector3 scale = host.transform.lossyScale;

        box.size = new Vector3(
            DrainLocalSize.x / SafeScale(scale.x),
            DrainLocalSize.y / SafeScale(scale.y),
            DrainLocalSize.z / SafeScale(scale.z));

        // Le centre voulu est décrit dans le repère de la table ; on le ramène en world par le
        // parent, puis en local du collider. InverseTransformPoint plutôt qu'une soustraction :
        // DrainZone peut avoir reçu une rotation, et le centre s'exprime dans l'espace local.
        Transform parent = host.transform.parent;

        Vector3 world = parent != null
            ? parent.TransformPoint(DrainLocalCentre)
            : DrainLocalCentre;

        box.center = host.transform.InverseTransformPoint(world);

        EditorUtility.SetDirty(box);
    }

    /// <summary>Évite la division par zéro si un objet a été mis à l'échelle nulle.</summary>
    private static float SafeScale(float value)
    {
        return Mathf.Abs(value) < 0.0001f ? 1f : value;
    }

    // ------------------------------------------------------------- hôtes de gameplay

    private static void EnsureManagers()
    {
        EnsureObjectWith<InputRouter>("InputRouter");
        EnsureObjectWith<ScoreManager>("ScoreManager");
        EnsureObjectWith<BallManager>("BallManager");
    }

    /// <summary>
    /// Un flipper est un pivot (<see cref="Rigidbody"/> cinématique + <see cref="Flipper"/>) dont
    /// l'enfant porte le collider de la batte. Séparer les deux évite qu'un collider centré sur
    /// le pivot empêche la rotation.
    ///
    /// <para><b>Plus de <c>HingeJoint</c>.</b> Le flipper est tourné par script : l'angle est écrit
    /// directement par <see cref="Flipper"/> et le corps est cinématique. Le joint à ressort qui
    /// équipait ce pivot était instable — mesuré : 359° de balayage pour des butées de ±30°, et un
    /// angle qui passait à <c>NaN</c> dès le 3ᵉ pas physique. La cause tenait au tenseur d'inertie
    /// rendu <b>singulier</b> par les contraintes de rotation gelée ci-dessous.</para>
    ///
    /// <para><b>Ni contraintes de rotation.</b> <c>FreezeRotationX | FreezeRotationY</c> annule
    /// l'inertie de ces axes, ce qui fait diverger tout joint à ressort. C'est le diagnostic qui a
    /// été payé pour être trouvé : ne pas le réintroduire.</para>
    /// </summary>
    private static void EnsureFlipper(string name, Transform parent, int side)
    {
        GameObject existing = FindAnywhere(parent, name);
        bool isNew = existing == null;

        GameObject pivot = EnsureObject(name, parent, null, null);

        if (isNew)
        {
            pivot.transform.localPosition = new Vector3(side * FlipperX, PlayY, FlipperZ);
            pivot.transform.localRotation = Quaternion.identity;
        }

        Rigidbody body = EnsureComponent<Rigidbody>(pivot);

        if (isNew)
        {
            body.useGravity = false;
            body.isKinematic = true;                    // il commande, il ne subit pas
            body.constraints = RigidbodyConstraints.None;
            body.linearDamping = 0.5f;
            body.angularDamping = 1.5f;
            body.mass = 1f;
        }

        EnsureComponent<Flipper>(pivot);

        // La batte s'étend du pivot vers le centre de la table, donc à l'opposé du côté.
        Transform bat = pivot.transform.Find("Flipper_Bat");

        if (bat == null)
        {
            GameObject created = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.name = "Flipper_Bat";
            created.transform.SetParent(pivot.transform, false);
            Undo.RegisterCreatedObjectUndo(created, "Assurer la structure MVP");
            Created.Add(name + "/Flipper_Bat");
            bat = created.transform;

            ApplyMaterial(created.GetComponent<MeshRenderer>(), "FlipperOrange_Mat");
        }

        bat.localPosition = new Vector3(-side * BatOffset, 0f, 0f);
        bat.localRotation = Quaternion.identity;
        bat.localScale = new Vector3(BatLength, 0.12f, 0.22f);
    }

    private static void EnsureSlingshot(string name, Transform parent, float x)
    {
        bool isNew = FindAnywhere(parent, name) == null;

        GameObject host = EnsureObject(name, parent, PrimitiveType.Cube, "Metal_Mat");

        if (isNew)
        {
            host.transform.localPosition = new Vector3(x, 0.50f, -2.50f);
            host.transform.localRotation = Quaternion.identity;
            host.transform.localScale = new Vector3(0.30f, 0.50f, 1.40f);
        }

        // Le cube primitif apporte déjà son BoxCollider : on s'assure seulement qu'il est solide.
        if (isNew && host.GetComponent<Collider>() is Collider slingshotCollider)
        {
            slingshotCollider.isTrigger = false;
        }

        EnsureComponent<Slingshot>(host);
    }

    private static void EnsureBumpers(Transform parent)
    {
        (string name, float x, float z)[] spots =
        {
            ("Bumper_01", -0.05f, 0.55f),
            ("Bumper_02", -1.70f, 1.35f),
            ("Bumper_03", 1.60f, 1.35f),
        };

        foreach ((string name, float x, float z) in spots)
        {
            bool isNew = FindAnywhere(parent, name) == null;

            GameObject host = EnsureObject(name, parent, PrimitiveType.Sphere, "BumperRed_Mat");

            if (isNew)
            {
                host.transform.localPosition = new Vector3(x, 0.62f, z);
                host.transform.localRotation = Quaternion.identity;
                // Le collider d'une sphère primitive suit l'échelle : 0.62 donne un rayon de 0.31.
                host.transform.localScale = Vector3.one * 0.62f;
            }

            if (isNew && host.GetComponent<Collider>() is Collider bumperCollider)
            {
                bumperCollider.isTrigger = false;
            }

            EnsureComponent<Bumper>(host);
        }
    }

    private static void EnsurePlunger(Transform parent)
    {
        bool isNew = FindAnywhere(parent, "Plunger") == null;

        GameObject host = EnsureObject("Plunger", parent, null, null);

        if (isNew)
        {
            host.transform.localPosition = new Vector3(LaneCentreX, 0.46f, -6.30f);
            host.transform.localRotation = Quaternion.identity;
        }

        // Le plateau est incliné (TableGravity, 7°) et le couloir subit la même pente : sans
        // obstacle, la bille recule derrière le lanceur et se coince contre l'apron avant, hors
        // de portée. Sur un vrai flipper, c'est le bout du lanceur qui la retient — d'où ce
        // collider, à la largeur exacte du couloir pour qu'elle ne passe pas sur les côtés.
        bool hadBlock = host.GetComponent<BoxCollider>() != null;
        BoxCollider block = EnsureComponent<BoxCollider>(host);

        if (!hadBlock)
        {
            block.isTrigger = false;
            block.size = new Vector3(LaneWidth, 0.55f, PlungerBlockDepth);
            block.center = Vector3.zero;
        }

        EnsureComponent<Plunger>(host);
    }

    /// <summary>
    /// Zone de sortie : un déclencheur, jamais un collider solide — un solide à cet endroit
    /// empêcherait la bille de tomber et bloquerait la partie.
    /// </summary>
    private static void EnsureDrain(Transform parent)
    {
        bool isNew = FindAnywhere(parent, "DrainZone") == null;

        GameObject host = EnsureObject("DrainZone", parent, null, null);

        if (isNew)
        {
            host.transform.localPosition = new Vector3(0f, DrainLocalCentre.y, DrainLocalCentre.z);
            host.transform.localRotation = Quaternion.identity;
        }

        // Même logique que la réparation : une seule définition de la boîte de drain.
        ApplyDrainBox(host);
        EnsureComponent<DrainZone>(host);
    }

    // ------------------------------------------------------------------------ HUD

    private static void EnsureHud()
    {
        GameObject canvasGo = GameObject.Find("Canvas");
        bool isNew = canvasGo == null;

        if (isNew)
        {
            canvasGo = new GameObject("Canvas");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Assurer la structure MVP");
            Created.Add("Canvas");

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
        }
        else
        {
            Kept.Add("Canvas");
        }

        EnsureComponent<HudController>(canvasGo);

        // Les noms sont ceux que HudController cherche et que BuildPinballTable.LayoutHud repositionne.
        EnsureHudText(canvasGo.transform, "ScoreText", new Vector2(0f, 1f), new Vector2(46f, -46f),
            new Vector2(620f, 90f), 54f, TextAlignmentOptions.Left);

        EnsureHudText(canvasGo.transform, "HighScoreText", new Vector2(1f, 1f), new Vector2(-46f, -46f),
            new Vector2(620f, 90f), 54f, TextAlignmentOptions.Right);

        EnsureHudText(canvasGo.transform, "BallsText", new Vector2(0f, 0f), new Vector2(46f, 46f),
            new Vector2(620f, 90f), 54f, TextAlignmentOptions.Left);

        EnsureHudText(canvasGo.transform, "MissionText", new Vector2(1f, 0f), new Vector2(-46f, 46f),
            new Vector2(760f, 90f), 44f, TextAlignmentOptions.Right);

        EnsureHudText(canvasGo.transform, "MessageText", new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(1400f, 170f), 78f, TextAlignmentOptions.Center);
    }

    /// <summary>
    /// Texte de HUD avec sa mise en page. Le contenu de départ reprend ce que les gestionnaires
    /// afficheront, pour que la scène soit lisible dans l'éditeur sans lancer le jeu.
    /// </summary>
    private static void EnsureHudText(Transform canvas, string name, Vector2 anchor, Vector2 offset,
        Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        Transform existing = canvas.Find(name);

        if (existing != null)
        {
            Kept.Add(name);
            return;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        Undo.RegisterCreatedObjectUndo(go, "Assurer la structure MVP");
        Created.Add(name);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.text = PlaceholderFor(name);
        text.raycastTarget = false;
    }

    private static string PlaceholderFor(string name)
    {
        switch (name)
        {
            case "ScoreText": return "SCORE : 0";
            case "HighScoreText": return "RECORD : 0";
            case "BallsText": return "BILLES : 3";
            default: return string.Empty;
        }
    }

    // -------------------------------------------------------------------- câblage

    /// <summary>
    /// Remplit les références laissées vides sur <see cref="GameManager"/>. Un champ déjà
    /// assigné n'est jamais écrasé : c'est peut-être un réglage volontaire.
    /// </summary>
    /// <remarks>
    /// Réutilisé par <see cref="NeutralScene"/> : la scène neutre crée un <c>GameManager</c>
    /// tout neuf, dont les références de bille sont forcément vides. Sans ce remplissage, le
    /// gestionnaire ne saurait ni où poser la bille ni quel prefab instancier.
    /// </remarks>
    internal static void FillEmptyGameManagerFields()
    {
        GameManager manager = Object.FindFirstObjectByType<GameManager>();

        if (manager == null)
        {
            Debug.LogWarning("[EnsureMvpStructure] Aucun GameManager dans la scène : " +
                             "les références de bille n'ont pas pu être remplies.");
            return;
        }

        SerializedObject serialized = new SerializedObject(manager);
        bool changed = false;

        changed |= AssignIfEmpty(serialized, "ballSpawnPoint", () =>
        {
            GameObject spawn = GameObject.Find("BallSpawnPoint");

            if (spawn == null)
            {
                spawn = EnsureObject("BallSpawnPoint", null, null, null);
                spawn.transform.localPosition = new Vector3(LaneCentreX, 0.60f, -6.60f);
            }

            return spawn.transform;
        });

        changed |= AssignIfEmpty(serialized, "ballPrefab",
            () => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ball.prefab"));

        if (changed)
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
            Debug.Log("[EnsureMvpStructure] GameManager : références de bille complétées " +
                      "(point d'apparition et prefab).");
        }
    }

    private static bool AssignIfEmpty(SerializedObject serialized, string field, System.Func<Object> resolve)
    {
        SerializedProperty property = serialized.FindProperty(field);

        if (property == null || property.objectReferenceValue != null)
        {
            return false;
        }

        Object value = resolve();

        if (value == null)
        {
            Debug.LogWarning($"[EnsureMvpStructure] Champ '{field}' laissé vide : aucune valeur trouvée.");
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }

    // -------------------------------------------------------------------- outillage

    private static GameObject EnsureObject(string name, Transform parent, PrimitiveType? primitive, string materialName)
    {
        GameObject existing = FindAnywhere(parent, name);

        if (existing != null)
        {
            Kept.Add(name);
            return existing;
        }

        GameObject go = primitive.HasValue ? GameObject.CreatePrimitive(primitive.Value) : new GameObject();
        go.name = name;

        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        Undo.RegisterCreatedObjectUndo(go, "Assurer la structure MVP");
        Created.Add(name);

        if (primitive.HasValue)
        {
            ApplyMaterial(go.GetComponent<MeshRenderer>(), materialName);
        }

        return go;
    }

    private static void EnsureObjectWith<T>(string name) where T : Component
    {
        GameObject go = EnsureObject(name, null, null, null);
        EnsureComponent<T>(go);
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();

        if (existing != null)
        {
            return existing;
        }

        return Undo.AddComponent<T>(go);
    }

    /// <summary>
    /// Cherche d'abord sous le parent attendu, puis dans toute la scène : un hôte déplacé à la
    /// main ne doit pas être dupliqué.
    /// </summary>
    private static GameObject FindAnywhere(Transform parent, string name)
    {
        if (parent != null)
        {
            Transform child = parent.Find(name);

            if (child != null)
            {
                return child.gameObject;
            }
        }

        return GameObject.Find(name);
    }

    private static void ApplyMaterial(MeshRenderer renderer, string materialName)
    {
        if (renderer == null || string.IsNullOrEmpty(materialName))
        {
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{materialName}.mat");

        if (material != null)
        {
            renderer.sharedMaterial = material;
        }
    }
}
