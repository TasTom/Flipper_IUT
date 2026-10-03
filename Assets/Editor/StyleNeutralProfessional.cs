using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Explicit editor art pass for Neutral. GDD: ambiance Vosges/IUT and interface.
/// Adds visual furniture only; the tested physical tracks remain the source of geometry.
/// Does not run during play or save the scene. Existing art roots are never regenerated.</summary>
public static class StyleNeutralProfessional
{
    const string Folder = "Assets/Generated/NeutralReference/Art";
    const string Action = "Habillage professionnel de Neutral";
    static Transform table, physical, art, gameplay;
    static Material chrome, ink, cream, teal, gold, acrylic, cover, print;
    static int meshId;

    [MenuItem("Flipper/Neutral/Habillage professionnel")]
    public static void Apply()
    {
        if (Application.isPlaying || UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Neutral.unity")
            throw new InvalidOperationException("Ouvrir Neutral hors Play.");
        table = GameObject.Find("PinballTable")?.transform;
        physical = table?.Find("ReferencePlayfield"); gameplay = table?.Find("Gameplay");
        if (physical == null || gameplay == null) throw new InvalidOperationException("Plateau de référence absent.");
        if (physical.Find("ProfessionalArt") != null) throw new InvalidOperationException("Habillage présent : conserver les ajustements manuels.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated/NeutralReference", "Art");
        AssetDatabase.ImportAsset("Assets/Generated/NeutralReference/VosgesPlayfield.png", ImportAssetOptions.ForceSynchronousImport);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Generated/NeutralReference/VosgesPlayfield.png");
        if (texture == null) throw new InvalidOperationException("Illustration du plateau absente.");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(Action);
        try
        {
            meshId = 0;
            chrome = Mat("PolishedSteel", new Color(.65f,.72f,.77f), .92f, .88f);
            ink = Mat("EnamelMidnight", new Color(.008f,.024f,.032f), .35f, .64f);
            cream = Mat("WarmIvory", new Color(.92f,.79f,.49f), .15f, .55f);
            teal = Mat("ForestEnamel", new Color(.018f,.22f,.19f), .4f, .7f);
            gold = Mat("AmberInsert", new Color(1,.38f,.055f), .05f, .68f);
            gold.EnableKeyword("_EMISSION"); gold.SetColor("_EmissionColor", new Color(1,.22f,.025f)*1.8f);
            acrylic = Mat("ClearRamp", new Color(.36f,.72f,.68f,.18f), .03f, .86f, true);
            cover = Mat("ClearGuard", new Color(.65f,.85f,.88f,.035f), 0, .88f, true);
            print = Mat("PlayfieldPrint", Color.white, 0, .32f); print.mainTexture = texture;
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
            importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.anisoLevel = 8; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
            art = New("ProfessionalArt", physical);
            var floor = MeshObject("PrintedPlayfield", new[]{new Vector3(-4.285f,.008f,1),new Vector3(4.285f,.008f,1),new Vector3(4.285f,.008f,17.77f),new Vector3(-4.285f,.008f,17.77f)}, new[]{0,2,1,0,3,2}, print);
            floor.GetComponent<MeshFilter>().sharedMesh.uv = new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
            foreach(var name in new[]{"VosgesRidgelines"}) Hide(physical.Find(name));
            foreach(var name in new[]{"LeftBoundary","ShooterOuter","ShooterDivider","RearOrbitSteel","ShooterExit","InlaneOuter_Left","InlaneOuter_Right","InlaneInner_Left","InlaneInner_Right","ApronGuide_Left","ApronGuide_Right","OrbitExitFeed"})
                MaterialOn(physical.Find(name), chrome);
            foreach(var name in new[]{"LeftCabinet","RightCabinet","BackCabinet","CabinetBackdrop","Apron"}) MaterialOn(physical.Find(name), ink);
            Hide(physical.Find("RearOrbit"));
            Ramp("Vosges"); Ramp("IUT");
            Slingshot("Left"); Slingshot("Right");
            Bumpers(); Targets(); Spinner(); Boss(); Labels(); Lighting();
            AssetDatabase.SaveAssets();
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
        finally { Undo.CollapseUndoOperations(group); }
    }

    static void Ramp(string name)
    {
        var track = physical.Find("Ramp_"+name); var v = track.GetComponent<MeshFilter>().sharedMesh.vertices;
        int n = v.Length/8; var path = new List<Vector3>();
        for(int i=0;i<n;i++) path.Add((v[i*8+4]+v[i*8+5])*.5f);
        MaterialOn(track, acrylic); MaterialOn(physical.Find("Ramp_"+name+"_Cover"),cover);
        MaterialOn(physical.Find("Ramp_"+name+"_Skirt"),teal);
        foreach(int side in new[]{-1,1})
        {
            Hide(physical.Find("Ramp_"+name+"_Rim"+side));
            Tube(name+"_UpperRail_"+side,path.Select((p,i)=>p+Normal(path,i)*side*.347f+Vector3.up*.515f).ToList(),.025f,chrome);
            // Four round wire rails embrace the transparent return channel; no decoration enters its clear bore.
            Tube(name+"_ReturnRunner_"+side,path.Skip(94).Select((p,k)=>p+Normal(path,k+94)*side*.29f-Vector3.up*.025f).ToList(),.025f,chrome);
        }
        for(int i=98;i<n-5;i+=9)
        {
            var p=path[i]; var normal=Normal(path,i); var hoop=new List<Vector3>();
            hoop.Add(p-normal*.39f+Vector3.up*.53f);hoop.Add(p-normal*.39f-Vector3.up*.06f);
            hoop.Add(p+normal*.39f-Vector3.up*.06f);hoop.Add(p+normal*.39f+Vector3.up*.53f);
            Tube(name+"_WireBrace_"+i,hoop,.018f,chrome);
        }
        for(int i=54;i<n-12;i+=18)
        {
            var p=path[i]; var normal=Normal(path,i);
            // Fine hoop above the protective clear cover makes the containment readable.
            var pts=new List<Vector3>();for(int a=0;a<=12;a++){float t=Mathf.PI*a/12;pts.Add(p+normal*(Mathf.Cos(t)*.40f)+Vector3.up*(.53f+Mathf.Sin(t)*.15f));}
            Tube(name+"_SafetyBow_"+i,pts,.014f,chrome);
        }
        // Flared stainless entry plate, with screws outside the ball opening.
        var e=path[0];var nn=Normal(path,0);
        Tube(name+"_EntryLip",new[]{e-nn*.40f+Vector3.up*.015f,e+nn*.40f+Vector3.up*.015f},.012f,chrome);
        foreach(int s in new[]{-1,1}) Screw(e+nn*s*.41f+Vector3.up*.07f);
    }

    static void Bumpers()
    {
        for(int i=1;i<=3;i++)
        {
            var b=gameplay.Find("Bumper_0"+i);var p=b.localPosition;
            foreach(var r in b.GetComponentsInChildren<MeshRenderer>())
            {Undo.RecordObject(r,Action);r.sharedMaterial=r.name.Contains("_2_")?cream:chrome;}
            Cylinder("BumperMedallion_"+i,p+Vector3.up*.79f,.37f,.012f,teal);
            Circle("BumperRing_"+i,p+Vector3.up*.805f,.34f,.014f,chrome);
            Label("BumperNumber_"+i,"0"+i,p+Vector3.up*.813f,1.7f,new Color(1,.83f,.50f));
            foreach(float a in new[]{0f,120f,240f}) {float f=a*Mathf.Deg2Rad;Screw(p+new Vector3(Mathf.Cos(f)*.45f,.78f,Mathf.Sin(f)*.45f));}
        }
    }

    static void Targets()
    {
        int n=0;
        foreach(var target in gameplay.GetComponentsInChildren<SubjectTarget>())
        {
            foreach(var r in target.GetComponentsInChildren<MeshRenderer>())
            {
                if(r.name=="RewardInsert") continue;
                var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++)if(mats[i]!=null){if(mats[i].name.Contains("Yellow"))mats[i]=cream;else if(mats[i].name.Contains("Metal"))mats[i]=chrome;}
                Undo.RecordObject(r,Action);r.sharedMaterials=mats;
            }
            var insert=target.transform.Find("RewardInsert");if(insert==null)continue;
            var pos=table.InverseTransformPoint(insert.position);
            // Labels/inserts are offset toward the clear centre, away from the left orbit feed.
            bool left=target.transform.localPosition.x<0;pos.x=left?-1.95f:1.15f;
            pos.z=7.0f+(n%3)*.72f;pos.y=.020f;
            Undo.RecordObject(insert,Action);insert.position=table.TransformPoint(pos);
            MaterialOn(insert,gold);Circle("RewardBezel_"+n,pos+Vector3.up*.023f,.235f,.022f,chrome);
            var label=target.transform.Find("RewardLabel");if(label!=null){Undo.RecordObject(label,Action);label.position=table.TransformPoint(pos+new Vector3(left?.52f:-.52f,.012f,0));var txt=label.GetComponent<TextMeshPro>();Undo.RecordObject(txt,Action);txt.fontSize=1.0f;txt.color=new Color(1,.89f,.68f);}
            n++;
        }
    }

    static void Slingshot(string side)
    {
        var sling=gameplay.Find("Slingshot_"+side);var plastic=sling.Find("Plastique_Decal");
        if(plastic==null)return;
        var points=plastic.GetComponent<MeshFilter>().sharedMesh.vertices.Select(p=>table.InverseTransformPoint(plastic.TransformPoint(p))).ToArray();
        float y=points.Max(p=>p.y)+.025f;var hull=Hull(points.Select(p=>new Vector2(p.x,p.z)).ToList());
        if(hull.Count<3)return;
        var vertices=hull.Select(p=>new Vector3(p.x,y,p.y)).ToArray();var triangles=new List<int>();
        for(int i=1;i<hull.Count-1;i++)triangles.AddRange(new[]{0,i+1,i});
        MeshObject("SlingPrintedPlastic_"+side,vertices,triangles.ToArray(),teal);
        var line=vertices.Concat(new[]{vertices[0]}).ToArray();Tube("SlingTrim_"+side,line,.024f,cream);
        Vector3 centre=vertices.Aggregate(Vector3.zero,(a,b)=>a+b)/vertices.Length;
        foreach(var v in vertices)Screw(Vector3.Lerp(v,centre,.16f)+Vector3.up*.025f);
        Label("SlingLegend_"+side,side=="Left"?"V O S G E S":"I U T",centre+Vector3.up*.012f,.76f,new Color(1,.83f,.5f));
        MaterialOn(plastic,teal);MaterialOn(sling.Find("Slingshot_Band"),cream);
    }

    static void Spinner()
    {
        // Real Stern bracket from vbousquet/pinball-parts, mounted in the table coordinate frame.
        var host=gameplay.Find("Spinner"); if(host==null)return;
        var model=Part("Assets/Models/Parts/Switches/Spinner_Stern_-_Bracket_with_switch.fbx","SpinnerMount",host.localPosition+new Vector3(-.1f,0,0),Quaternion.Euler(-90,0,0));
        if(model!=null)foreach(var r in model.GetComponentsInChildren<Renderer>())r.sharedMaterial=chrome;
        MaterialOn(host.Find("Spinner_Blade"),teal);
    }

    static void Boss()
    {
        var host=gameplay.Find("Boss_ProjetFinal");if(host==null)return;
        foreach(var r in host.GetComponentsInChildren<MeshRenderer>())
        {Undo.RecordObject(r,Action);r.sharedMaterial=r.name.Contains("Ecran")?teal:r.name.Contains("Montant")?chrome:ink;}
        var p=host.localPosition;Label("FinalProjectLegend","PROJET FINAL",p+new Vector3(0,.86f,.03f),.83f,new Color(1,.83f,.5f));
        Tube("FinalProjectChrome",new[]{p+new Vector3(-.70f,.86f,-.17f),p+new Vector3(.70f,.86f,-.17f)},.025f,chrome);
    }

    static void Labels()
    {
        var title=physical.Find("Title").GetComponent<TextMeshPro>();Undo.RecordObject(title,Action);
        title.text="VOSGES\n<size=62%>M A N I A</size>";title.fontStyle=FontStyles.Bold;title.fontSize=4.0f;title.color=new Color(1,.85f,.54f);
        Undo.RecordObject(title.transform,Action);title.transform.localPosition=new Vector3(-.4f,.03f,6.15f);
        foreach(string n in new[]{"Shot_Vosges","Shot_IUT"}){var tr=physical.Find(n);var p=tr.localPosition;
            var pts=new[]{p+new Vector3(-.18f,.012f,-.45f),p+new Vector3(0,.012f,-.20f),p+new Vector3(.18f,.012f,-.45f)};
            Tube(n+"_Arrow",pts,.026f,gold);
        }
        Label("ApronEdition","VOSGES MANIA  /  ÉDITION IUT",new Vector3(0,.135f,.58f),.85f,new Color(1,.83f,.5f));
        Hide(physical.Find("ApronTitle"));
        for(int s=-1;s<=1;s+=2){var x=s*3.55f;Screw(new Vector3(x,.135f,.63f));}
    }

    static void Lighting()
    {
        var sun=UnityEngine.Object.FindObjectsByType<Light>().FirstOrDefault(l=>l.type==LightType.Directional);
        if(sun!=null){Undo.RecordObject(sun,Action);sun.intensity=1.4f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.65f;}
        for(int i=0;i<3;i++)
        {
            var tr=New("TableFill_"+i,art);tr.localPosition=new Vector3(i==1?2.8f:-2.8f,3.1f,4.8f+i*4.3f);
            var l=tr.gameObject.AddComponent<Light>();l.type=LightType.Point;l.range=6;l.intensity=2.8f;
            l.color=i==1?new Color(.40f,.75f,1):new Color(1,.74f,.42f);l.shadows=LightShadows.None;
        }
        var probe=New("CabinetReflection",art);probe.localPosition=new Vector3(0,2,9);
        var rp=probe.gameObject.AddComponent<ReflectionProbe>();rp.mode=ReflectionProbeMode.Realtime;
        rp.refreshMode=ReflectionProbeRefreshMode.OnAwake;rp.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        rp.resolution=256;rp.size=new Vector3(14,9,27);rp.intensity=.85f;rp.clearFlags=ReflectionProbeClearFlags.Skybox;rp.RenderProbe();
    }

    static Transform Part(string path,string name,Vector3 pos,Quaternion rotation)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null){Debug.LogWarning("Pièce absente : "+path);return null;}
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);Undo.RegisterCreatedObjectUndo(go,Action);
        go.name=name;go.transform.SetParent(art,false);go.transform.localPosition=pos;go.transform.localRotation=rotation;
        foreach(var c in go.GetComponentsInChildren<Collider>())Undo.DestroyObjectImmediate(c);
        return go.transform;
    }
    static void Screw(Vector3 pos)=>Part("Assets/Models/Parts/Screws/Bolt_8-32_-_Slotted_Flanged.fbx","MountingScrew",pos,Quaternion.Euler(-90,0,0));
    static Material Mat(string name,Color c,float metal,float smooth,bool transparent=false)
    {
        var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(m,path);}
        Undo.RecordObject(m,Action);m.color=c;m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
        if(transparent){m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_BlendModePreserveSpecular",0);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);m.DisableKeyword("_ALPHAPREMULTIPLY_ON");m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;}
        EditorUtility.SetDirty(m);return m;
    }
    static Transform New(string name,Transform parent){var g=new GameObject(name);Undo.RegisterCreatedObjectUndo(g,Action);g.transform.SetParent(parent,false);return g.transform;}
    static void MaterialOn(Transform tr,Material mat){if(tr==null)return;foreach(var r in tr.GetComponentsInChildren<MeshRenderer>()){Undo.RecordObject(r,Action);r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();}}
    static void Hide(Transform tr){if(tr==null)return;foreach(var r in tr.GetComponentsInChildren<Renderer>()){Undo.RecordObject(r,Action);r.enabled=false;}}
    static GameObject MeshObject(string name,Vector3[] v,int[] t,Material m)
    {
        var mesh=new Mesh{name=name};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Folder+"/"+(meshId++)+"_"+name+".asset");
        var go=New(name,art).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=m;return go;
    }
    static Vector3 Normal(IList<Vector3> p,int i){var d=p[Math.Min(p.Count-1,i+1)]-p[Math.Max(0,i-1)];return new Vector3(d.z,0,-d.x).normalized;}
    static void Tube(string name,IEnumerable<Vector3> source,float radius,Material material)
    {
        var p=source.ToList();var v=new List<Vector3>();var tri=new List<int>();const int sides=8;
        for(int i=0;i<p.Count;i++){var normal=Normal(p,i);if(normal.sqrMagnitude<.1f)normal=Vector3.right;
            var direction=(p[Math.Min(i+1,p.Count-1)]-p[Math.Max(0,i-1)]).normalized;var up=Vector3.Cross(direction,normal).normalized;
            for(int j=0;j<sides;j++){float a=2*Mathf.PI*j/sides;v.Add(p[i]+radius*(normal*Mathf.Cos(a)+up*Mathf.Sin(a)));}}
        for(int i=0;i<p.Count-1;i++)for(int j=0;j<sides;j++){int a=i*sides+j,b=i*sides+(j+1)%sides;tri.AddRange(new[]{a,b,b+sides,a,b+sides,a+sides});}
        MeshObject(name,v.ToArray(),tri.ToArray(),material);
    }
    static void Circle(string name,Vector3 p,float radius,float wire,Material m){var pts=new List<Vector3>();for(int i=0;i<=48;i++){float a=i*Mathf.PI/24;pts.Add(p+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius);}Tube(name,pts,wire,m);}
    static void Cylinder(string name,Vector3 p,float radius,float height,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Undo.RegisterCreatedObjectUndo(g,Action);g.name=name;g.transform.SetParent(art,false);g.transform.localPosition=p;g.transform.localScale=new Vector3(radius*2,height/2,radius*2);Undo.DestroyObjectImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;}
    static void Label(string name,string text,Vector3 p,float size,Color c){var tr=New(name,art);tr.localPosition=p;tr.localRotation=Quaternion.Euler(90,0,0);var t=tr.gameObject.AddComponent<TextMeshPro>();t.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");t.text=text;t.fontSize=size;t.color=c;t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.NoWrap;t.rectTransform.sizeDelta=new Vector2(4,1);}
    static List<Vector2> Hull(List<Vector2> p)
    {
        p=p.Distinct().OrderBy(v=>v.x).ThenBy(v=>v.y).ToList();if(p.Count<3)return p;
        float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        var h=new List<Vector2>();foreach(var v in p){while(h.Count>=2&&Cross(h[h.Count-2],h[h.Count-1],v)<=0)h.RemoveAt(h.Count-1);h.Add(v);}int lower=h.Count;
        for(int i=p.Count-2;i>=0;i--){while(h.Count>lower&&Cross(h[h.Count-2],h[h.Count-1],p[i])<=0)h.RemoveAt(h.Count-1);h.Add(p[i]);}h.RemoveAt(h.Count-1);return h;
    }
}
