// ETAT DE L'HOTE PLUNGER — sonde LECTURE SEULE.
//
// Releve, pour chacun des hotes demandes : existence, chemin hierarchique complet, parent,
// index entre freres, transforms locaux ET monde, activeSelf / activeInHierarchy, layer (nom +
// index), tag, liste des composants, champs serialises des composants de gameplay
// (Plunger / DrainZone / Flipper / Bumper), et la liste des enfants avec leur position locale.
//
// Releve aussi l'etat de la scene et les enfants directs de PinballTable/Gameplay.
//
// Aucune ecriture : que des lectures. Pas de GetInstanceID (obsolete en 6000.6).

var sb = new System.Text.StringBuilder();

System.Action<string> w = s => sb.AppendLine(s);

// --- lecture d'une propriete serialisee, quelle que soit sa forme -------------------------------

System.Func<UnityEditor.SerializedProperty, string> val = null;

val = p =>
{
    if (p.isArray)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (int i = 0; i < p.arraySize; i++)
        {
            parts.Add(val(p.GetArrayElementAtIndex(i)));
        }
        return "[" + p.arraySize + "] { " + string.Join(", ", parts) + " }";
    }

    switch (p.propertyType)
    {
        case UnityEditor.SerializedPropertyType.Integer: return p.intValue.ToString();
        case UnityEditor.SerializedPropertyType.Boolean: return p.boolValue.ToString();
        case UnityEditor.SerializedPropertyType.Float: return p.floatValue.ToString("F6");
        case UnityEditor.SerializedPropertyType.String: return "\"" + p.stringValue + "\"";
        case UnityEditor.SerializedPropertyType.Enum:
            return p.enumValueIndex + " (" +
                   (p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length
                        ? p.enumDisplayNames[p.enumValueIndex] : "?") + ")";
        case UnityEditor.SerializedPropertyType.ObjectReference:
            return p.objectReferenceValue == null
                ? "<VIDE>"
                : p.objectReferenceValue.name + " [" + p.objectReferenceValue.GetType().Name + "]";
        case UnityEditor.SerializedPropertyType.Color: return p.colorValue.ToString();
        case UnityEditor.SerializedPropertyType.Vector2: return p.vector2Value.ToString("F4");
        case UnityEditor.SerializedPropertyType.Vector3: return p.vector3Value.ToString("F4");
        case UnityEditor.SerializedPropertyType.Vector4: return p.vector4Value.ToString("F4");
        case UnityEditor.SerializedPropertyType.Quaternion:
            return p.quaternionValue.eulerAngles.ToString("F3");
        case UnityEditor.SerializedPropertyType.LayerMask: return p.intValue.ToString();
        case UnityEditor.SerializedPropertyType.Bounds: return p.boundsValue.ToString("F4");
        default: return "<" + p.propertyType + ">";
    }
};

// --- helpers ------------------------------------------------------------------------------------

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

System.Func<Transform, string> cheminDe = t =>
{
    string chemin = t.name;
    var p = t.parent;
    while (p != null) { chemin = p.name + "/" + chemin; p = p.parent; }
    return chemin;
};

System.Func<string, Transform> trouver = nom =>
{
    foreach (var root in scene.GetRootGameObjects())
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == nom) { return t; }
        }
    }
    return null;
};

string[] typesDeGameplay = { "Plunger", "DrainZone", "Flipper", "Bumper" };

System.Action<string, Transform> dump = (etiquette, t) =>
{
    w("");
    w("--- " + etiquette + " ---");

    if (t == null)
    {
        w("  ABSENT de la scene.");
        return;
    }

    var go = t.gameObject;

    w("  chemin complet  : " + cheminDe(t));
    w("  parent          : " + (t.parent != null ? t.parent.name + "  (" + cheminDe(t.parent) + ")" : "<RACINE>"));
    w("  index / freres  : " + t.GetSiblingIndex() + " / " + (t.parent != null ? t.parent.childCount : 0));

    w("  localPosition   : " + t.localPosition.ToString("F6"));
    w("  localEulerAngles: " + t.localEulerAngles.ToString("F4"));
    w("  localRotation   : " + t.localRotation.ToString("F6"));
    w("  localScale      : " + t.localScale.ToString("F6"));

    w("  position monde  : " + t.position.ToString("F6"));
    w("  rotation monde  : " + t.rotation.eulerAngles.ToString("F4"));
    w("  perte d'echelle : " + t.lossyScale.ToString("F6"));

    w("  activeSelf      : " + go.activeSelf + "   activeInHierarchy : " + go.activeInHierarchy);
    w("  layer           : " + go.layer + " (" + UnityEngine.LayerMask.LayerToName(go.layer) + ")");
    w("  tag             : " + go.tag);
    w("  statique (flag) : " + go.isStatic);

    // --- composants ---
    var composants = go.GetComponents<Component>();
    w("  composants (" + composants.Length + ") :");
    foreach (var c in composants)
    {
        w("      " + (c == null ? "<MANQUANT / script introuvable>" : c.GetType().FullName));
    }

    // --- champs serialises des composants de gameplay ---
    foreach (var c in composants)
    {
        if (c == null) { continue; }

        string nomType = c.GetType().Name;
        bool interesse = false;
        foreach (var n in typesDeGameplay) { if (nomType == n) { interesse = true; } }
        if (!interesse) { continue; }

        w("      ~~ champs serialises de " + nomType + " (valeurs de la SCENE) ~~");

        var so = new UnityEditor.SerializedObject(c);
        var it = so.GetIterator();
        bool premier = true;

        while (it.NextVisible(premier))
        {
            premier = false;
            if (it.name == "m_Script") { continue; }
            w("         " + it.name.PadRight(30) + " = " + val(it));
        }
    }

    // --- colliders ---
    var colsSelf = go.GetComponents<Collider>();
    var colsTout = go.GetComponentsInChildren<Collider>(true);
    w("  colliders sur l'hote : " + colsSelf.Length + "   dans la sous-hierarchie : " + colsTout.Length);

    foreach (var c in colsSelf)
    {
        string detail = "-";
        if (c is BoxCollider bc) { detail = "box center=" + bc.center.ToString("F4") + " size=" + bc.size.ToString("F4"); }
        else if (c is SphereCollider sc) { detail = "sphere center=" + sc.center.ToString("F4") + " r=" + sc.radius.ToString("F4"); }
        else if (c is CapsuleCollider cc) { detail = "capsule center=" + cc.center.ToString("F4") + " r=" + cc.radius.ToString("F4") + " h=" + cc.height.ToString("F4"); }
        else if (c is MeshCollider mc) { detail = "mesh=" + (mc.sharedMesh != null ? mc.sharedMesh.name : "<null>") + " convex=" + mc.convex; }

        w("      " + c.GetType().Name.PadRight(16) + " trigger=" + c.isTrigger + "  " + detail);
    }

    w("  Rigidbody sur l'hote : " + (go.GetComponent<Rigidbody>() != null)
      + "   dans la sous-hierarchie : " + go.GetComponentsInChildren<Rigidbody>(true).Length);

    // --- enfants directs (nom + position locale) ---
    w("  enfants directs (" + t.childCount + ") :");
    foreach (Transform e in t)
    {
        w("      [" + e.GetSiblingIndex() + "] " + e.name.PadRight(28)
          + " localPos=" + e.localPosition.ToString("F5")
          + "  localScale=" + e.localScale.ToString("F5")
          + "  actif=" + e.gameObject.activeInHierarchy
          + "  colliders=" + e.GetComponents<Collider>().Length
          + "  renderers=" + e.GetComponents<Renderer>().Length
          + "  composants=[" + string.Join(",", System.Array.ConvertAll(e.GetComponents<Component>(),
                c => c == null ? "<MANQUANT>" : c.GetType().Name)) + "]");
    }
};

// ================================================================================================
// PARTIE 1
// ================================================================================================

w("=== PARTIE 1 : etat des hotes ===");

dump("Plunger", trouver("Plunger"));
dump("BallSpawnPoint", trouver("BallSpawnPoint"));
dump("DrainZone", trouver("DrainZone"));
dump("Flipper_Left_Pivot", trouver("Flipper_Left_Pivot"));
dump("Bumper_01", trouver("Bumper_01"));

var pinballTable = trouver("PinballTable");
dump("PinballTable/Gameplay", pinballTable != null ? pinballTable.transform.Find("Gameplay") : null);
dump("PinballTable/Table", pinballTable != null ? pinballTable.transform.Find("Table") : null);

// ================================================================================================
// PARTIE 2 : etat de la scene
// ================================================================================================

w("");
w("=== PARTIE 2 : etat de la scene ===");
w("  name     : " + scene.name);
w("  path     : " + scene.path);
w("  isDirty  : " + scene.isDirty);
w("  isLoaded : " + scene.isLoaded);
w("  rootCount: " + scene.rootCount);

w("");
w("  racines de la scene :");
foreach (var root in scene.GetRootGameObjects())
{
    w("      " + root.name.PadRight(24)
      + " actif=" + root.activeInHierarchy
      + "  localPos=" + root.transform.localPosition.ToString("F4")
      + "  localEuler=" + root.transform.localEulerAngles.ToString("F3")
      + "  enfant(s)=" + root.transform.childCount);
}

var gameplay = pinballTable != null ? pinballTable.transform.Find("Gameplay") : null;

w("");
if (gameplay == null)
{
    w("  PinballTable/Gameplay ABSENT.");
}
else
{
    w("  enfants directs de PinballTable/Gameplay (" + gameplay.childCount + ") :");
    foreach (Transform e in gameplay)
    {
        w("      [" + e.GetSiblingIndex() + "] " + e.name.PadRight(28)
          + " type=[" + string.Join(",", System.Array.ConvertAll(e.GetComponents<Component>(),
                c => c == null ? "<MANQUANT>" : c.GetType().Name)) + "]"
          + "  localPos=" + e.localPosition.ToString("F5"));
    }
}

// ================================================================================================
// PARTIE 2 bis : complets des hôtes — ce que d'autres hotes nommes portent
// ================================================================================================

w("");
w("=== complets : hôtes attendus absents ===");

string[] attendus =
{
    "Flipper_Left_Pivot", "Flipper_Right_Pivot", "Flipper_Upper_Pivot",
    "Slingshot_Left", "Slingshot_Right",
    "Bumper_01", "Bumper_02", "Bumper_03",
    "Target_Programmation", "Target_Reseau", "Target_Web", "Target_BDD", "Target_Projet",
    "Ramp_Vosges_In", "Ramp_Vosges_Out", "Ramp_IUT_In", "Ramp_IUT_Out",
    "Loop_At_Entry", "Loop_At_Exit", "Door_IUT", "Boss_ProjetFinal",
    "Plunger", "BallSpawnPoint", "Ball", "DrainZone",
};

foreach (var nom in attendus)
{
    var t = trouver(nom);
    w("  " + nom.PadRight(24) + (t == null ? "ABSENT" : cheminDe(t)));
}

// ================================================================================================
// PARTIE 4 : la bille posee et ce que les gestionnaires referencent
// ================================================================================================

w("");
w("=== PARTIE 4 : bille posee et references des gestionnaires ===");

var balle = trouver("Ball");

if (balle == null)
{
    w("  aucune bille nommee 'Ball'.");
}
else
{
    w("  chemin         : " + cheminDe(balle));
    w("  position monde : " + balle.position.ToString("F6"));
    w("  position locale: " + balle.localPosition.ToString("F6"));
    w("  localScale     : " + balle.localScale.ToString("F6"));
    w("  layer / tag    : " + balle.gameObject.layer + " (" + UnityEngine.LayerMask.LayerToName(balle.gameObject.layer) + ") / " + balle.tag);
    w("  actif          : activeSelf=" + balle.gameObject.activeSelf + " activeInHierarchy=" + balle.gameObject.activeInHierarchy);

    var rb = balle.GetComponent<Rigidbody>();
    if (rb != null)
    {
        w("  Rigidbody      : masse " + rb.mass + "  cinematic " + rb.isKinematic
          + "  useGravity " + rb.useGravity + "  detection " + rb.collisionDetectionMode);
    }

    var sph = balle.GetComponent<SphereCollider>();
    if (sph != null) { w("  SphereCollider : rayon " + sph.radius.ToString("F6") + " (x echelle " + balle.lossyScale.x.ToString("F4") + " = " + (sph.radius * balle.lossyScale.x).ToString("F6") + " u)"); }

    w("  ecart avec BallSpawnPoint (monde) : "
      + (trouver("BallSpawnPoint") != null
            ? UnityEngine.Vector3.Distance(balle.position, trouver("BallSpawnPoint").position).ToString("F6")
            : "n/a"));
}

System.Action<string, UnityEngine.Object, string[]> dumpManager = (titre, comp, champs) =>
{
    if (comp == null)
    {
        w("  " + titre + " : ABSENT de la scene.");
        return;
    }

    w("  " + titre + " sur '" + comp.name + "' :");
    var so = new UnityEditor.SerializedObject(comp);
    var it = so.GetIterator();
    bool premier = true;

    while (it.NextVisible(premier))
    {
        premier = false;
        if (it.name == "m_Script") { continue; }
        bool demande = champs.Length == 0;
        foreach (var c in champs) { if (it.name == c) { demande = true; } }
        if (!demande) { continue; }
        w("      " + it.name.PadRight(26) + " = " + val(it));
    }
};

dumpManager("GameManager", UnityEngine.Object.FindFirstObjectByType<GameManager>(), new string[0]);
dumpManager("BallManager", UnityEngine.Object.FindFirstObjectByType<BallManager>(), new string[0]);
dumpManager("InputRouter", UnityEngine.Object.FindFirstObjectByType<InputRouter>(), new string[0]);

// ================================================================================================
// PARTIE 5 : quelles pieces importees existent, pour habiller les hotes
// ================================================================================================

w("");
w("=== PARTIE 5 : assets de pieces disponibles (Assets/Models/Parts) ===");

string[] dossiers = { "Flipper", "Bumpers", "Miscellaneous", "Posts", "Lane_Guides", "Switches" };

foreach (var d in dossiers)
{
    string chemin = "Assets/Models/Parts/" + d;
    string[] fichiers = System.IO.Directory.Exists(chemin)
        ? System.IO.Directory.GetFiles(chemin, "*.fbx")
        : new string[0];

    w("  " + chemin + " -> " + fichiers.Length + " fbx");

    foreach (var f in fichiers)
    {
        w("      " + System.IO.Path.GetFileName(f));
    }
}

return sb.ToString();
