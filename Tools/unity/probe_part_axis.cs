// Le marqueur de `probe_axis.py`, mesuré dans Unity.
//
// Il est fait pour trancher une question que la table ne peut pas trancher seule : Unity
// miroite-t-il **tout** FBX Blender, ou seulement celui-là ? La réponse décide du sort des
// 19 pièces de `Assets/Models/Parts/`, exportées avec le même couple d'axes par
// `export_parts.py`.
//
// En repère de table, la boîte va de x 0,10 à 0,30 — franchement du côté positif. Si Unity
// annonce x 0,10 -> 0,30, il n'y a pas de miroir générique et les pièces sont saines. S'il
// annonce -0,30 -> -0,10, le miroir est général et les pièces chirales (flippers, supports)
// sont retournées.

const string path = "Assets/Temp/axisprobe/marker_minusZ_Y.fbx";

AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

if (asset == null) { return path + " : INTROUVABLE"; }

var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var contents = PrefabUtility.InstantiatePrefab(asset, preview) as GameObject;

var filters = contents.GetComponentsInChildren<MeshFilter>(true);
var sb = new System.Text.StringBuilder();

sb.AppendLine("maillages " + filters.Length);

foreach (var filter in filters)
{
    var mesh = filter.sharedMesh;

    if (mesh == null) { continue; }

    var vertices = mesh.vertices;
    float minX = float.MaxValue, maxX = float.MinValue;
    float minY = float.MaxValue, maxY = float.MinValue;
    float minZ = float.MaxValue, maxZ = float.MinValue;

    for (int i = 0; i < vertices.Length; i++)
    {
        var v = vertices[i];
        if (v.x < minX) { minX = v.x; }
        if (v.x > maxX) { maxX = v.x; }
        if (v.y < minY) { minY = v.y; }
        if (v.y > maxY) { maxY = v.y; }
        if (v.z < minZ) { minZ = v.z; }
        if (v.z > maxZ) { maxZ = v.z; }
    }

    sb.AppendLine("  " + filter.name + " : sommets natifs x " + minX.ToString("F3")
                  + " -> " + maxX.ToString("F3")
                  + "   y " + minY.ToString("F3") + " -> " + maxY.ToString("F3")
                  + "   z " + minZ.ToString("F3") + " -> " + maxZ.ToString("F3"));
}

var renderers = contents.GetComponentsInChildren<Renderer>(true);

if (renderers.Length > 0)
{
    var bounds = renderers[0].bounds;

    for (int i = 1; i < renderers.Length; i++) { bounds.Encapsulate(renderers[i].bounds); }

    sb.AppendLine("  bounds monde x " + bounds.min.x.ToString("F3")
                  + " -> " + bounds.max.x.ToString("F3")
                  + "   y " + bounds.min.y.ToString("F3") + " -> " + bounds.max.y.ToString("F3")
                  + "   z " + bounds.min.z.ToString("F3") + " -> " + bounds.max.z.ToString("F3"));
}

UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);

return sb.ToString();
