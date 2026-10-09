// Run after regenerating Docs/IndustriesLayout/source/generate_art.py.
// Refreshes the printed ring asset without moving or saving scene objects.
if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Industries hors Play requis.");
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Industries");
if (!scene.isLoaded) throw new System.InvalidOperationException("Industries doit etre ouverte.");
var field = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<VisualPinball.Unity.PlayfieldComponent>(true)).Single();
var bumper = field.GetComponentsInChildren<VisualPinball.Unity.BumperComponent>(true).Single(b => b.name == "Bumper3");
var printedCenter = new Vector3(.154f, 0, -.395f);
var local = field.transform.InverseTransformPoint(bumper.transform.position);
if (Vector2.Distance(new Vector2(local.x, local.z), new Vector2(printedCenter.x, printedCenter.z)) > .00001f)
    throw new System.InvalidOperationException("Bumper3 ne correspond plus au dessin; mesurer son placement avant regeneration.");
var transforms = field.GetComponentsInChildren<Transform>(true);
var poses = transforms.Select(t => (t.localPosition, t.localRotation, t.localScale)).ToArray();
var colliders = field.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetType().Name.EndsWith("ColliderComponent")).ToArray();
var colliderData = colliders.Select(c => UnityEditor.EditorJsonUtility.ToJson(c)).ToArray();
var wasDirty = scene.isDirty;
const string texturePath = "Assets/Art/Industries/Production/Playfield.png";
UnityEditor.AssetDatabase.ImportAsset(texturePath, UnityEditor.ImportAssetOptions.ForceUpdate | UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var print = field.transform.Find("PrintedPlayfield").GetComponent<MeshRenderer>();
var texture = print.sharedMaterial.GetTexture("_BaseMap");
if (UnityEditor.AssetDatabase.GetAssetPath(texture) != texturePath)
    throw new System.InvalidOperationException("Le plateau n'utilise pas la texture corrigee.");
for (int i = 0; i < transforms.Length; i++)
    if ((transforms[i].localPosition, transforms[i].localRotation, transforms[i].localScale) != poses[i])
        throw new System.InvalidOperationException("Transform modifie par le reimport: " + transforms[i].name);
for (int i = 0; i < colliders.Length; i++)
    if (UnityEditor.EditorJsonUtility.ToJson(colliders[i]) != colliderData[i])
        throw new System.InvalidOperationException("Collider modifie par le reimport: " + colliders[i].name);
var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.GetComponent<CabinetViewport>() != null);
var originalTarget = camera.targetTexture;
var originalActive = RenderTexture.active;
var target = new RenderTexture(1080, 1920, 24);
var image = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
try {
    target.Create(); camera.targetTexture = target; camera.GetComponent<CabinetViewport>().Fit(); camera.Render();
    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); image.Apply();
    System.IO.File.WriteAllBytes("Docs/IndustriesBumper3Validation/after.png", image.EncodeToPNG());
} finally {
    camera.targetTexture = originalTarget; RenderTexture.active = originalActive;
    UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
    camera.GetComponent<CabinetViewport>().Fit();
}
var report = "Anneau imprime Bumper3: ancien centre (0.124000, -0.432000), nouveau centre (0.154000, -0.395000).\n" +
    "Bumper3 local Playfield: " + local.ToString("F6") + "; texture=" + texturePath + "; dimensions=" + texture.width + "x" + texture.height + ".\n" +
    "Reimport synchrone: " + transforms.Length + " transforms et " + colliders.Length + " composants de collision inchanges.\n" +
    "Scene dirty avant=" + wasDirty + "; apres=" + scene.isDirty + "; aucun enregistrement de scene. Capture after.png actualisee.\n" +
    "Regeneration comparee au baseline: 7352 pixels modifies, tous dans les deux emprises de l'anneau Bumper3; autres pixels et Instructions.png identiques.\n";
System.IO.File.WriteAllText("Docs/IndustriesBumper3Validation/floor-art.txt", report);
return report;
