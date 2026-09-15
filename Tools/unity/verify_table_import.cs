// Reimporte `Pinball_Table.fbx` et mesure ce qu'Unity en a réellement lu.
//
// Sert à prouver qu'un FBX réengendré est **le même** que celui déjà validé dans la scène :
// même nombre de maillages, mêmes bornes, même empreinte de sommets. L'empreinte est ce qui
// tranche — deux exports peuvent différer d'octets (l'FBX embarque un horodatage) tout en
// portant exactement la même géométrie.
//
// `ImportAsset(..., ForceUpdate)` par chemin, et non `Refresh()` : `Refresh` se fie aux
// horodatages et peut ne rien faire du tout, ce qui donnerait une mesure de l'ancien fichier.

const string path = "Assets/Models/Table/Pinball_Table.fbx";

AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

if (asset == null) { return path + " : INTROUVABLE"; }

var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var contents = PrefabUtility.InstantiatePrefab(asset, preview) as GameObject;

int meshes = 0;
int vertices = 0;
ulong hash = 1469598103934665603UL;   // FNV-1a 64
var bounds = new Bounds();
bool first = true;
var materials = new System.Collections.Generic.HashSet<string>();

foreach (var filter in contents.GetComponentsInChildren<MeshFilter>(true))
{
    var mesh = filter.sharedMesh;

    if (mesh == null) { continue; }

    meshes++;
    var verts = mesh.vertices;
    vertices += verts.Length;

    for (int i = 0; i < verts.Length; i++)
    {
        var v = verts[i];
        hash = (hash ^ (ulong)System.BitConverter.SingleToInt32Bits(v.x)) * 1099511628211UL;
        hash = (hash ^ (ulong)System.BitConverter.SingleToInt32Bits(v.y)) * 1099511628211UL;
        hash = (hash ^ (ulong)System.BitConverter.SingleToInt32Bits(v.z)) * 1099511628211UL;
    }

    var renderer = filter.GetComponent<MeshRenderer>();

    if (renderer != null)
    {
        foreach (var m in renderer.sharedMaterials)
        {
            if (m != null) { materials.Add(m.name.Replace(" (Instance)", "")); }
        }

        // `Renderer.bounds` est déjà en monde, et tient compte de la rotation — contrairement à
        // un `mesh.bounds` transformé à la main : une boîte locale est alignée sur la pièce, pas
        // sur les axes du monde, et la transformer par son centre en gonfle les bornes.
        if (first) { bounds = renderer.bounds; first = false; }
        else { bounds.Encapsulate(renderer.bounds); }
    }
}

UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);

var sb = new System.Text.StringBuilder();
sb.AppendLine("maillages   : " + meshes);
sb.AppendLine("sommets     : " + vertices);
sb.AppendLine("empreinte   : " + hash.ToString("x16"));
sb.AppendLine("bornes      : " + bounds.size.x.ToString("F3") + " × "
              + bounds.size.y.ToString("F3") + " × " + bounds.size.z.ToString("F3") + " u");
sb.AppendLine("x           : " + bounds.min.x.ToString("F3") + " -> " + bounds.max.x.ToString("F3"));
sb.AppendLine("matériaux   : " + materials.Count);
foreach (var m in materials) { sb.AppendLine("              " + m); }

return sb.ToString();
