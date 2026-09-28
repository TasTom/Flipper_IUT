using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>Explicit user-requested revision of Neutral: classic proportions, crossed
/// ramp returns and continuous lower guides. GDD: ramps, bumpers, targets and flippers.
/// One editor transaction; never runs automatically and never saves the scene.</summary>
public static class RefineNeutralFlow
{
    const string Folder="Assets/Generated/NeutralFlow";
    const string Action="Reprendre les proportions et les circuits de Neutral";
    static Transform table, gp, root, old, art;
    static Material steel, clear, liner, cover, enamel, ivory;
    static PhysicsMaterial physics;
    static int meshId;
    static string outputFolder=Folder;
    [Serializable] public class Route {public string name;public Vector3[] points;}
    [Serializable] public class Routes {public Route[] routes;}

    [MenuItem("Flipper/Neutral/Proportions et retours croisÃ©s")]
    public static void Apply()
    {
        if(Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/Neutral.unity")throw new InvalidOperationException("Ouvrir Neutral hors Play.");
        table=GameObject.Find("PinballTable").transform;gp=table.Find("Gameplay");old=table.Find("ReferencePlayfield");art=old.Find("ProfessionalArt");
        if(old.Find("FlowRefinement")!=null)throw new InvalidOperationException("RÃ©vision dÃ©jÃ  prÃ©sente : conserver les ajustements.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Generated","NeutralFlow");
        steel=Mat("PolishedSteel");clear=Mat("ClearRamp");liner=Mat("WireformClearLiner");cover=Mat("ClearGuard");enamel=Mat("EnamelMidnight");ivory=Mat("WarmIvory");
        physics=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Generated/NeutralPresentation/RampSurface.physicMaterial");
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(Action);
        try
        {
            outputFolder=Folder;meshId=0;root=New("FlowRefinement",old);
            foreach(Transform tr in old)if(tr.name.StartsWith("Ramp_")||tr.name.StartsWith("RampSupport_")||tr.name.StartsWith("Inlane")||tr.name.StartsWith("OutlanePost")||tr.name.StartsWith("ApronGuide")||tr.name=="OrbitExitFeed")Retire(tr,true);
            foreach(Transform tr in art)if(tr.name.StartsWith("Vosges_")||tr.name.StartsWith("IUT_")||tr.name.StartsWith("Sling")||tr.name.StartsWith("Bumper")||tr.name.StartsWith("MountingScrew")||tr.name.StartsWith("Shot_")||tr.name.StartsWith("Apron"))Retire(tr,false);
            foreach(string s in new[]{"Left","Right"})Lower(s);
            OrbitFeed();
            var paths=BuildPaths();var vosges=paths[0].points.ToList();var iut=paths[1].points.ToList();
            Ramp("Vosges",vosges,88);Ramp("IUT",iut,64);
            Bumpers();Targets();Apron();
            Move(gp.Find("Boss_ProjetFinal"),P(.9f,0,15.6f));
            foreach(string n in new[]{"FinalProjectLegend","FinalProjectChrome"}){var tr=art.Find(n);Undo.RecordObject(tr,Action);tr.localPosition+=P(1.3f,0,-.9f);}
            var title=old.Find("Title").GetComponent<TextMeshPro>();Undo.RecordObject(title,Action);title.fontSize=3.3f;
            var plaque=art.Find("TitlePlaque");Undo.RecordObject(plaque,Action);plaque.localScale=new Vector3(1.95f,.015f,1.10f);
            var boss=gp.Find("Boss_ProjetFinal");Undo.RecordObject(boss,Action);boss.localScale=Vector3.one*.48f;
            GatePair("Vosges",vosges);GatePair("IUT",iut);
            Physics.SyncTransforms();AssetDatabase.SaveAssets();
            System.IO.File.WriteAllText("Tools/unity/out/refined-paths.json",JsonUtility.ToJson(new Routes{routes=new[]{new Route{name="Vosges",points=vosges.ToArray()},new Route{name="IUT",points=iut.ToArray()}}},true));
        }
        catch{Undo.RevertAllDownToGroup(group);throw;}
        finally{Undo.CollapseUndoOperations(group);}
    }

    static Route[] BuildPaths()
    {
        var a=new List<Vector3>();
        Add(a,P(-1.65f,0,9.65f),P(-1.95f,0,10.4f),P(-2.65f,.42f,11.6f),P(-2.70f,.65f,12.4f));
        Add(a,P(-2.70f,.65f,12.4f),P(-2.8f,.95f,14.1f),P(-2.6f,1.22f,15.9f),P(-1.3f,1.24f,16.2f));
        Add(a,P(-1.3f,1.24f,16.2f),P(.1f,1.26f,16.7f),P(2.36f,1.24f,16.25f),P(2.36f,1.20f,14.8f));
        Add(a,P(2.36f,1.20f,14.8f),P(2.36f,1.05f,12),P(2.34f,.90f,8),P(2.34f,.66f,6.9f));
        Add(a,P(2.34f,.66f,6.9f),P(2.34f,.36f,6.3f),P(2.34f,0,5.8f),P(2.34f,0,5.5f));
        var b=new List<Vector3>();
        Add(b,P(.9f,0,9.65f),P(.85f,0,10.5f),P(.95f,.9f,11.2f),P(1.15f,1.55f,12.1f));
        Add(b,P(1.15f,1.55f,12.1f),P(1.45f,1.78f,13.35f),P(.9f,1.76f,13.8f),P(0,1.7f,13.15f));
        Add(b,P(0,1.7f,13.15f),P(-.9f,1.66f,12.5f),P(-2.95f,1.48f,11.65f),P(-3.08f,1.3f,10.55f));
        Add(b,P(-3.08f,1.3f,10.55f),P(-3.13f,1.1f,9.2f),P(-3.1f,.88f,7.6f),P(-3.1f,.66f,6.9f));
        Add(b,P(-3.1f,.66f,6.9f),P(-3.1f,.36f,6.3f),P(-3.1f,0,5.8f),P(-3.1f,0,5.5f));
        return new[]{new Route{name="Vosges",points=a.ToArray()},new Route{name="IUT",points=b.ToArray()}};
    }

    public static void CorrectRoutes()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit required");
        table=GameObject.Find("PinballTable").transform;gp=table.Find("Gameplay");old=table.Find("ReferencePlayfield");art=old.Find("ProfessionalArt");
        var existing=old.Find("FlowRefinement");if(existing==null)throw new InvalidOperationException("Initial layout required");
        if(existing.Find("CorrectedRoutes")!=null)throw new InvalidOperationException("Already corrected");
        steel=Mat("PolishedSteel");clear=Mat("ClearRamp");liner=Mat("WireformClearLiner");cover=Mat("ClearGuard");
        physics=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Generated/NeutralPresentation/RampSurface.physicMaterial");
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("DÃ©gager les circuits croisÃ©s");
        foreach(Transform tr in existing)if(tr.name.StartsWith("Vosges_")||tr.name.StartsWith("IUT_"))Retire(tr,true);
        outputFolder=Folder+"/CorrectedRoutes";if(!AssetDatabase.IsValidFolder(outputFolder))AssetDatabase.CreateFolder(Folder,"CorrectedRoutes");
        root=New("CorrectedRoutes",existing);meshId=0;var paths=BuildPaths();
        foreach(var r in paths){Ramp(r.name,r.points.ToList(),r.name=="Vosges"?88:64);GatePair(r.name,r.points.ToList());}
        AssetDatabase.SaveAssets();Physics.SyncTransforms();
        System.IO.File.WriteAllText("Tools/unity/out/refined-paths.json",JsonUtility.ToJson(new Routes{routes=paths},true));
        Undo.CollapseUndoOperations(group);
    }

    static void OrbitFeed()
    {
        var points=new List<Vector3>();
        Add(points,P(-4.15f,0,11.2f),P(-4.08f,0,10.35f),P(-3.65f,0,9.9f),P(-3.20f,0,9.55f));
        Wall("OrbitFeedCurve",points,.065f,.38f);
    }
    public static void AddOrbitFeed()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit required");
        root=GameObject.Find("PinballTable").transform.Find("ReferencePlayfield/FlowRefinement");
        if(root.Find("OrbitFeedCurve")!=null)throw new InvalidOperationException("Guide already present");
        steel=Mat("PolishedSteel");physics=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Generated/NeutralPresentation/RampSurface.physicMaterial");
        outputFolder=Folder;meshId=900;OrbitFeed();AssetDatabase.SaveAssets();Physics.SyncTransforms();
    }
    static void Lower(string side)
    {
        bool left=side=="Left";float Mirror(float x)=>left?x:-.8f-x;
        var outer=new List<Vector3>();
        Add(outer,P(Mirror(-3.43f),0,6.35f),P(Mirror(-3.46f),0,5.7f),P(Mirror(-3.49f),0,4.7f),P(Mirror(-3.05f),0,4.15f));
        Add(outer,P(Mirror(-3.05f),0,4.15f),P(Mirror(-2.75f),0,3.78f),P(Mirror(-2.25f),0,3.52f),P(Mirror(-1.97f),0,3.48f));
        Wall("InlaneOuter_"+side,outer,.065f,.38f);
        var inner=new List<Vector3>();Add(inner,P(Mirror(-2.73f),0,6.35f),P(Mirror(-2.68f),0,5.85f),P(Mirror(-2.64f),0,5.10f),P(Mirror(-2.61f),0,4.73f));
        Wall("InlaneInner_"+side,inner,.065f,.38f);
        Post("OutlanePost_"+side,P(Mirror(-3.43f),.19f,6.35f),.075f,.38f,true);
        Move(gp.Find("Flipper_"+side+"_Pivot"),P(left?-1.95f:1.15f,0,3.2f));
        var sling=gp.Find("Slingshot_"+side);Undo.RecordObject(sling,Action);sling.localScale=new Vector3(.62f,.75f,.62f);sling.localPosition=P(left?-2.12f:1.32f,.15f,5.3f);
        var plate=sling.Find("Plastique_Decal");var rr=plate.GetComponent<Renderer>();Undo.RecordObject(rr,Action);rr.sharedMaterials=Enumerable.Repeat(Mat("ForestEnamel"),rr.sharedMaterials.Length).ToArray();
        var mf=plate.GetComponent<MeshFilter>();var points=mf.sharedMesh.vertices.Select(v=>table.InverseTransformPoint(plate.TransformPoint(v))).ToArray();
        var centre=points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Length;
        Label("SlingLegend_"+side,left?"VOSGES":"IUT",centre+Vector3.up*.07f,.65f,ivory.color);
        // The apron covers these drain-side guides; the visible inlane is a continuous return.
        var below=new List<Vector3>();Add(below,P(Mirror(-4.12f),0,2.8f),P(Mirror(-3.5f),0,1.6f),P(Mirror(-1.9f),0,1.2f),P(-.4f,0,.9f));
        Wall("CoveredDrainGuide_"+side,below,.07f,.32f);
        Move(gp.Find("Outlane_Drain_"+side),P(left?-3.83f:3.08f,.30f,2.55f));
        Move(old.Find("Out_"+side),P(left?-3.82f:3.09f,.026f,4.4f));
        Move(old.Find("Return_"+side),P(left?-3.1f:2.34f,.026f,5.6f));
        if(left)Retire(gp.Find("Kickback_Left"),false);
    }

    static void Bumpers()
    {
        var p=new[]{P(-1.2f,0,13.15f),P(.12f,0,14.2f),P(-1.22f,0,15.2f)};
        for(int i=0;i<3;i++){
            var tr=gp.Find("Bumper_0"+(i+1));Move(tr,p[i]);
            foreach(var r in tr.GetComponentsInChildren<MeshRenderer>())if(r.name.Contains("_2_")){Undo.RecordObject(r.transform,Action);r.transform.localScale=new Vector3(.86f,1,.86f);}
            Post("BumperMedallion_"+i,p[i]+Vector3.up*.79f,.30f,.009f,false,Mat("ForestEnamel"));
            Circle("BumperCapRim_"+i,p[i]+Vector3.up*.80f,.30f,.012f,steel);
            Label("BumperIndex_"+i,"0"+(i+1),p[i]+Vector3.up*.81f,1.4f,ivory.color);
        }
    }
    static void Targets()
    {
        int i=0;
        foreach(var target in gp.GetComponentsInChildren<SubjectTarget>()){
            bool left=target.transform.localPosition.x<0;float z=7.25f+(i%3)*.68f;
            Move(target.transform,P(left?-2.55f:1.75f,0,z));
            var model=target.transform.Find("Target_Pro");Undo.RecordObject(model,Action);model.localScale*=.85f;
            var col=target.GetComponent<BoxCollider>();Undo.RecordObject(col,Action);col.size=new Vector3(.34f,.51f,.13f);col.center=Vector3.up*.255f;
            var insert=target.transform.Find("RewardInsert");Move(insert,P(left?-1.98f:1.18f,.02f,z));Undo.RecordObject(insert,Action);insert.localScale*=.82f;
            var label=target.transform.Find("RewardLabel");Move(label,P(left?-1.50f:.70f,.032f,z));
            var bezel=art.Find("RewardBezel_"+i);if(bezel!=null){Retire(bezel,false);Circle("RewardBezel_"+i,P(left?-1.98f:1.18f,.046f,z),.191f,.017f,steel);}
            var plaque=art.Find("Plaque_"+target.name);if(plaque!=null)Move(plaque,P(left?-1.5f:.7f,.019f,z));i++;
        }
    }
    static void Apron()
    {
        Retire(old.Find("Apron"),false);Retire(old.Find("Rules"),false);Retire(old.Find("ApronTitle"),false);
        var edge=new List<Vector3>();Add(edge,P(-4.285f,.55f,3.0f),P(-2.9f,.55f,2.95f),P(-1.5f,.55f,2.1f),P(-.4f,.55f,2.1f));
        Add(edge,P(-.4f,.55f,2.1f),P(.8f,.55f,2.1f),P(2.8f,.55f,2.95f),P(3.48f,.55f,3.0f));
        var v=new List<Vector3>();var tri=new List<int>();foreach(var p in edge){v.Add(P(p.x,.55f,-.55f));v.Add(p);}
        for(int i=0;i<edge.Count-1;i++){int a=i*2;tri.AddRange(new[]{a,a+1,a+3,a,a+3,a+2});}
        MeshObject("ApronCover",v,tri,enamel,false);Tube("ApronChromeEdge",edge,.028f,steel);
        Label("ApronBrand","VOSGES  /  MANIA",P(-.4f,.565f,.70f),1.8f,ivory.color);
        Label("ApronRules","6 CIBLES     2 RAMPES     3 BILLES",P(-.4f,.565f,1.20f),.65f,ivory.color);
        Label("ApronControls","Q / A : GAUCHE          D : DROITE",P(-.4f,.565f,.22f),.57f,new Color(.7f,.76f,.75f));
    }

    static void Ramp(string name,List<Vector3> p,int wireStart)
    {
        var section=new[]{new Vector2(-.37f,-.03f),new Vector2(.37f,-.03f),new Vector2(.37f,.36f),new Vector2(.33f,.36f),new Vector2(.33f,0),new Vector2(-.33f,0),new Vector2(-.33f,.36f),new Vector2(-.37f,.36f)};
        Sweep(name+"_Track",p,section,clear,true);
        // A clear, thin cage roof stops aerial escapes without a bulky opaque channel.
        Sweep(name+"_Cover",RoofPath(p),new[]{new Vector2(-.37f,0),new Vector2(.37f,0),new Vector2(.37f,.02f),new Vector2(-.37f,.02f)},cover,true);
        var skirtV=new List<Vector3>();var skirtT=new List<int>();
        for(int i=0;i<p.Count;i++){var n=Normal(p,i);var l=p[i]-n*.37f;var r=p[i]+n*.37f;skirtV.Add(P(l.x,-.012f,l.z));skirtV.Add(P(r.x,-.012f,r.z));skirtV.Add(r-Vector3.up*.03f);skirtV.Add(l-Vector3.up*.03f);}
        for(int i=0;i<p.Count-1;i++)if(p[i].y<.72f||p[i+1].y<.72f)for(int j=0;j<4;j++){int a=i*4+j,b=i*4+(j+1)%4;skirtT.AddRange(new[]{a,b,b+4,a,b+4,a+4});}
        MeshObject(name+"_UndercutGuard",skirtV,skirtT,Mat("ForestEnamel"),true);
        var tr=root.Find(name+"_Track");var source=tr.GetComponent<MeshFilter>().sharedMesh;var visual=UnityEngine.Object.Instantiate(source);visual.name=name+"_Visual";var tt=source.triangles;var first=new List<int>();var second=new List<int>();
        for(int i=0;i<tt.Length;i+=3){var dest=Mathf.Min(tt[i],Mathf.Min(tt[i+1],tt[i+2]))/8<wireStart?first:second;dest.AddRange(new[]{tt[i],tt[i+1],tt[i+2]});}
        visual.subMeshCount=2;visual.SetTriangles(first,0);visual.SetTriangles(second,1);AssetDatabase.CreateAsset(visual,outputFolder+"/"+visual.name+".asset");tr.GetComponent<MeshFilter>().sharedMesh=visual;tr.GetComponent<Renderer>().sharedMaterials=new[]{clear,liner};
        foreach(int s in new[]{-1,1}){
            Tube(name+"_Rim_"+s,p.Select((v,i)=>v+Normal(p,i)*s*.35f+Vector3.up*.365f),.021f,steel);
            Tube(name+"_Runner_"+s,p.Skip(wireStart).Select((v,i)=>v+Normal(p,i+wireStart)*s*.16f-Vector3.up*.018f),.025f,steel);
            Tube(name+"_MidRail_"+s,p.Skip(wireStart).Select((v,i)=>v+Normal(p,i+wireStart)*s*.35f+Vector3.up*.19f),.019f,steel);
        }
        for(int i=wireStart;i<p.Count-3;i+=9){var n=Normal(p,i);Tube(name+"_Brace_"+i,new[]{p[i]-n*.39f+Vector3.up*.39f,p[i]-n*.39f-Vector3.up*.05f,p[i]+n*.39f-Vector3.up*.05f,p[i]+n*.39f+Vector3.up*.39f},.015f,steel);}
        for(int i=wireStart;i<p.Count-5;i+=18){var n=Normal(p,i);var arc=new List<Vector3>();for(int j=0;j<=12;j++){float a=j*Mathf.PI/12;arc.Add(p[i]+n*Mathf.Cos(a)*.39f+Vector3.up*(.36f+Mathf.Sin(a)*.23f));}Tube(name+"_Bow_"+i,arc,.012f,steel);}
        Move(old.Find("Shot_"+name),p[0]-new Vector3(0,-.028f,.6f));
    }
    static void GatePair(string name,List<Vector3> p)
    {
        var entry=gp.Find("Ramp_"+name+"_In");var exit=gp.Find("Ramp_"+name+"_Out");
        Gate(entry,p[2]+Vector3.up*.26f,p[4]-p[0]);int end=p.Count-20;Gate(exit,p[end]+Vector3.up*.26f,p[end+1]-p[end-1]);
        var a=new SerializedObject(entry.GetComponent<RampGate>());a.FindProperty("partner").objectReferenceValue=exit.GetComponent<RampGate>();a.ApplyModifiedProperties();var b=new SerializedObject(exit.GetComponent<RampGate>());b.FindProperty("partner").objectReferenceValue=entry.GetComponent<RampGate>();b.ApplyModifiedProperties();
    }
    static void Gate(Transform tr,Vector3 p,Vector3 dir){Move(tr,p);Undo.RecordObject(tr,Action);dir.y=0;tr.localRotation=Quaternion.LookRotation(dir);var c=tr.GetComponent<BoxCollider>();Undo.RecordObject(c,Action);c.size=new Vector3(.72f,.50f,.10f);c.center=Vector3.zero;c.isTrigger=true;}
    static void Add(List<Vector3> p,Vector3 a,Vector3 b,Vector3 c,Vector3 d){for(int i=p.Count==0?0:1;i<=32;i++){float t=i/32f,u=1-t;p.Add(u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d);}}
    static Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);
    // Vertical clearance must grow on slopes to keep the normal clearance above the ball diameter.
    static List<Vector3> RoofPath(IList<Vector3> p)
    {
        return p.Select((v,i)=>{
            var d=p[Math.Min(i+1,p.Count-1)]-p[Math.Max(0,i-1)];
            float horizontal=new Vector2(d.x,d.z).magnitude;
            float rise=.59f*Mathf.Sqrt(1+d.y*d.y/Mathf.Max(.000001f,horizontal*horizontal));
            return v+Vector3.up*rise;
        }).ToList();
    }
    public static void CorrectRoofClearance()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit required");
        var parent=GameObject.Find("PinballTable").transform.Find("ReferencePlayfield/FlowRefinement/CorrectedRoutes");
        foreach(var route in BuildPaths()){
            var tr=parent.Find(route.name+"_Cover");var mesh=tr.GetComponent<MeshFilter>().sharedMesh;
            Undo.RecordObject(mesh,"Dégager les plafonds de rampe");var v=mesh.vertices;var p=RoofPath(route.points);
            for(int i=0;i<p.Count;i++)for(int j=0;j<4;j++)v[i*4+j]=p[i]+Normal(p,i)*(j==0||j==3?-.37f:.37f)+Vector3.up*(j<2?0:.02f);
            mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var col=tr.GetComponent<MeshCollider>();col.sharedMesh=null;col.sharedMesh=mesh;
        }
        AssetDatabase.SaveAssets();Physics.SyncTransforms();
    }
    static Vector3 Normal(IList<Vector3> p,int i){var d=p[Math.Min(i+1,p.Count-1)]-p[Math.Max(0,i-1)];return new Vector3(d.z,0,-d.x).normalized;}
    static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/NeutralReference/Art/"+n+".mat");
    static Transform New(string n,Transform parent){var go=new GameObject(n);Undo.RegisterCreatedObjectUndo(go,Action);go.transform.SetParent(parent,false);return go.transform;}
    static void Move(Transform tr,Vector3 p){if(tr==null)return;Undo.RecordObject(tr,Action);tr.position=table.TransformPoint(p);}
    static void Retire(Transform tr,bool collision){if(tr==null)return;foreach(var r in tr.GetComponentsInChildren<Renderer>(true)){Undo.RecordObject(r,Action);r.enabled=false;}if(collision)foreach(var c in tr.GetComponentsInChildren<Collider>(true)){Undo.RecordObject(c,Action);c.enabled=false;}}
    static GameObject MeshObject(string n,IEnumerable<Vector3> verts,IEnumerable<int> tris,Material material,bool collision){var m=new Mesh{name=n};m.SetVertices(verts.ToList());m.SetTriangles(tris.ToList(),0);m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,outputFolder+"/"+(meshId++)+"_"+n+".asset");var go=New(n,root).gameObject;go.AddComponent<MeshFilter>().sharedMesh=m;go.AddComponent<MeshRenderer>().sharedMaterial=material;if(collision){var c=go.AddComponent<MeshCollider>();c.sharedMesh=m;c.sharedMaterial=physics;}return go;}
    static void Sweep(string n,List<Vector3> p,Vector2[] section,Material mat,bool collision){var v=new List<Vector3>();var t=new List<int>();int k=section.Length;for(int i=0;i<p.Count;i++)foreach(var q in section)v.Add(p[i]+Normal(p,i)*q.x+Vector3.up*q.y);for(int i=0;i<p.Count-1;i++)for(int j=0;j<k;j++){int a=i*k+j,b=i*k+(j+1)%k;t.AddRange(new[]{a,b,b+k,a,b+k,a+k});}int[] caps=k==8?new[]{0,1,4,0,4,5,1,2,3,1,3,4,0,5,6,0,6,7}:new[]{0,1,2,0,2,3};for(int i=0;i<caps.Length;i+=3){t.AddRange(new[]{caps[i+2],caps[i+1],caps[i]});int end=(p.Count-1)*k;t.AddRange(new[]{end+caps[i],end+caps[i+1],end+caps[i+2]});}MeshObject(n,v,t,mat,collision);}
    static void Wall(string n,List<Vector3> p,float w,float h)=>Sweep(n,p,new[]{new Vector2(-w/2,0),new Vector2(w/2,0),new Vector2(w/2,h),new Vector2(-w/2,h)},steel,true);
    static void Tube(string n,IEnumerable<Vector3> source,float r,Material m){var p=source.ToList();var v=new List<Vector3>();var tri=new List<int>();const int k=10;for(int i=0;i<p.Count;i++){var normal=Normal(p,i);if(normal.sqrMagnitude<.1f)normal=Vector3.right;var dir=(p[Math.Min(i+1,p.Count-1)]-p[Math.Max(0,i-1)]).normalized;var up=Vector3.Cross(dir,normal).normalized;for(int j=0;j<k;j++){float a=j*Mathf.PI*2/k;v.Add(p[i]+r*(normal*Mathf.Cos(a)+up*Mathf.Sin(a)));}}for(int i=0;i<p.Count-1;i++)for(int j=0;j<k;j++){int a=i*k+j,b=i*k+(j+1)%k;tri.AddRange(new[]{a,b,b+k,a,b+k,a+k});}MeshObject(n,v,tri,m,false);}
    static void Circle(string n,Vector3 p,float r,float w,Material m){var v=new List<Vector3>();for(int i=0;i<=48;i++){float a=i*Mathf.PI/24;v.Add(p+P(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));}Tube(n,v,w,m);}
    static void Post(string n,Vector3 p,float r,float h,bool collision,Material m=null){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Undo.RegisterCreatedObjectUndo(g,Action);g.name=n;g.transform.SetParent(root,false);g.transform.localPosition=p;g.transform.localScale=P(r*2,h/2,r*2);g.GetComponent<Renderer>().sharedMaterial=m??steel;if(!collision)Undo.DestroyObjectImmediate(g.GetComponent<Collider>());else g.GetComponent<Collider>().sharedMaterial=physics;}
    static void Label(string n,string value,Vector3 p,float size,Color color){var tr=New(n,root);tr.localPosition=p;tr.localRotation=Quaternion.Euler(90,0,0);var text=tr.gameObject.AddComponent<TextMeshPro>();text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");text.text=value;text.fontSize=size;text.color=color;text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;text.rectTransform.sizeDelta=new Vector2(5,1);}
}
