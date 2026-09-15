// Ajoute les layers du GDD §Tags et layers, et pose la bille sur le sien.
//
// Le GDD demande quatre layers : `Ball`, `Table`, `TableElement`, `Environment`. Aucun n'existe
// (`TagManager.asset` n'a que Default, TransparentFX, Ignore Raycast, Water, UI). Ils servent à
// la matrice de collision : c'est ce qui permettra plus tard de faire traverser le décor par la
// bille, ou d'ignorer les collisions entre pièces.
//
// Poser la bille sur `Ball` tout de suite est sans risque : la matrice de collision d'Unity
// laisse **tout** se croiser par défaut, donc rien ne change physiquement tant que quelqu'un ne
// décoche pas une case. En revanche, le faire maintenant évite d'oublier une pièce plus tard.
//
// Le prefab est modifié par `PrefabUtility`, jamais à la main dans le YAML : les identifiants y
// sont attribués par l'éditeur, et une écriture manuelle serait invisible tant que l'éditeur ne
// rechargerait pas le fichier.

var report = new System.Text.StringBuilder();

// --- layers ---------------------------------------------------------------------------------

string[] wanted = { "Ball", "Table", "TableElement", "Environment" };

var tagManager = new SerializedObject(
    AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
var layers = tagManager.FindProperty("layers");

report.AppendLine("=== layers ===");

int next = 8;   // 0-7 sont réservés par Unity

foreach (string name in wanted)
{
    int found = -1;

    for (int i = 0; i < layers.arraySize; i++)
    {
        if (layers.GetArrayElementAtIndex(i).stringValue == name) { found = i; break; }
    }

    if (found >= 0)
    {
        report.AppendLine("  " + name.PadRight(14) + " déjà présent à l'index " + found);
        continue;
    }

    bool placed = false;

    while (next < layers.arraySize)
    {
        var slot = layers.GetArrayElementAtIndex(next);

        if (string.IsNullOrEmpty(slot.stringValue))
        {
            slot.stringValue = name;
            report.AppendLine("  " + name.PadRight(14) + " ajouté à l'index " + next);
            next++;
            placed = true;
            break;
        }

        next++;
    }

    if (!placed)
    {
        report.AppendLine("  " + name.PadRight(14) + " ÉCHEC : plus d'emplacement libre");
    }
}

tagManager.ApplyModifiedPropertiesWithoutUndo();
AssetDatabase.SaveAssets();

// --- la bille sur son layer -----------------------------------------------------------------

report.AppendLine();
report.AppendLine("=== prefab de la bille ===");

const string ballPath = "Assets/Prefabs/Ball.prefab";

int ballLayer = LayerMask.NameToLayer("Ball");

if (ballLayer < 0)
{
    report.AppendLine("  layer 'Ball' introuvable : prefab laissé tel quel");
    return report.ToString();
}

var root = PrefabUtility.LoadPrefabContents(ballPath);

if (root == null)
{
    report.AppendLine("  " + ballPath + " introuvable");
    return report.ToString();
}

report.AppendLine("  avant : layer " + root.layer + " (" + LayerMask.LayerToName(root.layer) + ")");

bool changed = root.layer != ballLayer;
root.layer = ballLayer;

foreach (var child in root.GetComponentsInChildren<Transform>(true))
{
    if (child != root.transform && child.gameObject.layer != ballLayer)
    {
        child.gameObject.layer = ballLayer;
        changed = true;
    }
}

if (changed)
{
    PrefabUtility.SaveAsPrefabAsset(root, ballPath);
    report.AppendLine("  après : layer " + root.layer + " (" + LayerMask.LayerToName(root.layer) + ")   → enregistré");
}
else
{
    report.AppendLine("  déjà sur le bon layer : rien à écrire");
}

PrefabUtility.UnloadPrefabContents(root);

// --- relecture, pour constater plutôt que supposer ------------------------------------------

report.AppendLine();
report.AppendLine("=== relecture ===");

var verify = AssetDatabase.LoadAssetAtPath<GameObject>(ballPath);

report.AppendLine("  layer du prefab  : " + verify.layer
                  + " (" + LayerMask.LayerToName(verify.layer) + ")");
report.AppendLine("  tag du prefab    : " + verify.tag);

var collider = verify.GetComponent<SphereCollider>();

if (collider != null)
{
    report.AppendLine("  collider         : rayon " + collider.radius.ToString("F4")
                      + "   échelle racine " + verify.transform.localScale.x.ToString("F4")
                      + "   → rayon réel " + (collider.radius * verify.transform.localScale.x).ToString("F4") + " u");
    report.AppendLine("  matériau physique: "
                      + (collider.sharedMaterial != null ? collider.sharedMaterial.name : "AUCUN"));
    report.AppendLine("  déclencheur      : " + collider.isTrigger);
}

var body = verify.GetComponent<Rigidbody>();

if (body != null)
{
    report.AppendLine("  rigidbody        : masse " + body.mass
                      + "   drag " + body.linearDamping.ToString("F2")
                      + "   angular " + body.angularDamping.ToString("F2")
                      + "   détection " + body.collisionDetectionMode
                      + "   interpolation " + body.interpolation
                      + "   gravité " + body.useGravity);
}

return report.ToString();
