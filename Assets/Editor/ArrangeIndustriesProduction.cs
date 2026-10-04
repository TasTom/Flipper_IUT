using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Unity;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;
using Mesh = UnityEngine.Mesh;
using Material = UnityEngine.Material;

/// <summary>
/// Migration explicite et annulable d'Industries vers deux lignes de production.
/// Autorisée par la demande de remodelage du layout. Une seule application ; les
/// retouches suivantes dans l'Inspector sont préservées. Aucun enregistrement de scène.
/// </summary>
public static class ArrangeIndustriesProduction
{
    private const string Folder = "Assets/Generated/Industries/Production";
    private static readonly Vector3[] WoodPath = {
        new Vector3(.184f,0,-.689f),new Vector3(.190f,.020f,-.50f),new Vector3(.178f,.033f,-.335f),
        new Vector3(.142f,.041f,-.245f),new Vector3(.086f,.041f,-.242f),new Vector3(.027f,.0394f,-.2699f)
    };
    [MenuItem("Flipper/Industries/Installer le layout et les règles de production")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Industries")
        { Debug.LogWarning("[Industries] Ouvrir Industries hors Play."); return; }
        var table = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TableComponent>(true)).FirstOrDefault();
        var pf = table?.GetComponentInChildren<PlayfieldComponent>()?.transform;
        var config = AssetDatabase.LoadAssetAtPath<IndustriesConfig>("Assets/Generated/Industries/IndustriesConfig.asset");
        if (pf == null || config == null || !AssetDatabase.IsValidFolder(Folder))
        { Debug.LogWarning("[Industries] Table, config ou assets de production manquants."); return; }
        if (pf.Find("ProductionLayout") != null)
        { Debug.Log("[Industries] Layout déjà installé ; placements manuels conservés."); return; }
        string[] names = { "sw1", "sw2", "sw3", "sw11", "sw12", "sw13" };
        var targets = names.Select(n => table.GetComponentsInChildren<TargetComponent>(true).FirstOrDefault(t => t.name == n)).ToArray();
        var lamps = new[] { "l1", "l2", "l3", "l11", "l12", "l13" }.Select(n => table.GetComponentsInChildren<LightComponent>(true).FirstOrDefault(t => t.name == n)).ToArray();
        var bumpers = Enumerable.Range(1, 5).Select(i => table.GetComponentsInChildren<BumperComponent>(true).FirstOrDefault(b => b.name == "Bumper" + i)).ToArray();
        var scoop = table.GetComponentsInChildren<KickerComponent>(true).FirstOrDefault(k => k.name == "Kicker1");
        var print = pf.Find("PrintedPlayfield")?.GetComponent<MeshFilter>();
        var presentation = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<IndustriesPresentation>(true)).FirstOrDefault();
        var method = typeof(VpxSceneConverter).GetMethod("InstantiateAndPersistPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
        if (targets.Any(t => t == null || t.GetComponent<MeshFilter>()?.sharedMesh == null) || lamps.Any(l => l == null) || bumpers.Any(b => b == null) ||
            scoop == null || print?.GetComponent<MeshRenderer>()?.sharedMaterial == null || pf.GetComponent<MeshFilter>() == null || presentation == null || method == null ||
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Industries/Production/Playfield.png") == null ||
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Industries/Production/Instructions.png") == null ||
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Industries/Remodel/WorkshopSteel.mat") == null ||
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Industries/Remodel/WorkshopCopper.mat") == null)
        { Debug.LogWarning("[Industries] Une pièce VPE requise manque ; migration annulée."); return; }
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Industries : layout et production");
        try
        {
            var marker = new GameObject("ProductionLayout");
            Undo.RegisterCreatedObjectUndo(marker, "Layout Industries"); marker.transform.SetParent(pf, false);
            var targetPositions = new[] {
                new Vector3(.250f,0,-.535f), new Vector3(.279f,0,-.5215f), new Vector3(.308f,0,-.508f),
                new Vector3(.394f,0,-.610f), new Vector3(.406f,0,-.641f), new Vector3(.418f,0,-.672f)
            };
            for (int i = 0; i < 6; i++)
            {
                Place(targets[i].transform, pf, targetPositions[i], i < 3 ? -25 : 70);
                // Conserve le même recul, le même gabarit et la lecture des chiffres du bas.
                var mesh = targets[i].GetComponent<MeshFilter>().sharedMesh;
                var visible = mesh.vertices.Where(v => v.y >= 0).ToArray();
                float center = (visible.Min(v => v.x) + visible.Max(v => v.x)) * .5f;
                var point = pf.InverseTransformPoint(targets[i].transform.TransformPoint(new Vector3(center,0,visible.Min(v=>v.z)-config.targetNumberFrontGap)));
                point.y = .0005f; Place(lamps[i].transform, pf, point, 0);
            }
            var bumperPositions = new[] {
                new Vector3(.284f,0,-.401f), new Vector3(.366f,0,-.370f), new Vector3(.124f,0,-.432f),
                new Vector3(.357f,0,-.235f), new Vector3(.278f,0,-.268f)
            };
            for (int i = 0; i < 5; i++)
            {
                var old = bumpers[i].transform.position;
                Place(bumpers[i].transform, pf, bumperPositions[i], 0);
                var delta = bumpers[i].transform.position - old;
                string prefix = "b" + (i+1) + "l";
                foreach (var lamp in table.GetComponentsInChildren<LightComponent>(true))
                    if (lamp.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !lamp.transform.IsChildOf(bumpers[i].transform))
                    { Undo.RecordObject(lamp.transform,"Voyant de bumper"); lamp.transform.position += delta; Record(lamp.transform); }
            }
            var scoopPosition = new Vector3(.119f,0,-.574f);
            var scoopDelta = pf.TransformPoint(scoopPosition) - scoop.transform.position;
            Place(scoop.transform,pf,scoopPosition,0);
            var cup = table.GetComponentsInChildren<PrimitiveComponent>(true).FirstOrDefault(p => p.name == "kickerCup1");
            if (cup != null) { Undo.RecordObject(cup.transform,"Coupelle de livraison"); cup.transform.position += scoopDelta; Record(cup.transform); }
            var meshAsset = BoardWithScoop(scoopPosition);
            foreach (var filter in new[] { pf.GetComponent<MeshFilter>(), print })
            { Undo.RecordObject(filter,"Ouverture déplacée du scoop"); filter.sharedMesh = meshAsset; Record(filter); }
            var nativeMesh = pf.GetComponent<VisualPinball.Unity.Playfield.PlayfieldMeshComponent>();
            if (nativeMesh != null) { Undo.RecordObject(nativeMesh,"Plateau avec ouverture"); nativeMesh.AutoGenerate = false; Record(nativeMesh); }
            float k = VisualPinball.Unity.Physics.ScaleInv;
            var data = new RampData("ProductionWoodRamp", WoodPath.Select(p => new DragPointData(new Vertex3D(p.x/k,-p.z/k,p.y/k)){IsSmooth=true}).ToArray()) {
                HeightBottom = 0, HeightTop = 0, WidthBottom = .054f/k, WidthTop = .038f/k,
                LeftWallHeight = .022f/k, RightWallHeight = .022f/k,
                LeftWallHeightVisible = .014f/k, RightWallHeightVisible = .014f/k,
                Elasticity = .15f, Friction = .08f, IsCollidable = true
            };
            var ramp = Instantiate(table,new VisualPinball.Engine.VPT.Ramp.Ramp(data),method);
            var steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Industries/Remodel/WorkshopSteel.mat");
            var copper = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Industries/Remodel/WorkshopCopper.mat");
            foreach (var renderer in ramp.GetComponentsInChildren<MeshRenderer>(true))
            { Undo.RecordObject(renderer,"Matériaux de rampe"); renderer.sharedMaterial = renderer.name == "Floor" ? steel : copper; Record(renderer); }
            foreach (var filter in ramp.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null||filter.sharedMesh.vertexCount==0)continue;
                string meshPath=Folder+"/WoodRamp"+filter.name+".asset";
                var persisted=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(persisted==null){persisted=Object.Instantiate(filter.sharedMesh);persisted.name="WoodRamp"+filter.name;AssetDatabase.CreateAsset(persisted,meshPath);}
                filter.sharedMesh=persisted;Record(filter);
            }
            // Volumes minces au niveau de chaque parcours : une bille passant dessous ne valide pas la rampe.
            Sensor(table, method, IndustriesProduction.WoodEntry, new Vector3(.184f,.001f,-.675f), .054f,.018f);
            Sensor(table, method, IndustriesProduction.WoodExit, new Vector3(.060f,.036f,-.254f), .020f,.038f);
            Sensor(table, method, IndustriesProduction.TextileEntry, new Vector3(.390f,.002f,-.512f), .052f,.018f);
            Sensor(table, method, IndustriesProduction.TextileExit, new Vector3(.302f,.044f,-.083f), .018f,.044f);
            RefineRoutes();
            Undo.RecordObject(config,"Règles de production"); config.productionEnabled = true;
            config.scoopKickAngle = 150; EditorUtility.SetDirty(config);
            var printRenderer = print.GetComponent<MeshRenderer>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/ProductionPlayfield.mat");
            if (material == null)
            {
                material = new Material(printRenderer.sharedMaterial){name="ProductionPlayfield"};
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Industries/Production/Playfield.png"));
                AssetDatabase.CreateAsset(material,Folder + "/ProductionPlayfield.mat");
            }
            Undo.RecordObject(printRenderer,"Art du nouveau layout"); printRenderer.sharedMaterial = material; Record(printRenderer);
            foreach (var text in table.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!text.text.Contains("6 CIBLES")) continue;
                Undo.RecordObject(text,"Instructions de production");
                text.text = "PRODUCTION\n3 CIBLES : CHARGER\nRAMPE : TRANSFORMER\nSCOOP : LIVRER\nDOUBLE LIVRAISON : BONUS"; Record(text);
            }
            FinishPresentation();
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log("[Industries] Layout asymétrique installé : nouvelle rampe bois, cibles centrales, bumpers déplacés, scoop à gauche, deux productions. Ctrl+Z annule ; Ctrl+S à votre choix.");
        }
        catch (Exception e)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogError("[Industries] Migration annulée : " + e);
        }
    }

    private static GameObject Instantiate(TableComponent table, IItem item, MethodInfo method)
    {
        table.TableContainer.Refresh();
        var result = method.Invoke(new VpxSceneConverter(table),new object[]{item,null});
        var go = result?.GetType().GetProperty("GameObject")?.GetValue(result) as GameObject;
        if (go == null) throw new InvalidOperationException("VPE n'a pas créé " + item.Name);
        Undo.RegisterCreatedObjectUndo(go,"Nouvelle pièce VPE Industries"); return go;
    }

    public static void FinishPresentation()
    {
        var engine=Object.FindAnyObjectByType<IndustriesVpeGame>();if(engine==null)return;
        var so=new SerializedObject(engine);var label=so.FindProperty("objectivesText").objectReferenceValue as TMP_Text;
        if(label!=null)
        {
            Undo.RecordObject(label,"Statut des deux productions");label.fontSize=22;label.text="BOIS 0/3\nTEXTILE 0/3";Record(label);
        }
        var card=engine.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.name=="ProductionCard");
        if(card!=null)
        {
            string path=Folder+"/ProductionInstructions.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(card.sharedMaterial){name="ProductionInstructions"};mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Industries/Production/Instructions.png"));AssetDatabase.CreateAsset(mat,path);}
            Undo.RecordObject(card,"Règles de production sur l'apron");card.sharedMaterial=mat;Record(card);
        }
        var ramp=engine.GetComponentsInChildren<RampComponent>(true).FirstOrDefault(r=>r.name=="ProductionWoodRamp");
        if(ramp!=null)foreach(var filter in ramp.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.sharedMesh==null||filter.sharedMesh.vertexCount==0)continue;
            string path=Folder+"/WoodRamp"+filter.name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=Object.Instantiate(filter.sharedMesh);mesh.name="WoodRamp"+filter.name;AssetDatabase.CreateAsset(mesh,path);}
            Undo.RecordObject(filter,"Mesh de la rampe bois");filter.sharedMesh=mesh;Record(filter);
        }
        EditorSceneManager.MarkSceneDirty(engine.gameObject.scene);
    }

    // Mise au point des hauteurs : la pente du modèle de base sautait brusquement
    // de 0 à 46 mm puis devenait plate, ce qui catapultait les tirs rapides.
    public static void RefineRoutes()
    {
        var engine=Object.FindAnyObjectByType<IndustriesVpeGame>();if(engine==null)return;
        var ramps=engine.GetComponentsInChildren<RampComponent>(true);
        var wood=ramps.FirstOrDefault(r=>r.name=="ProductionWoodRamp");
        if(wood!=null)
        {
            OwnRouteMeshes(wood,"WoodRamp");
            Undo.RecordObjects(new UnityEngine.Object[]{wood,wood.DragPointSpline,wood.DragPointSpline.Container},"Raccord progressif de la rampe bois");
            wood._widthTop=.038f/VisualPinball.Unity.Physics.ScaleInv;
            wood._heightBottom=wood._heightTop=0;
            Undo.RecordObject(wood.transform,"Origine du profil bois");
            var origin=wood.transform.localPosition;origin.y=0;wood.transform.localPosition=origin;Record(wood.transform);
            float k=VisualPinball.Unity.Physics.ScaleInv;
            var pf=engine.GetComponentInChildren<PlayfieldComponent>().transform;
            var points=WoodPath.Select(p=>{var v=wood.transform.InverseTransformPoint(pf.TransformPoint(p));
                return new DragPointData(new Vertex3D(v.x/k,-v.z/k,v.y/k)){IsSmooth=true};}).ToArray();
            wood.DragPoints=points;wood.RebuildMeshes();Record(wood);Record(wood.DragPointSpline);Record(wood.DragPointSpline.Container);
            var exit=engine.GetComponentsInChildren<TriggerComponent>(true).FirstOrDefault(t=>t.name==IndustriesProduction.WoodExit);
            if(exit!=null)Place(exit.transform,pf,new Vector3(.060f,.036f,-.254f),0);
        }
        var textile=ramps.FirstOrDefault(r=>r.name=="Ramp1");
        if(textile!=null)
        {
            OwnRouteMeshes(textile,"TextileRamp");
            Undo.RecordObjects(new UnityEngine.Object[]{textile,textile.DragPointSpline,textile.DragPointSpline.Container},"Pente continue de la rampe textile");
            textile._heightBottom=0;textile._heightTop=0;
            Undo.RecordObject(textile.transform,"Origine de la pente textile");
            var position=textile.transform.localPosition;position.y=0;textile.transform.localPosition=position;Record(textile.transform);
            var pf=engine.GetComponentInChildren<PlayfieldComponent>().transform;
            var profile=new[]{new Vector3(.3825f,0,-.5248f),new Vector3(.410f,.023f,-.475f),
                new Vector3(.4396f,.040f,-.4331f),new Vector3(.4743f,.046f,-.3048f),
                new Vector3(.4521f,.046f,-.1642f),new Vector3(.3615f,.043f,-.0895f),new Vector3(.2364f,.0394f,-.081f)};
            float k=VisualPinball.Unity.Physics.ScaleInv;
            var points=profile.Select(p=>{
                var v=textile.transform.InverseTransformPoint(pf.TransformPoint(p));
                return new DragPointData(new Vertex3D(v.x/k,-v.z/k,v.y/k)){IsSmooth=true};
            }).ToArray();
            textile.DragPoints=points;
            var col=textile.GetComponentInChildren<RampColliderComponent>(true);
            Undo.RecordObject(col,"Guides de rampe textile");col.LeftWallHeight=col.RightWallHeight=.045f/VisualPinball.Unity.Physics.ScaleInv;
            textile._leftWallHeightVisible=textile._rightWallHeightVisible=.040f/VisualPinball.Unity.Physics.ScaleInv;
            col.OverwritePhysics=true;col.Elasticity=.15f;col.Friction=.08f;col.Scatter=0;
            textile.RebuildMeshes();Record(textile);Record(textile.DragPointSpline);Record(textile.DragPointSpline.Container);Record(col);
            // Les deux rubans du modèle initial suivaient l'ancienne pente, sans collider.
            var presentation=Object.FindAnyObjectByType<IndustriesPresentation>();
            var so=new SerializedObject(presentation);var hidden=so.FindProperty("hiddenRenderers");
            foreach(var r in ramps.Where(r=>r.name=="Ramp6"||r.name=="Ramp7").SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true)))
            {
                bool found=false;for(int i=0;i<hidden.arraySize;i++)if(hidden.GetArrayElementAtIndex(i).objectReferenceValue==r)found=true;
                if(!found){hidden.InsertArrayElementAtIndex(hidden.arraySize);hidden.GetArrayElementAtIndex(hidden.arraySize-1).objectReferenceValue=r;}
                Undo.RecordObject(r,"Covers de l'ancienne pente");r.enabled=false;Record(r);
            }
            so.ApplyModifiedProperties();Record(presentation);
            var exit=engine.GetComponentsInChildren<TriggerComponent>(true).FirstOrDefault(t=>t.name==IndustriesProduction.TextileExit);
            if(exit!=null)Place(exit.transform,pf,new Vector3(.302f,.038f,-.083f),0);
        }
        foreach(var ramp in new[]{wood,textile}.Where(r=>r!=null))foreach(var filter in ramp.GetComponentsInChildren<MeshFilter>(true))
            if(filter.sharedMesh!=null&&AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(Folder,StringComparison.Ordinal))
            {EditorUtility.SetDirty(filter.sharedMesh);AssetDatabase.SaveAssetIfDirty(filter.sharedMesh);}
        EditorSceneManager.MarkSceneDirty(engine.gameObject.scene);
    }

    private static void OwnRouteMeshes(RampComponent ramp,string prefix)
    {
        foreach(var filter in ramp.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.sharedMesh==null||filter.sharedMesh.vertexCount==0)continue;
            string path=Folder+"/"+prefix+filter.name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=Object.Instantiate(filter.sharedMesh);mesh.name=prefix+filter.name;AssetDatabase.CreateAsset(mesh,path);}
            Undo.RecordObject(filter,"Mesh propre au layout Industries");filter.sharedMesh=mesh;Record(filter);
            Undo.RecordObject(mesh,"Géométrie de rampe Industries");
        }
    }

    private static void Sensor(TableComponent table, MethodInfo method, string name, Vector3 p, float width, float depth)
    {
        float k = VisualPinball.Unity.Physics.ScaleInv;
        var polygon = new[] { new Vector2(p.x-width/2,p.z-depth/2),new Vector2(p.x+width/2,p.z-depth/2),
            new Vector2(p.x+width/2,p.z+depth/2),new Vector2(p.x-width/2,p.z+depth/2) };
        var data = new TriggerData(name,p.x/k,-p.z/k) {
            Shape=TriggerShape.TriggerNone, IsVisible=false, HitHeight=.022f/k,
            DragPoints=polygon.Select(v=>new DragPointData(v.x/k,-v.y/k)).ToArray()
        };
        var go = Instantiate(table,new VisualPinball.Engine.VPT.Trigger.Trigger(data),method);
        var pos=go.transform.localPosition;pos.y=p.y;go.transform.localPosition=pos;Record(go.transform);
    }

    private static Mesh BoardWithScoop(Vector3 center)
    {
        const string path = Folder + "/ProductionPlayfield.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if(existing != null)return existing;
        const float width=.51384f,length=1.16694f,r=.0162f;
        var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int i=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
            if(Vector3.Cross(b-a,c-a).y<0){triangles.AddRange(new[]{i,i+2,i+1,i,i+3,i+2});}
            else triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
        }
        Vector3 P(float x,float z)=>new Vector3(x,0,z);
        void Rect(float x0,float x1,float z0,float z1)=>Quad(P(x0,z0),P(x1,z0),P(x1,z1),P(x0,z1));
        Rect(0,center.x-r,-length,0);Rect(center.x+r,width,-length,0);
        Rect(center.x-r,center.x+r,-length,center.z-r);Rect(center.x-r,center.x+r,center.z+r,0);
        for(int i=0;i<32;i++)
        {
            Vector3 Circle(int n){float a=n*Mathf.PI*2/32;return new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));}
            var a=Circle(i);var b=Circle(i+1);
            var outerA=a*r/Mathf.Max(Mathf.Abs(a.x),Mathf.Abs(a.z));var outerB=b*r/Mathf.Max(Mathf.Abs(b.x),Mathf.Abs(b.z));
            Quad(center+outerA,center+outerB,center+b*r,center+a*r);
        }
        var mesh=new Mesh{name="ProductionPlayfield"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
        mesh.uv=vertices.Select(v=>new Vector2(v.x/width,1+v.z/length)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    private static void Place(Transform t,Transform pf,Vector3 p,float yaw)
    { Undo.RecordObject(t,"Placement du layout Industries");t.position=pf.TransformPoint(p);t.rotation=pf.rotation*Quaternion.Euler(0,yaw,0);Record(t); }
    private static void Record(Object obj)=>PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
}
