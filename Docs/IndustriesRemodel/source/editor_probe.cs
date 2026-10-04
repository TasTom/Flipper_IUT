#pragma warning disable CS0162
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var table=UnityEngine.GameObject.Find("ExampleTable");var pf=table.transform.Find("Playfield");var root=pf.Find("WorkshopRemodel");
var report=new System.Text.StringBuilder();
void Check(string label,bool value){report.AppendLine((value?"PASS ":"FAIL ")+label);}
Check("Industries hors Play, corrections non enregistrées",scene.name=="Industries"&&!UnityEditor.EditorApplication.isPlaying&&scene.isDirty);
Check("Huit carters 3D et portique présents",root!=null&&root.GetComponentsInChildren<UnityEngine.Transform>(true).Count(t=>t.name.StartsWith("Carter_"))==8&&root.Find("FittedCarters/FactoryGantry")!=null);
Check("Aucun collider PhysX ajouté au nouvel habillage",root.GetComponentsInChildren<UnityEngine.Collider>(true).Length==0);
var caps=table.GetComponentsInChildren<VisualPinball.Unity.BumperComponent>(true).Select(b=>b.transform.Find("Cap")).ToArray();
Check("Cinq manomètres suivent les chapeaux natifs",caps.Length==5&&caps.All(c=>c.Find("PressureDial")!=null&&c.Find("CopperBezel")!=null));
Check("Deux flippers utilisent le même matériau, sans logos du template",table.GetComponentsInChildren<VisualPinball.Unity.FlipperComponent>(true).All(f=>f.transform.Find("Base").GetComponent<UnityEngine.Renderer>().sharedMaterial.name=="WorkshopIvory"));
Check("Les six indicateurs de cibles restent présents",table.GetComponentsInChildren<UnityEngine.Transform>(true).Count(t=>t.name=="TargetNumber")==6);
// TextMeshPro génère son mesh au chargement ; seuls les meshes de modèle doivent être des assets.
Check("Maillages et matériaux persistants",root.GetComponentsInChildren<UnityEngine.MeshFilter>(true).Where(f=>f.GetComponent<TMPro.TMP_Text>()==null).All(f=>UnityEditor.AssetDatabase.Contains(f.sharedMesh))&&root.GetComponentsInChildren<UnityEngine.MeshRenderer>(true).Where(r=>r.GetComponent<TMPro.TMP_Text>()==null).All(r=>r.sharedMaterials.All(m=>UnityEditor.AssetDatabase.Contains(m))));
var count=table.GetComponentsInChildren<UnityEngine.Transform>(true).Length;
RemodelIndustriesPresentation.Apply();
Check("Relance sans doublons ni remplacement des réglages existants",count==table.GetComponentsInChildren<UnityEngine.Transform>(true).Length);
Check("Portique ajusté au cadrage de borne",UnityEngine.Vector3.Distance(root.Find("GantryName").localPosition,new UnityEngine.Vector3(.258f,.145f,-.2f))<.000001f);
int missing=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<UnityEngine.Transform>(true).Sum(t=>UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
Check("Aucun script manquant",missing==0);
System.IO.File.WriteAllText(System.IO.Path.GetFullPath("Docs/IndustriesRemodel/editor.txt"),report.ToString());
if(report.ToString().Contains("FAIL"))throw new System.Exception(report.ToString());
return report.ToString();
