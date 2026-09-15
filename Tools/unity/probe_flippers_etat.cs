// SONDE (lecture seule) — état réel des flippers dans la scène active, pour décider d'une pose.
//
// Relève, sans rien écrire :
//  1. les deux hôtes (`Flipper_Left_Pivot` / `Flipper_Right_Pivot`) : position monde + locale,
//     rotation, et l'état du composant `Flipper` (angles, côté, ressort, limites du joint) ;
//  2. l'enfant `Flipper_Bat` : existe-t-il, et quelle est la boîte des maillages portés,
//     exprimée dans le repère du pivot (c'est ce repère qui décide du sens du talon) ;
//  3. le bas de table : où sont les slingshots, le drain, le point d'apparition, la bille —
//     c'est-à-dire les cotes auxquelles un flipper doit s'accorder.

var sb = new System.Text.StringBuilder();

var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
sb.AppendLine("=== scène '" + scene.name + "'  (" + scene.path + ") ===");

var racine = GameObject.Find("PinballTable");
if (racine == null) { return "PinballTable introuvable."; }

var gameplay = racine.transform.Find("Gameplay");
var table = racine.transform.Find("Table");

sb.AppendLine("PinballTable : pos " + racine.transform.position.ToString("F4")
              + "  rot " + racine.transform.eulerAngles.ToString("F2"));
sb.AppendLine("Gameplay     : " + (gameplay != null ? "✓" : "ABSENT")
              + "   Table : " + (table != null ? "✓" : "ABSENT"));
if (gameplay == null) { return sb.ToString(); }

// --- 1. les hôtes ------------------------------------------------------------------------------

foreach (var nom in new string[] { "Flipper_Left_Pivot", "Flipper_Right_Pivot" })
{
    sb.AppendLine();
    sb.AppendLine("### " + nom);

    var t = gameplay.Find(nom);

    if (t == null) { sb.AppendLine("  ABSENT"); continue; }

    sb.AppendLine("  position monde   " + t.position.ToString("F4"));
    sb.AppendLine("  position locale  " + t.localPosition.ToString("F4"));
    sb.AppendLine("  rotation monde   " + t.eulerAngles.ToString("F2")
                  + "   locale " + t.localEulerAngles.ToString("F2"));
    sb.AppendLine("  forward (monde)  " + t.forward.ToString("F4"));
    sb.AppendLine("  right   (monde)  " + t.right.ToString("F4"));
    sb.AppendLine("  up      (monde)  " + t.up.ToString("F4"));

    var comp = t.GetComponent("Flipper");

    if (comp != null)
    {
        var so = new UnityEditor.SerializedObject(comp);
        var it = so.GetIterator();

        if (it.NextVisible(true))
        {
            do
            {
                if (it.propertyType == UnityEditor.SerializedPropertyType.Generic) { continue; }

                string v;

                switch (it.propertyType)
                {
                    case UnityEditor.SerializedPropertyType.Float:
                        v = it.floatValue.ToString("F3"); break;
                    case UnityEditor.SerializedPropertyType.Integer:
                        v = it.intValue.ToString(); break;
                    case UnityEditor.SerializedPropertyType.Boolean:
                        v = it.boolValue ? "true" : "false"; break;
                    case UnityEditor.SerializedPropertyType.String:
                        v = it.stringValue; break;
                    case UnityEditor.SerializedPropertyType.Enum:
                        v = it.enumDisplayNames.Length > it.enumValueIndex && it.enumValueIndex >= 0
                            ? it.enumDisplayNames[it.enumValueIndex] : "?"; break;
                    case UnityEditor.SerializedPropertyType.ObjectReference:
                        v = it.objectReferenceValue != null ? it.objectReferenceValue.name : "null";
                        break;
                    default: v = "(" + it.propertyType + ")"; break;
                }

                sb.AppendLine("    " + it.name + " = " + v);
            } while (it.NextVisible(false));
        }
    }
    else { sb.AppendLine("    (pas de composant Flipper)"); }

    var joint = t.GetComponent<HingeJoint>();

    if (joint != null)
    {
        sb.AppendLine("    HINGE axis(local) " + joint.axis.ToString("F4")
                      + "  useLimits " + joint.useLimits
                      + "  limites [" + joint.limits.min.ToString("F1") + " ; " + joint.limits.max.ToString("F1") + "]"
                      + "  useSpring " + joint.useSpring
                      + "  spring " + joint.spring.spring + "/" + joint.spring.damper
                      + "  connectedBody " + (joint.connectedBody != null ? joint.connectedBody.name : "null"));
    }
    else { sb.AppendLine("    (pas de HingeJoint)"); }

    var corps = t.GetComponent<Rigidbody>();
    sb.AppendLine("    Rigidbody : " + (corps != null
        ? "masse " + corps.mass.ToString("F3") + "  kinematic " + corps.isKinematic
          + "  gravité " + corps.useGravity + "  drag " + corps.linearDamping
        : "ABSENT"));

    // --- 2. le bat ------------------------------------------------------------------------
    sb.AppendLine("  enfants : " + t.childCount);

    for (int i = 0; i < t.childCount; i++)
    {
        var e = t.GetChild(i);

        // Les huit coins de la boîte des maillages, ramenés dans le repère du pivot : c'est
        // la mesure qui dit où tombent les extrémités du bat dans les axes du joint.
        var boite = new Bounds(Vector3.zero, Vector3.zero);
        bool premier = true;

        foreach (var f in e.GetComponentsInChildren<MeshFilter>())
        {
            if (f.sharedMesh == null) { continue; }

            var b = f.sharedMesh.bounds;

            for (int k = 0; k < 8; k++)
            {
                var p = t.InverseTransformPoint(f.transform.TransformPoint(new Vector3(
                    (k & 1) == 0 ? b.min.x : b.max.x,
                    (k & 2) == 0 ? b.min.y : b.max.y,
                    (k & 4) == 0 ? b.min.z : b.max.z)));

                if (premier) { boite = new Bounds(p, Vector3.zero); premier = false; }
                else { boite.Encapsulate(p); }
            }
        }

        sb.AppendLine("    [" + i + "] '" + e.name + "'  local " + e.localPosition.ToString("F4")
                      + "  rot " + e.localEulerAngles.ToString("F2")
                      + "  échelle " + e.localScale.ToString("F4")
                      + "  maillages " + e.GetComponentsInChildren<MeshFilter>().Length
                      + "  colliders " + e.GetComponentsInChildren<Collider>().Length);

        if (!premier)
        {
            sb.AppendLine("        boîte (repère pivot)  centre " + boite.center.ToString("F4")
                          + "  taille " + boite.size.ToString("F4")
                          + "   x ∈ [" + boite.min.x.ToString("F4") + " ; " + boite.max.x.ToString("F4") + "]"
                          + "  y ∈ [" + boite.min.y.ToString("F4") + " ; " + boite.max.y.ToString("F4") + "]"
                          + "  z ∈ [" + boite.min.z.ToString("F4") + " ; " + boite.max.z.ToString("F4") + "]");

            // Extrémités en monde, pour comparer à la géométrie réelle de la table.
            var pMin = t.TransformPoint(boite.center - new Vector3(boite.extents.x, 0f, 0f));
            var pMax = t.TransformPoint(boite.center + new Vector3(boite.extents.x, 0f, 0f));
            sb.AppendLine("        extrémités (monde)    A " + pMin.ToString("F4") + "   B " + pMax.ToString("F4"));
        }
    }
}

// --- 3. le bas de table ------------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("### bas de table");

void Poser(string chemin)
{
    var g = GameObject.Find(chemin);
    sb.AppendLine("  " + chemin.PadRight(56) + (g != null
        ? g.transform.position.ToString("F4") + "   rot " + g.transform.eulerAngles.ToString("F2")
        : "ABSENT"));
}

foreach (var c in new string[]
{
    "PinballTable/Gameplay/BallSpawnPoint",
    "PinballTable/Gameplay/Ball",
    "PinballTable/Gameplay/Plunger",
    "PinballTable/Gameplay/DrainZone",
    "PinballTable/Gameplay/Slingshot_Left",
    "PinballTable/Gameplay/Slingshot_Right",
    "PinballTable/Gameplay/Bumper_01",
    "PinballTable/Table/Pinball_Table/Slingshots/Body_L",
    "PinballTable/Table/Pinball_Table/Slingshots/Body_R",
    "PinballTable/Table/Pinball_Table/Playfield",
    "PinballTable/Table/Pinball_Table/Walls",
    "PinballTable/Table/Pinball_Table/Orbit",
})
{
    Poser(c);
}

// Le contenu direct de `Pinball_Table` : c'est là que sont les pièces du dépôt (bumpers,
// cibles, poteaux), et savoir comment elles sont groupées dit où poser les flippers.
var importee = GameObject.Find("PinballTable/Table/Pinball_Table");

if (importee != null)
{
    sb.AppendLine();
    sb.AppendLine("### PinballTable/Table/Pinball_Table — " + importee.transform.childCount + " enfants");
    sb.AppendLine("  échelle racine " + importee.transform.localScale.ToString("F4"));

    for (int i = 0; i < importee.transform.childCount; i++)
    {
        var e = importee.transform.GetChild(i);
        var b = new Bounds();
        bool premier = true;

        foreach (var r in e.GetComponentsInChildren<Renderer>())
        {
            if (premier) { b = r.bounds; premier = false; }
            else { b.Encapsulate(r.bounds); }
        }

        for (int j = 0; j < e.childCount && j < 8; j++)
        {
            sb.AppendLine("      ." + e.GetChild(j).name);
        }

        sb.AppendLine("  [" + i.ToString("D2") + "] " + e.name.PadRight(34)
                      + (premier ? "  (aucun renderer)"
                                 : "  centre " + b.center.ToString("F3") + "  taille " + b.size.ToString("F3"))
                      + "   colliders " + e.GetComponentsInChildren<Collider>().Length);
    }
}

// La bille : le mètre-étalon de toutes les cotes.
var bille = GameObject.Find("PinballTable/Gameplay/Ball");

if (bille != null)
{
    var col = bille.GetComponent<Collider>();
    sb.AppendLine();
    sb.AppendLine("### bille : échelle " + bille.transform.localScale.ToString("F4")
                  + "   collider " + (col != null ? col.bounds.size.ToString("F4") : "aucun"));
}

// Le CLI tronque les longues sorties : le rapport part sur disque, et la valeur de retour
// ne sert qu'à dire où.
var chemin = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "unity", "out",
                                    "flippers_etat.txt");
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(chemin));
System.IO.File.WriteAllText(chemin, sb.ToString());

return "rapport écrit : " + System.IO.Path.GetFullPath(chemin) + "  (" + sb.Length + " car.)";
