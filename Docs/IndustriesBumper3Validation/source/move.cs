if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Industries hors Play requis pour conserver le placement.");
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Industries");
if (!scene.isLoaded) throw new System.InvalidOperationException("La scene Industries doit etre ouverte.");
var field = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<VisualPinball.Unity.PlayfieldComponent>(true)).Single();
var bumper = field.GetComponentsInChildren<VisualPinball.Unity.BumperComponent>(true).Single(b => b.name == "Bumper3");
var lamps = field.GetComponentsInChildren<VisualPinball.Unity.LightComponent>(true).Where(l => l.name.Equals("b3l1",StringComparison.OrdinalIgnoreCase) || l.name.Equals("b3l2",StringComparison.OrdinalIgnoreCase)).Select(l => l.transform).Where(t => !t.IsChildOf(bumper.transform)).ToArray();
if (lamps.Length != 2) throw new System.InvalidOperationException("Les deux lampes du bumper sont requises.");
var camera = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Single(c=>c.GetComponent<CabinetViewport>() != null);
var originalTarget = camera.targetTexture; var originalActive = RenderTexture.active;
System.IO.Directory.CreateDirectory("Docs/IndustriesBumper3Validation");
void Capture(string name)
{
    var target = new RenderTexture(1080,1920,24);
    var image = new Texture2D(1080,1920,TextureFormat.RGB24,false);
    try {
        target.Create(); camera.targetTexture=target; camera.GetComponent<CabinetViewport>().Fit(); camera.Render();
        RenderTexture.active=target; image.ReadPixels(new Rect(0,0,1080,1920),0,0); image.Apply();
        System.IO.File.WriteAllBytes("Docs/IndustriesBumper3Validation/"+name+".png", image.EncodeToPNG());
    } finally {
        camera.targetTexture=originalTarget; RenderTexture.active=originalActive; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image); camera.GetComponent<CabinetViewport>().Fit();
    }
}
var oldLocal = field.transform.InverseTransformPoint(bumper.transform.position);
var targetLocal = new Vector3(.154f, oldLocal.y, -.395f);
if ((oldLocal-targetLocal).sqrMagnitude < 0.0000000001f) return "Placement deja applique; aucun changement.";
if ((oldLocal-new Vector3(.124f,0,-.432f)).sqrMagnitude > .00000001f && (oldLocal-new Vector3(.138f,0,-.394f)).sqrMagnitude > .00000001f && (oldLocal-new Vector3(.154f,0,-.394f)).sqrMagnitude > .00000001f) throw new System.InvalidOperationException("Placement de Bumper3 modifie depuis le diagnostic; mesurer a nouveau avant application.");
if (!System.IO.File.Exists("Docs/IndustriesBumper3Validation/before.png")) Capture("before");
UnityEditor.Undo.IncrementCurrentGroup(); int group = UnityEditor.Undo.GetCurrentGroup();
UnityEditor.Undo.SetCurrentGroupName("Industries : remonter Bumper3 et ses lampes");
var oldWorld = bumper.transform.position;
UnityEditor.Undo.RecordObject(bumper.transform,"Remonter Bumper3"); bumper.transform.position=field.transform.TransformPoint(targetLocal);
UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(bumper.transform);
var delta=bumper.transform.position-oldWorld;
foreach (var lamp in lamps) { UnityEditor.Undo.RecordObject(lamp,"Suivre Bumper3"); lamp.position += delta; UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(lamp); }
var halo=field.transform.Find("IndustriesArcadeLighting/Halo_Bumper3");
if (halo!=null) {
    var hp=field.transform.InverseTransformPoint(halo.position);hp.x=targetLocal.x;hp.z=targetLocal.z;
    UnityEditor.Undo.RecordObject(halo,"Suivre Bumper3 avec halo");halo.position=field.transform.TransformPoint(hp);UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(halo);
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.Undo.CollapseUndoOperations(group);
Capture("after");
var report = "Bumper3 local Playfield " + oldLocal.ToString("F6") + " -> " + targetLocal.ToString("F6") + "; delta monde="+delta.ToString("F6")+"; lampes="+lamps.Length+"; scene non enregistree; Undo disponible.";
System.IO.File.WriteAllText("Docs/IndustriesBumper3Validation/placement.txt",report);
return report;
