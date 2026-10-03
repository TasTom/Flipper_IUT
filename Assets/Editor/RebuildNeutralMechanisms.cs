using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit user-authorized layout revision. Undoable, idempotent, never saves the scene.</summary>
public static class RebuildNeutralMechanisms
{
    const string Folder = "Assets/Generated/NeutralMechanisms";
    const string Action = "Agencement, scoop, aimant et ball lock de Neutral";
    static Transform table, root, gameplay, lamps;
    static PinballMechanismConfig config;
    static Material steel, clear, liner, roof, dark, lampMaterial;
    static PhysicsMaterial surface;
    static int meshIndex;
    static readonly Vector3 Hole = new Vector3(-1.05f, 0, 11.55f);
    const float HoleRadius = .33f;
    public static void Queue() { EditorApplication.update -= Run; EditorApplication.update += Run; }
    static void Run() { EditorApplication.update -= Run; try { Apply(); } catch(Exception e) { Debug.LogException(e); } }
    public static void RepairAperture()
    {
        table=GameObject.Find("PinballTable").transform;root=table.Find("Rails/MechanismRevision");
        if(root==null || Application.isPlaying)throw new InvalidOperationException("Révision en Edit Mode requise.");
        steel=root.Find("Scoop_Hole_Wall").GetComponent<Renderer>().sharedMaterial;
        surface=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Generated/NeutralPresentation/RampSurface.physicMaterial");
        foreach(var name in new[]{"PrintedPlayfield_WithScoop","Playfield_WithScoop","Scoop_Hole_Wall"}) {
            var go=root.Find(name).gameObject;var path=AssetDatabase.GetAssetPath(go.GetComponent<MeshFilter>().sharedMesh);
            if(!path.StartsWith(Folder+"/",StringComparison.Ordinal))throw new InvalidOperationException("Asset hors périmètre.");
            Undo.DestroyObjectImmediate(go);AssetDatabase.DeleteAsset(path);
        }
        meshIndex=32;CutPlayfield();AssetDatabase.SaveAssets();Physics.SyncTransforms();
    }
    [MenuItem("Pinball/Neutral/Agencer les rampes et ajouter les mécanismes")]
    public static void Apply()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/Neutral.unity") throw new InvalidOperationException("Neutral en Edit Mode requise.");
        table = GameObject.Find("PinballTable").transform; gameplay = table.Find("Gameplay");
        if (table.Find("Rails/MechanismRevision") != null) { Debug.Log("Mécanismes déjà installés ; réglages conservés."); return; }
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated", "NeutralMechanisms");
        config = AssetDatabase.LoadAssetAtPath<PinballMechanismConfig>(Folder + "/MechanismConfig.asset");
        if (config == null) { config = ScriptableObject.CreateInstance<PinballMechanismConfig>(); AssetDatabase.CreateAsset(config, Folder + "/MechanismConfig.asset"); }
        var old = table.Find("ReferencePlayfield/FlowRefinement/CorrectedRoutes");
        var mats = old.Find("IUT_Track").GetComponent<Renderer>().sharedMaterials; clear = mats[0]; liner = mats[1];
        steel = old.Find("IUT_Rim_1").GetComponent<Renderer>().sharedMaterial;
        roof = old.Find("IUT_Cover").GetComponent<Renderer>().sharedMaterial;
        dark = old.Find("IUT_UndercutGuard").GetComponent<Renderer>().sharedMaterial;
        surface = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Generated/NeutralPresentation/RampSurface.physicMaterial");
        lampMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/LampLens.mat");
        if (lampMaterial == null) { lampMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); lampMaterial.SetColor("_BaseColor", new Color(.4f,.5f,.48f)); lampMaterial.SetFloat("_Smoothness", .6f); lampMaterial.EnableKeyword("_EMISSION"); lampMaterial.SetColor("_EmissionColor", Color.white * .001f); lampMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; AssetDatabase.CreateAsset(lampMaterial,Folder + "/LampLens.mat"); }
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(Action);
        try
        {
            var rails = table.Find("Rails"); if (rails == null) rails = New("Rails", table);
            root = New("MechanismRevision", rails); meshIndex = 0;
            foreach (Transform tr in old) if (tr.name.StartsWith("Vosges_") || tr.name.StartsWith("IUT_")) {
                foreach (var r in tr.GetComponentsInChildren<Renderer>(true)) { Undo.RecordObject(r,Action); r.enabled = false; }
                foreach (var c in tr.GetComponentsInChildren<Collider>(true)) { Undo.RecordObject(c,Action); c.enabled = false; }
            }
            var paths = new RefineNeutralFlow.Routes { routes = new[]{"Vosges","IUT"}.Select(name=>{
                var track = old.Find(name+"_Track"); var vertices = track.GetComponent<MeshCollider>().sharedMesh.vertices;
                return new RefineNeutralFlow.Route { name=name, points=Enumerable.Range(0,vertices.Length/8).Select(i=>table.InverseTransformPoint(track.TransformPoint((vertices[i*8+4]+vertices[i*8+5])*.5f))).ToArray() };
            }).ToArray() };
            var vosges = new List<Vector3>();
            Bezier(vosges, P(-2.15f,0,11.25f), P(-2.65f,0,12.60f), P(-2.80f,.80f,14.20f), P(-2.60f,1.20f,15.30f));
            Bezier(vosges, vosges.Last(), P(-2.65f,1.26f,15.80f), P(-2.05f,1.26f,16.20f), paths.routes.First(r=>r.name=="Vosges").points[64]);
            vosges.AddRange(paths.routes.First(r=>r.name=="Vosges").points.Skip(65));
            var iut = new List<Vector3>();
            Bezier(iut, P(.25f,0,11.15f), P(.40f,0,12.15f), P(1.35f,.70f,13.35f), P(1.35f,1.32f,14.30f));
            Bezier(iut, iut.Last(), P(1.35f,1.60f,15.45f), P(.55f,1.70f,15.90f), P(-.50f,1.70f,15.15f));
            Bezier(iut, iut.Last(), P(-1.60f,1.65f,14.40f), P(-2.96f,1.48f,11.90f), paths.routes.First(r=>r.name=="IUT").points[96]);
            iut.AddRange(paths.routes.First(r=>r.name=="IUT").points.Skip(97));
            Ramp("Vosges", vosges, 88); Ramp("IUT", iut, 64); MoveSpinner();
            CutPlayfield();
            var scoop = Scoop(); var magnet = Magnet();
            var tableLights = table.Find("TableLights"); if (tableLights == null) tableLights = New("TableLights",table);
            lamps = New("LogicalGroups",tableLights);
            var vgroup = LampGroup("Ramp_Vosges",new Color(.20f,.8f,.48f),new[]{vosges[0]+P(0,.02f,-.75f),vosges[0]+P(0,.02f,-1.15f),vosges[0]+P(0,.02f,-1.55f)});
            var igroup = LampGroup("Ramp_IUT",new Color(.22f,.66f,1),new[]{iut[0]+P(0,.02f,-.75f),iut[0]+P(0,.02f,-1.15f),iut[0]+P(0,.02f,-1.55f)});
            var scoopGroup = LampGroup("Scoop",new Color(1,.75f,.25f),new[]{Hole+P(0,.02f,-1.05f)});
            var lockGroup = LampGroup("BallLock",new Color(1,.35f,.12f),new[]{Hole+P(-.3f,.02f,-1.45f),Hole+P(.3f,.02f,-1.45f)});
            var magnetGroup = LampGroup("Magnet",new Color(.5f,.3f,1),new[]{magnet.transform.localPosition+P(-.35f,.02f,0),magnet.transform.localPosition+P(.35f,.02f,0)});
            Assign(magnet,"lamps",magnetGroup);
            var loopGroup = LampGroup("Loop",new Color(.22f,.8f,.8f),new[]{P(-3.68f,.02f,12.7f),P(2.95f,.02f,12.7f)});
            var targetNames = new[]{"Matieres_01","Matieres_02","Matieres_03","Java_01","Projet_01","Cafe_01"};
            var targets = targetNames.Select(n=>gameplay.Find("Target_"+n).GetComponent<SubjectTarget>()).ToArray();
            var targetGroups = targets.Select((t,i)=> {
                var insert=t.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name=="RewardInsert");
                var p=table.InverseTransformPoint(insert!=null?insert.bounds.center:t.transform.position);p.y=.02f;p.z+=.34f;
                return LampGroup("Reward_"+targetNames[i],i<3?new Color(.9f,.65f,.2f):new Color(.2f,.75f,.8f),new[]{p});
            }).ToArray();
            var giL = LampGroup("GI_Left",new Color(1,.83f,.58f),Enumerable.Range(0,8).Select(i=>P(-3.98f,.23f,6.7f+i*1.16f)).ToArray(),true);
            var giR = LampGroup("GI_Right",new Color(.65f,.84f,1),Enumerable.Range(0,8).Select(i=>P(3.25f,.23f,6.7f+i*1.16f)).ToArray(),true);
            var giBack = LampGroup("GI_Back",new Color(1,.83f,.58f),Enumerable.Range(0,7).Select(i=>P(-2.8f+i*.8f,.28f,17.05f)).ToArray(),true);
            Label("LockLegend","SCOOP / LOCK",Hole+P(0,.026f,-1.86f),1.3f);
            Label("MagnetLegend","AIMANT • CAFÉ",magnet.transform.localPosition+P(0,.026f,-.54f),1.15f);
            var director = Undo.AddComponent<PinballLightDirector>(New("LightDirector",tableLights).gameObject);
            AssignArray(director,"targets",targets); AssignArray(director,"targetGroups",targetGroups); AssignArray(director,"rampExits",new[]{gameplay.Find("Ramp_Vosges_Out").GetComponent<RampGate>(),gameplay.Find("Ramp_IUT_Out").GetComponent<RampGate>()}); AssignArray(director,"rampGroups",new[]{vgroup,igroup});
            Assign(director,"loop",gameplay.Find("Loop_At_Exit").GetComponent<LoopGate>()); Assign(director,"loopGroup",loopGroup); Assign(director,"lockGroup",lockGroup); Assign(director,"scoopGroup",scoopGroup); Assign(director,"scoop",scoop); Assign(director,"magnet",magnet); Assign(director,"ballLock",scoop.GetComponent<BallLock>()); AssignArray(director,"multiballGroups",new[]{vgroup,igroup,loopGroup,giL,giR,giBack});
            AssetDatabase.SaveAssets(); Physics.SyncTransforms(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            System.IO.Directory.CreateDirectory("Tools/unity/out"); System.IO.File.WriteAllText("Tools/unity/out/mechanism-paths.json",JsonUtility.ToJson(new RefineNeutralFlow.Routes{routes=new[]{new RefineNeutralFlow.Route{name="Vosges",points=vosges.ToArray()},new RefineNeutralFlow.Route{name="IUT",points=iut.ToArray()}}},true));
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
        finally { Undo.CollapseUndoOperations(undo); }
    }
    static Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);
    static Transform New(string name,Transform parent) { var go=new GameObject(name); if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(go,Action); go.transform.SetParent(parent,false); return go.transform; }
    static void Assign(UnityEngine.Object target,string name,UnityEngine.Object value) { Undo.RecordObject(target,Action);var so=new SerializedObject(target);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedProperties(); }
    static void AssignArray(UnityEngine.Object target,string name,IEnumerable<UnityEngine.Object> values) { Undo.RecordObject(target,Action);var so=new SerializedObject(target);var p=so.FindProperty(name);var a=values.ToArray();p.arraySize=a.Length;for(int i=0;i<a.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=a[i];so.ApplyModifiedProperties(); }
    static void Bezier(List<Vector3> p,Vector3 a,Vector3 b,Vector3 c,Vector3 d) { for(int i=p.Count==0?0:1;i<=32;i++){float t=i/32f,u=1-t;p.Add(u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d);} }
    static Vector3 Normal(IList<Vector3> p,int i) { var d=p[Math.Min(i+1,p.Count-1)]-p[Math.Max(0,i-1)]; return P(d.z,0,-d.x).normalized; }
    static float Half(int i) { float t=Mathf.Clamp01(i/14f);return Mathf.Lerp(.45f,.3036f,t*t*(3-2*t)); }
    static GameObject MeshObject(string name,List<Vector3> v,List<int> t,Material mat,bool physical,Vector2[] uv=null)
    {
        if(v.Any(p=>float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z)))throw new InvalidOperationException("Sommets non finis : "+name);
        var source=new Mesh{name=name};source.SetVertices(v);source.SetTriangles(t,0);source.RecalculateNormals();source.RecalculateBounds();
        if(uv!=null){source.uv=uv;}else {var mapped=InstallNeutralSurfaceCoordinates.BoxMap(source);UnityEngine.Object.DestroyImmediate(source);source=mapped;source.name=name;}
        source.RecalculateTangents();
        if(!Application.isPlaying){AssetDatabase.CreateAsset(source,Folder+"/"+(meshIndex++).ToString("D3")+"_"+name+".asset");Undo.RegisterCreatedObjectUndo(source,Action);}
        var go=New(name,root).gameObject;go.AddComponent<MeshFilter>().sharedMesh=source;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
        if(physical){var col=go.AddComponent<MeshCollider>();col.sharedMesh=source;col.sharedMaterial=surface;var so=new SerializedObject(source);var bake=so.FindProperty("m_PreBakeTriangleCollisionMesh");if(bake!=null){bake.boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();}}
        return go;
    }
    static void Sweep(string name,IList<Vector3> p,Func<int,Vector2[]> section,Material mat,bool physical)
    {
        var v=new List<Vector3>();var t=new List<int>();int k=section(0).Length;
        for(int i=0;i<p.Count;i++)foreach(var s in section(i))v.Add(p[i]+Normal(p,i)*s.x+Vector3.up*s.y);
        for(int i=0;i<p.Count-1;i++)for(int j=0;j<k;j++){int a=i*k+j,b=i*k+(j+1)%k;t.AddRange(new[]{a,b,b+k,a,b+k,a+k});}
        var caps=k==8?new[]{0,1,4,0,4,5,1,2,3,1,3,4,0,5,6,0,6,7}:new[]{0,1,2,0,2,3};for(int i=0;i<caps.Length;i+=3){t.AddRange(new[]{caps[i+2],caps[i+1],caps[i]});int end=(p.Count-1)*k;t.AddRange(new[]{end+caps[i],end+caps[i+1],end+caps[i+2]});}
        MeshObject(name,v,t,mat,physical);
    }
    static void Tube(string name,IEnumerable<Vector3> points,float radius,Material mat)
    {
        var p=points.ToList();var v=new List<Vector3>();var t=new List<int>();const int k=10;
        for(int i=0;i<p.Count;i++){var n=Normal(p,i);if(n.sqrMagnitude<.1f)n=Vector3.right;var d=(p[Math.Min(i+1,p.Count-1)]-p[Math.Max(0,i-1)]).normalized;var up=Vector3.Cross(d,n).normalized;for(int j=0;j<k;j++){float a=j*Mathf.PI*2/k;v.Add(p[i]+radius*(n*Mathf.Cos(a)+up*Mathf.Sin(a)));}}
        for(int i=0;i<p.Count-1;i++)for(int j=0;j<k;j++){int a=i*k+j,b=i*k+(j+1)%k;t.AddRange(new[]{a,b,b+k,a,b+k,a+k});}MeshObject(name,v,t,mat,false);
    }
    static void Ramp(string name,List<Vector3> p,int wireStart)
    {
        Sweep(name+"_Track",p,i=>{float h=Half(i),o=h+.0368f;return new[]{new Vector2(-o,-.0276f),new Vector2(o,-.0276f),new Vector2(o,.3312f),new Vector2(h,.3312f),new Vector2(h,0),new Vector2(-h,0),new Vector2(-h,.3312f),new Vector2(-o,.3312f)};},clear,true);
        var track=root.Find(name+"_Track");var m=track.GetComponent<MeshFilter>().sharedMesh;var all=m.triangles;var first=new List<int>();var second=new List<int>();var verts=m.vertices;
        for(int i=0;i<all.Length;i+=3){var center=(verts[all[i]]+verts[all[i+1]]+verts[all[i+2]])/3;int nearest=Enumerable.Range(0,p.Count).OrderBy(j=>(p[j]-center).sqrMagnitude).First();(nearest<wireStart?first:second).AddRange(new[]{all[i],all[i+1],all[i+2]});}
        m.subMeshCount=2;m.SetTriangles(first,0);m.SetTriangles(second,1);EditorUtility.SetDirty(m);track.GetComponent<Renderer>().sharedMaterials=new[]{clear,liner};
        var roofPath=p.Select((point,i)=>{var d=p[Math.Min(i+1,p.Count-1)]-p[Math.Max(0,i-1)];float rise=.55f*Mathf.Sqrt(1+d.y*d.y/Mathf.Max(.00001f,d.x*d.x+d.z*d.z));return point+Vector3.up*rise;}).ToArray();
        Sweep(name+"_Cover",roofPath,i=>new[]{new Vector2(-Half(i)-.0368f,0),new Vector2(Half(i)+.0368f,0),new Vector2(Half(i)+.0368f,.0184f),new Vector2(-Half(i)-.0368f,.0184f)},roof,true);
        var guardV=new List<Vector3>();var guardT=new List<int>();for(int i=0;i<p.Count;i++){var n=Normal(p,i);var l=p[i]-n*(Half(i)+.0368f);var r=p[i]+n*(Half(i)+.0368f);guardV.Add(P(l.x,-.012f,l.z));guardV.Add(P(r.x,-.012f,r.z));guardV.Add(r-Vector3.up*.0276f);guardV.Add(l-Vector3.up*.0276f);}for(int i=0;i<p.Count-1;i++)if(p[i].y<.72f||p[i+1].y<.72f)for(int j=0;j<4;j++){int a=i*4+j,b=i*4+(j+1)%4;guardT.AddRange(new[]{a,b,b+4,a,b+4,a+4});}MeshObject(name+"_UndercutGuard",guardV,guardT,dark,true);
        foreach(int side in new[]{-1,1}){Tube(name+"_Rim_"+side,p.Select((v,i)=>v+Normal(p,i)*side*(Half(i)+.0184f)+Vector3.up*.336f),.0193f,steel);Tube(name+"_Runner_"+side,p.Skip(wireStart).Select((v,i)=>v+Normal(p,i+wireStart)*side*.1472f-Vector3.up*.0166f),.023f,steel);Tube(name+"_MidRail_"+side,p.Skip(wireStart).Select((v,i)=>v+Normal(p,i+wireStart)*side*.322f+Vector3.up*.1748f),.0175f,steel);}
        for(int i=wireStart;i<p.Count-3;i+=12){var n=Normal(p,i);Tube(name+"_Brace_"+i,new[]{p[i]-n*.35f+Vector3.up*.36f,p[i]-n*.35f-Vector3.up*.046f,p[i]+n*.35f-Vector3.up*.046f,p[i]+n*.35f+Vector3.up*.36f},.0138f,steel);}
        var entry=gameplay.Find("Ramp_"+name+"_In");Undo.RecordObject(entry,Action);entry.position=table.TransformPoint(p[2]+Vector3.up*.25f);var dir=p[4]-p[0];dir.y=0;entry.rotation=table.rotation*Quaternion.LookRotation(dir);
        var marker=table.Find("ReferencePlayfield/Shot_"+name);if(marker!=null){Undo.RecordObject(marker,Action);marker.position=table.TransformPoint(p[0]+P(0,.028f,-.6f));}
        if(marker==null)Label("RampLabel_"+name,name.ToUpperInvariant(),p[0]+P(0,.026f,-2.02f),1.45f);
    }
    static void CutPlayfield()
    {
        var floor=table.GetComponentsInChildren<BoxCollider>().First(c=>c.name=="Playfield");Undo.RecordObject(floor,Action);floor.enabled=false;var renderer=floor.GetComponent<Renderer>();if(renderer!=null){Undo.RecordObject(renderer,Action);renderer.enabled=false;}
        var printed=table.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="PrintedPlayfield");Undo.RecordObject(printed.GetComponent<Renderer>(),Action);printed.GetComponent<Renderer>().enabled=false;
        var angles=Enumerable.Range(0,48).Select(i=>i*Mathf.PI*2/48).Concat(new[]{P(-4.2865f,0,1)-Hole,P(4.2865f,0,1)-Hole,P(4.2865f,0,17.78f)-Hole,P(-4.2865f,0,17.78f)-Hole}.Select(v=>Mathf.Repeat(Mathf.Atan2(v.z,v.x),Mathf.PI*2))).OrderBy(a=>a).ToArray();
        var v=new List<Vector3>();var t=new List<int>();
        foreach(float a in angles){var d=P(Mathf.Cos(a),0,Mathf.Sin(a));float tx=Mathf.Abs(d.x)<.000001f?float.PositiveInfinity:(d.x>0?4.2865f-Hole.x:-4.2865f-Hole.x)/d.x;float tz=Mathf.Abs(d.z)<.000001f?float.PositiveInfinity:(d.z>0?17.78f-Hole.z:1-Hole.z)/d.z;v.Add(Hole+d*HoleRadius);v.Add(Hole+d*Mathf.Min(tx,tz));}
        int count=angles.Length;for(int i=0;i<count;i++){int a=i*2,b=((i+1)%count)*2;t.AddRange(new[]{a,b+1,a+1,a,b,b+1});}
        var visual=v.Select(p=>p+Vector3.up*.008f).ToList();var uv=visual.Select(p=>new Vector2(Mathf.InverseLerp(-4.285f,4.285f,p.x),Mathf.InverseLerp(1,17.77f,p.z))).ToArray();
        MeshObject("PrintedPlayfield_WithScoop",visual,t,printed.GetComponent<Renderer>().sharedMaterial,false,uv);
        var physical = MeshObject("Playfield_WithScoop",v,t,renderer.sharedMaterial,true);
        physical.GetComponent<MeshCollider>().sharedMaterial = floor.sharedMaterial;
        var walls=new List<Vector3>();var tri=new List<int>();for(int i=0;i<count;i++){var p=v[i*2];walls.Add(p);walls.Add(p-Vector3.up*.5f);}for(int i=0;i<count;i++){int a=i*2,b=((i+1)%count)*2;tri.AddRange(new[]{a,a+1,b,a+1,b+1,b});}MeshObject("Scoop_Hole_Wall",walls,tri,steel,true);
    }
    static GameObject Part(string name,string path,Transform parent,Vector3 position)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new InvalidOperationException("Pièce introuvable : "+path);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);Undo.RegisterCreatedObjectUndo(go,Action);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale*=.92f;
        foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(steel,r.sharedMaterials.Length).ToArray();return go;
    }
    static PinballScoop Scoop()
    {
        var host=New("Scoop_Semestre",gameplay);host.localPosition=Hole;
        var part=Part("Scoop_Professional","Assets/Models/Parts/Kickers/Scoop_1.fbx",host,P(0,0,.15f));part.transform.localRotation=Quaternion.Euler(0,180,0)*part.transform.localRotation;foreach(var mf in part.GetComponentsInChildren<MeshFilter>()){var col=Undo.AddComponent<MeshCollider>(mf.gameObject);col.sharedMesh=mf.sharedMesh;col.sharedMaterial=surface;}
        var sensor=Undo.AddComponent<BoxCollider>(host.gameObject);sensor.isTrigger=true;sensor.center=P(0,-.35f,0);sensor.size=P(.47f,.25f,.47f);
        var seat=New("Seat",host);seat.localPosition=P(0,-.45f,0);
        var eject=New("Eject",host);eject.localPosition=P(0,.25f,-.85f);eject.localRotation=Quaternion.LookRotation(P(-.18f,.23f,-1));
        var scoop=Undo.AddComponent<PinballScoop>(host.gameObject);var ballLock=Undo.AddComponent<BallLock>(host.gameObject);
        Assign(scoop,"config",config);Assign(scoop,"seat",seat);Assign(scoop,"ejectPoint",eject);Assign(scoop,"ballLock",ballLock);
        var slots=new[]{New("LockSlot_1",host),New("LockSlot_2",host)};slots[0].localPosition=P(0,-.8f,.35f);slots[1].localPosition=P(0,-1.3f,.35f);
        var manager=UnityEngine.Object.FindAnyObjectByType<MultiballManager>();var game=UnityEngine.Object.FindAnyObjectByType<GameManager>();var prefab=(GameObject)new SerializedObject(game).FindProperty("ballPrefab").objectReferenceValue;
        Assign(ballLock,"config",config);AssignArray(ballLock,"storage",slots);Assign(ballLock,"scoop",scoop);Assign(ballLock,"ballPrefab",prefab);Assign(ballLock,"replacement",gameplay.Find("BallSpawnPoint"));Assign(ballLock,"multiball",manager);Assign(manager,"ballLock",ballLock);
        return scoop;
    }
    static void MoveSpinner()
    {
        var spinner=table.GetComponentsInChildren<Spinner>().FirstOrDefault();if(spinner==null)return;
        var host=spinner.transform.parent;var old=table.InverseTransformPoint(host.position);var target=P(-3.67f,old.y,12.6f);var delta=table.TransformVector(target-old);
        Undo.RecordObject(host,Action);host.position+=delta;
        var joint=spinner.GetComponent<HingeJoint>();if(joint!=null&&joint.connectedBody==null){Undo.RecordObject(joint,Action);joint.connectedAnchor=spinner.transform.TransformPoint(joint.anchor);}
        var bracket=table.Find("ReferencePlayfield/ProfessionalArt/SpinnerMount");if(bracket!=null){Undo.RecordObject(bracket,Action);bracket.position+=delta;}
    }
    static PlayfieldMagnet Magnet()
    {
        var host=New("Magnet_Cafe",gameplay);host.localPosition=P(-.4f,0,9.05f);
        Part("Magnet_Core_Professional","Assets/Models/Parts/Miscellaneous/Magnet_Core.fbx",host,P(0,.002f,0));
        var magnet=Undo.AddComponent<PlayfieldMagnet>(host.gameObject);Assign(magnet,"config",config);Assign(magnet,"tableFrame",table);return magnet;
    }
    static PinballLightGroup LampGroup(string name,Color color,Vector3[] positions,bool gi=false)
    {
        var host=New(name,lamps);var renderers=new List<Renderer>();
        foreach(var p in positions){var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Undo.RegisterCreatedObjectUndo(go,Action);go.name="LampLens";go.transform.SetParent(host,false);go.transform.localPosition=p;go.transform.localScale=P(gi?.09f:.18f,.004f,gi?.09f:.18f);Undo.DestroyObjectImmediate(go.GetComponent<Collider>());var r=go.GetComponent<Renderer>();r.sharedMaterial=lampMaterial;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color*.32f);block.SetColor("_EmissionColor",color*.22f);r.SetPropertyBlock(block);renderers.Add(r);}
        var group=Undo.AddComponent<PinballLightGroup>(host.gameObject);Assign(group,"config",config);AssignArray(group,"emitters",renderers);var so=new SerializedObject(group);so.FindProperty("color").colorValue=color;so.FindProperty("generalIllumination").boolValue=gi;so.FindProperty("phase").floatValue=lamps.childCount*.6f;so.ApplyModifiedProperties();return group;
    }
    static void Label(string name,string value,Vector3 position,float size)
    {
        var host=New(name,root);host.localPosition=position;host.localRotation=Quaternion.Euler(90,0,0);var text=Undo.AddComponent<TextMeshPro>(host.gameObject);text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");text.text=value;text.fontSize=size;text.color=new Color(.95f,.84f,.57f);text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;text.rectTransform.sizeDelta=new Vector2(2.5f,.4f);
    }
}
