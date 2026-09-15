// Mesure ce qu'Unity lit réellement de `Ball.fbx`, avant d'en faire le prefab de la bille.
//
// Trois choses à constater plutôt qu'à supposer :
//
// 1. **La taille.** L'ancre d'échelle du projet est la bille elle-même : 27 mm réels pour
//    0,450 unité Unity de diamètre. C'est cette mesure qui valide le facteur 16,667 unités/m.
//    CLAUDE.md prévient que l'importeur laisse une échelle de racine ≈ 1666 sur un FBX Blender
//    (signature normale de `useFileScale`, pas une erreur) — mais une racine à 1666 n'est
//    correcte que si le maillage sous-jacent est en centimètres. On lit donc les deux.
//
// 2. **La forme.** Une bille est peut-être une hiérarchie (la sonde `probe_append.py` a montré
//    que `Plunger` en est une). Un collider posé sur la racine d'une hiérarchie ne prendrait
//    que le premier maillage.
//
// 3. **Le maillage.** Nombre de sommets et de triangles : une bille de 27 mm n'a pas besoin de
//    plus de quelques centaines de triangles, et un maillage trop lourd est un coût gratuit à
//    chaque `Instantiate`.

const string path = "Assets/Models/Parts/Miscellaneous/Ball.fbx";

AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

if (asset == null) { return path + " : INTROUVABLE"; }

var sb = new System.Text.StringBuilder();

// --- le prefab importé, tel que Unity le présente -------------------------------------------

sb.AppendLine("=== prefab importé ===");
sb.AppendLine("racine        : " + asset.name + "   échelle " + asset.transform.localScale);

foreach (var t in asset.GetComponentsInChildren<Transform>(true))
{
    string depth = new string(' ', t.parent == null ? 0 : 2);
    sb.AppendLine("  " + depth + t.name
                  + "   type " + (t.GetComponent<MeshFilter>() != null ? "maillage" : "vide")
                  + "   échelle " + t.localScale
                  + "   pos " + t.localPosition);
}

// --- la géométrie ---------------------------------------------------------------------------

var filters = asset.GetComponentsInChildren<MeshFilter>(true);
int vertices = 0;
int triangles = 0;

sb.AppendLine();
sb.AppendLine("=== géométrie ===");
sb.AppendLine("maillages     : " + filters.Length);

foreach (var filter in filters)
{
    var mesh = filter.sharedMesh;

    if (mesh == null) { continue; }

    vertices += mesh.vertexCount;
    triangles += mesh.triangles.Length / 3;

    sb.AppendLine("  " + mesh.name + " : " + mesh.vertexCount + " sommets, "
                  + (mesh.triangles.Length / 3) + " triangles, "
                  + "rayon local " + mesh.bounds.extents.ToString("F4"));
}

sb.AppendLine("total         : " + vertices + " sommets, " + triangles + " triangles");

// --- les bornes en monde, avec l'échelle de l'importeur appliquée ---------------------------

var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var contents = PrefabUtility.InstantiatePrefab(asset, preview) as GameObject;
var renderers = contents.GetComponentsInChildren<Renderer>(true);

if (renderers.Length > 0)
{
    var bounds = renderers[0].bounds;

    for (int i = 1; i < renderers.Length; i++) { bounds.Encapsulate(renderers[i].bounds); }

    sb.AppendLine();
    sb.AppendLine("=== bornes monde ===");
    sb.AppendLine("taille        : " + bounds.size.ToString("F4") + " u");
    sb.AppendLine("centre        : " + bounds.center.ToString("F4"));
    sb.AppendLine("rayon         : " + (bounds.extents.x).ToString("F4")
                  + " / " + bounds.extents.y.ToString("F4")
                  + " / " + bounds.extents.z.ToString("F4") + " u");

    float expected = 0.450f;
    float measured = bounds.size.x;
    sb.AppendLine("attendu       : 0,4500 u (27 mm à 16,667 u/m)   écart "
                  + ((measured - expected) * 1000f).ToString("F2") + " millièmes");
}

// --- matériaux ------------------------------------------------------------------------------

var materials = new System.Collections.Generic.HashSet<string>();

foreach (var renderer in renderers)
{
    foreach (var m in renderer.sharedMaterials)
    {
        if (m != null) { materials.Add(m.name + "  (" + (m.shader != null ? m.shader.name : "?") + ")"); }
    }
}

sb.AppendLine();
sb.AppendLine("=== matériaux ===");

foreach (var m in materials) { sb.AppendLine("  " + m); }

UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);

// --- le prefab attendu existe-t-il déjà ? ---------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== attendu par le projet ===");
sb.AppendLine("Assets/Prefabs/Ball.prefab : "
              + (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ball.prefab") != null
                 ? "EXISTE DÉJÀ" : "absent"));

string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
sb.AppendLine("prefabs dans Assets/Prefabs : " + prefabGuids.Length);

foreach (var guid in prefabGuids)
{
    sb.AppendLine("  " + AssetDatabase.GUIDToAssetPath(guid));
}

// --- layers ---------------------------------------------------------------------------------

sb.AppendLine();
sb.AppendLine("=== layers du projet ===");

var tagManager = new SerializedObject(
    AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
var layers = tagManager.FindProperty("layers");

for (int i = 0; i < layers.arraySize; i++)
{
    string name = layers.GetArrayElementAtIndex(i).stringValue;

    if (!string.IsNullOrEmpty(name)) { sb.AppendLine("  " + i + "  " + name); }
}

return sb.ToString();
