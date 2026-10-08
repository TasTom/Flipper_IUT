using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity;
using Object = UnityEngine.Object;
using Mesh = UnityEngine.Mesh;
using Material = UnityEngine.Material;
using Color = UnityEngine.Color;

/// <summary>
/// Correction explicite de Ramp1, demandée après le layout Industries.
/// GDD : rampes, séries de cibles et combos. Undo ; aucun enregistrement de scène.
/// </summary>
public static class RefineIndustriesRamp
{
    private const string Folder = "Assets/Generated/Industries/Ramp1";
    private const string Marker = "Ramp1CoveredRefit";
    private const string StraightMarker = "Ramp1EntryStraightened";
    public const string RoofName = "Ramp1TurnCeiling";
    public const float WallHeight = .020f;
    public const float CeilingClearance = .034f;
    private const float MouthWidth = .042f;
    private const float ReturnWidth = 58f * VisualPinball.Unity.Physics.ScaleInv;

    // Repères mesurés dans la scène, jamais les anciennes positions du VPE.
    private static Vector3[] BuildProfile(IndustriesVpeGame game, Transform pf)
    {
        var posts = game.GetComponentsInChildren<PrimitiveComponent>(true);
        var a = pf.InverseTransformPoint(posts.First(p => p.name == "Primitive28").transform.position);
        var b = pf.InverseTransformPoint(posts.First(p => p.name == "Primitive29").transform.position);
        var mouth = (a + b) * .5f; mouth.y = 0;
        var across = a - b; across.y = 0; across.Normalize();
        var forward = Vector3.Cross(across, Vector3.up).normalized;
        var wall = game.GetComponentsInChildren<SurfaceComponent>(true).First(s => s.name == "Wall36");
        var top = wall.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == "Top");
        var boundary = top.sharedMesh.vertices.Select(v => pf.InverseTransformPoint(top.transform.TransformPoint(v))).ToArray();
        int Nearest(Vector3 p) => Enumerable.Range(0, boundary.Length).OrderBy(i => (boundary[i] - p).sqrMagnitude).First();
        int start = Nearest(new Vector3(.39821f, .0313055f, -.45393f));
        int end = Nearest(new Vector3(.40275f, .0313055f, -.10797f));
        var arc = new System.Collections.Generic.List<Vector3>();
        for (int i = start; ; i = (i + 1) % boundary.Length) { arc.Add(boundary[i]); if (i == end) break; }
        float length = 0; for (int i = 1; i < arc.Count; i++) length += Vector3.Distance(arc[i-1], arc[i]);
        var result = new System.Collections.Generic.List<Vector3> { mouth, mouth + forward * .008f + Vector3.up * .0004f,
            mouth + forward * .020f + Vector3.up * .004f, new Vector3(mouth.x + .021f, .013f, mouth.z + .032f) };
        float distance = 0, last = -.1f;
        for (int i = 0; i < arc.Count; i++)
        {
            if (i > 0) distance += Vector3.Distance(arc[i-1], arc[i]);
            if (distance - last < .024f && i != arc.Count - 1) continue;
            var tangent = arc[Mathf.Min(i+1, arc.Count-1)] - arc[Mathf.Max(0, i-1)]; tangent.y = 0; tangent.Normalize();
            float t = (distance + .080f) / (length + .235f);
            // Le bord intérieur reste à 2,5 mm du carter. La largeur se resserre vers le retour.
            var p = arc[i] + Vector3.Cross(Vector3.up, tangent) * (Mathf.Lerp(MouthWidth, ReturnWidth, t) * .5f + .0025f
                + .002f * Mathf.Max(0, 1 - distance / .060f));
            p.y = Mathf.Lerp(.030f, .046f, Mathf.SmoothStep(0, 1, distance / .075f));
            result.Add(p); last = distance;
        }
        result.AddRange(new[] { new Vector3(.380f,.044f,-.079f), new Vector3(.329f,.042f,-.081f),
            new Vector3(.275f,.040f,-.081f), new Vector3(.2411f,.03940175f,-.08096f) });
        var profile = result.ToArray(); AlignEntry(profile); return profile;
    }

    // Points 2 à 6, en incluant le nœud d'index 6 : axe et pente constants.
    private static void AlignEntry(Vector3[] profile)
    {
        var start = profile[1]; var end = profile[6]; var span = end-start;
        var plan = span; plan.y = 0;
        for(int i=2;i<6;i++)
        {
            var offset = profile[i]-start; offset.y=0;
            profile[i]=Vector3.Lerp(start,end,Mathf.Clamp01(Vector3.Dot(offset,plan)/plan.sqrMagnitude));
        }
    }

    [MenuItem("Flipper/Industries/Redresser l'entrée de Ramp1 (points 2–6)")]
    public static void StraightenEntry()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.name!="Industries" || EditorApplication.isPlaying)
        { Debug.LogWarning("[Industries] Ouvrir Industries hors Play pour redresser Ramp1."); return; }
        var game=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<IndustriesVpeGame>(true)).FirstOrDefault();
        var pf=game?.GetComponentInChildren<PlayfieldComponent>()?.transform;
        var ramp=game?.GetComponentsInChildren<RampComponent>(true).FirstOrDefault(r=>r.name=="Ramp1");
        var layout=pf?.Find("ProductionLayout");
        if(layout==null || layout.Find(Marker)==null || ramp==null || ramp.DragPoints.Length<7)
        { Debug.LogWarning("[Industries] Ramp1 ou son layout manque ; aucune modification."); return; }
        if(layout.Find(StraightMarker)!=null)return;
        float k=VisualPinball.Unity.Physics.ScaleInv;
        var data=ramp.DragPoints;
        var profile=data.Select(d=>pf.InverseTransformPoint(ramp.transform.TransformPoint(new Vector3(d.Center.X*k,d.Center.Z*k,-d.Center.Y*k)))).ToArray();
        var entrySpan=profile[6]-profile[1];entrySpan.y=0;
        if(entrySpan.sqrMagnitude<.0001f)
        { Debug.LogWarning("[Industries] Segment d'entrée trop court pour être redressé."); return; }
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Redresser les points 2–6 de Ramp1");
        try
        {
            AlignEntry(profile);
            Undo.RecordObjects(new Object[]{ramp,ramp.DragPointSpline,ramp.DragPointSpline.Container},"Segment droit de Ramp1");
            foreach(var filter in ramp.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh!=null && AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(Folder,StringComparison.Ordinal))
                    Undo.RecordObject(filter.sharedMesh,"Maillages suivant le segment droit");
            for(int i=1;i<=6;i++)
            {
                var p=ramp.transform.InverseTransformPoint(pf.TransformPoint(profile[i]));
                data[i].Center=new Vertex3D(p.x/k,-p.z/k,p.y/k);
                // Empêche les tangentes des points extérieurs de courber le segment aligné.
                if(i==1 || i==6)data[i].IsSmooth=false;
            }
            ramp.DragPoints=data;ramp.RebuildMeshes();BuildEdgeRails(ramp,pf);BuildCeiling(ramp,pf,true);
            foreach(var filter in ramp.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh!=null && AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(Folder,StringComparison.Ordinal))
                { EditorUtility.SetDirty(filter.sharedMesh);AssetDatabase.SaveAssetIfDirty(filter.sharedMesh); }
            foreach(var obj in new Object[]{ramp,ramp.DragPointSpline,ramp.DragPointSpline.Container})Record(obj);
            var marker=new GameObject(StraightMarker);Undo.RegisterCreatedObjectUndo(marker,"Entrée droite de Ramp1");marker.transform.SetParent(layout,false);
            EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);Selection.activeGameObject=ramp.gameObject;
        }
        catch(Exception e){Undo.RevertAllDownToGroup(group);Debug.LogError("[Industries] Redressement annulé : "+e);}
    }

    [MenuItem("Flipper/Industries/Retirer la rampe bois et corriger Ramp1")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Industries" || EditorApplication.isPlaying)
        { Debug.LogWarning("[Industries] Ouvrir Industries hors Play pour corriger Ramp1."); return; }
        var game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<IndustriesVpeGame>(true)).FirstOrDefault();
        var pf = game?.GetComponentInChildren<PlayfieldComponent>()?.transform;
        var ramp = game?.GetComponentsInChildren<RampComponent>(true).FirstOrDefault(r => r.name == "Ramp1");
        var config = AssetDatabase.LoadAssetAtPath<IndustriesConfig>("Assets/Generated/Industries/IndustriesConfig.asset");
        if (pf == null || ramp == null || config == null || ramp.GetComponent<RampColliderComponent>() == null)
        { Debug.LogWarning("[Industries] Ramp1, plateau ou config manquant ; aucune modification."); return; }
        var layout = pf.Find("ProductionLayout");
        if (layout == null) { Debug.LogWarning("[Industries] Installer le layout Industries avant cette correction."); return; }
        if (layout.Find(Marker) != null) { StraightenEntry(); Debug.Log("[Industries] Ramp1 couverte déjà corrigée ; réglages manuels conservés."); return; }
        if (!game.GetComponentsInChildren<PrimitiveComponent>(true).Any(p => p.name == "Primitive28") ||
            !game.GetComponentsInChildren<PrimitiveComponent>(true).Any(p => p.name == "Primitive29") ||
            !game.GetComponentsInChildren<SurfaceComponent>(true).Any(s => s.name == "Wall36"))
        { Debug.LogWarning("[Industries] Poteaux d'entrée ou Wall36 manquants ; aucune modification."); return; }
        var sensors = game.GetComponentsInChildren<TriggerComponent>(true);
        var entry = sensors.FirstOrDefault(t => t.name == IndustriesProduction.TextileEntry);
        var exit = sensors.FirstOrDefault(t => t.name == IndustriesProduction.TextileExit);
        if (entry == null || exit == null) { Debug.LogWarning("[Industries] Capteurs Ramp1 manquants ; aucune modification."); return; }
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Industries : supprimer la rampe bois et corriger Ramp1");
        try
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated/Industries", "Ramp1");
            Rebuild(ramp, pf);
            RestoreGantry(pf);
            HideObsoleteCovers(game);
            var mouth = BuildProfile(game, pf)[0];
            AlignSensor(entry, pf, mouth + Vector3.up * .002f, .030f, .026f);
            AlignSensor(exit, pf, new Vector3(.273f,.040f,-.081f), .020f, .031f);
            foreach (var obsolete in game.GetComponentsInChildren<RampComponent>(true).Where(r => r.name == "ProductionWoodRamp").Select(r => r.gameObject)
                .Concat(sensors.Where(t => t.name == IndustriesProduction.WoodEntry || t.name == IndustriesProduction.WoodExit).Select(t => t.gameObject)).ToArray())
                Undo.DestroyObjectImmediate(obsolete);
            Undo.RecordObject(config, "Rampe de transformation commune");
            config.productionEnabled = true; config.sharedProductionRamp = true; EditorUtility.SetDirty(config);
            var marker = new GameObject(Marker); Undo.RegisterCreatedObjectUndo(marker, "Correction Ramp1"); marker.transform.SetParent(layout, false);
            var straightMarker = new GameObject(StraightMarker); Undo.RegisterCreatedObjectUndo(straightMarker,"Entrée droite de Ramp1");straightMarker.transform.SetParent(layout,false);
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssetIfDirty(config);
            Selection.activeGameObject = ramp.gameObject;
            Debug.Log("[Industries] Ramp1 centrée entre Primitive28/29, abaissée et guidée à l'extérieur de Wall36 ; virages couverts par un collider VPE. Ctrl+Z annule, Ctrl+S conserve.");
        }
        catch (Exception e) { Undo.RevertAllDownToGroup(group); Debug.LogError("[Industries] Correction annulée : " + e); }
    }

    // Le profil est dans le repère natif du plateau : la racine conserve sa pose et son échelle.
    public static void Rebuild(RampComponent ramp, Transform pf)
    {
        foreach (var filter in ramp.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name != "Floor" && filter.name != "Walls") continue;
            string path = Folder + "/Ramp1" + filter.name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = Object.Instantiate(filter.sharedMesh); mesh.name = "Ramp1" + filter.name; AssetDatabase.CreateAsset(mesh, path); }
            Undo.RecordObject(filter, "Géométrie dédiée de Ramp1"); filter.sharedMesh = mesh; Record(filter); Undo.RecordObject(mesh, "Profil de Ramp1");
        }
        Undo.RecordObjects(new Object[] { ramp, ramp.DragPointSpline, ramp.DragPointSpline.Container, ramp.transform }, "Courbe et pente de Ramp1");
        float k = VisualPinball.Unity.Physics.ScaleInv;
        var position = ramp.transform.localPosition; position.y = 0; ramp.transform.localPosition = position;
        ramp._heightBottom = ramp._heightTop = 0;
        ramp._widthBottom = MouthWidth / k; ramp._widthTop = ReturnWidth / k;
        ramp._leftWallHeightVisible = ramp._rightWallHeightVisible = WallHeight / k;
        ramp.DragPoints = BuildProfile(ramp.GetComponentInParent<IndustriesVpeGame>(), pf).Select((p,i) => {
            var v = ramp.transform.InverseTransformPoint(pf.TransformPoint(p));
            return new DragPointData(new Vertex3D(v.x / k, -v.z / k, v.y / k)) { IsSmooth = i!=1 && i!=6 };
        }).ToArray();
        var collider = ramp.GetComponent<RampColliderComponent>(); Undo.RecordObject(collider, "Guides de Ramp1");
        collider.LeftWallHeight = collider.RightWallHeight = WallHeight / k;
        collider.OverwritePhysics = true; collider.Elasticity = .15f; collider.Friction = .02f; collider.Scatter = 0;
        ramp.RebuildMeshes();
        foreach (var filter in ramp.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null && AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(Folder, StringComparison.Ordinal))
            { EditorUtility.SetDirty(filter.sharedMesh); AssetDatabase.SaveAssetIfDirty(filter.sharedMesh); }
        foreach (var renderer in ramp.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Floor" || r.name == "Walls"))
        {
            string path = Folder + "/Ramp1Plastic.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Ramp1Plastic", renderQueue = 3000 };
                mat.SetColor("_BaseColor", new Color(.31f, .69f, .67f, .14f)); mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
                mat.SetFloat("_SrcBlend", 5); mat.SetFloat("_DstBlend", 10); mat.SetFloat("_ZWrite", 0); mat.SetFloat("_Cull", 0);
                mat.SetFloat("_Smoothness", .72f); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.SetShaderPassEnabled("ShadowCaster", false);
                AssetDatabase.CreateAsset(mat, path);
            }
            Undo.RecordObject(renderer, "Plastique transparent de Ramp1"); renderer.sharedMaterial = mat; Record(renderer);
        }
        BuildEdgeRails(ramp, pf);
        BuildCeiling(ramp, pf);
        HideObsoleteCovers(ramp.GetComponentInParent<IndustriesVpeGame>());
        foreach (var obj in new Object[] { ramp, ramp.transform, ramp.DragPointSpline, ramp.DragPointSpline.Container, collider }) Record(obj);
    }

    private static void BuildEdgeRails(RampComponent ramp, Transform pf)
    {
        var floor = ramp.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == "Floor");
        var edge = ramp.transform.Find("EdgeRails");
        if (edge == null)
        {
            var go = new GameObject("EdgeRails"); Undo.RegisterCreatedObjectUndo(go, "Bordures de Ramp1");
            go.transform.SetParent(ramp.transform, false); go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>(); edge = go.transform;
        }
        string path = Folder + "/Ramp1EdgeRails.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh { name = "Ramp1EdgeRails" }; AssetDatabase.CreateAsset(mesh, path); }
        Undo.RecordObject(mesh, "Bordures ajustées au profil Ramp1");
        var vertices = new System.Collections.Generic.List<Vector3>(); var triangles = new System.Collections.Generic.List<int>();
        var surface = floor.sharedMesh.vertices; int count = surface.Length / 2;
        var lift = ramp.transform.InverseTransformVector(pf.TransformVector(Vector3.up * WallHeight));
        const int sides = 6;
        for (int side = 0; side < 2; side++)
        {
            int start = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                var p = ramp.transform.InverseTransformPoint(floor.transform.TransformPoint(surface[i * 2 + side])) + lift;
                var q = ramp.transform.InverseTransformPoint(floor.transform.TransformPoint(surface[(i == count - 1 ? i - 1 : i + 1) * 2 + side])) + lift;
                var tangent = (q - p).normalized * (i == count - 1 ? -1 : 1);
                var across = Vector3.Cross(tangent, lift.normalized).normalized; var up = Vector3.Cross(across, tangent).normalized;
                for (int j = 0; j < sides; j++)
                { float a = j * Mathf.PI * 2 / sides; vertices.Add(p + .00065f * (Mathf.Cos(a) * across + Mathf.Sin(a) * up)); }
                if (i == 0) continue;
                for (int j = 0; j < sides; j++)
                { int a = start + (i - 1) * sides + j, b = start + (i - 1) * sides + (j + 1) % sides, c = start + i * sides + j, d = start + i * sides + (j + 1) % sides;
                    triangles.AddRange(new[] { a, c, b, b, c, d }); }
            }
        }
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var filter = edge.GetComponent<MeshFilter>(); var renderer = edge.GetComponent<MeshRenderer>(); Undo.RecordObjects(new Object[] { filter, renderer }, "Bordures de cuivre de Ramp1");
        filter.sharedMesh = mesh; renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Industries/Remodel/WorkshopCopper.mat");
        Record(filter); Record(renderer); EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh);
    }

    // Plafond réel, pas un MeshCollider PhysX : la bille est simulée par VPE.
    private static void BuildCeiling(RampComponent ramp, Transform pf, bool preservePhysics=false)
    {
        var floor = ramp.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == "Floor");
        var rows = floor.sharedMesh.vertices;
        var pairs = Enumerable.Range(0, rows.Length / 2).Select(i => new[] {
            pf.InverseTransformPoint(floor.transform.TransformPoint(rows[i*2])),
            pf.InverseTransformPoint(floor.transform.TransformPoint(rows[i*2+1]))
        }).Where(v => (v[0].y+v[1].y)*.5f >= .004f && (v[0].x+v[1].x)*.5f > .258f).ToArray();
        var ceiling = ramp.transform.Find(RoofName);
        if (ceiling == null)
        {
            var go = new GameObject(RoofName); Undo.RegisterCreatedObjectUndo(go, "Plafond physique des virages");
            go.transform.SetParent(ramp.transform, false); ceiling = go.transform;
            Undo.AddComponent<MeshFilter>(go); Undo.AddComponent<MeshRenderer>(go);
            Undo.AddComponent<PrimitiveComponent>(go); Undo.AddComponent<PrimitiveColliderComponent>(go);
        }
        string path = Folder + "/Ramp1TurnCeiling.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh { name = RoofName }; AssetDatabase.CreateAsset(mesh, path); }
        Undo.RecordObject(mesh, "Plafond ajusté au sol de Ramp1");
        var vertices = new System.Collections.Generic.List<Vector3>(); var triangles = new System.Collections.Generic.List<int>();
        Vector3 Local(Vector3 p) => ceiling.InverseTransformPoint(pf.TransformPoint(p));
        for (int i = 0; i < pairs.Length; i++)
        {
            var across = (pairs[i][1] - pairs[i][0]).normalized;
            // Bord d'entrée relevé progressivement, sans arête frontale dans la trajectoire.
            float gap = CeilingClearance + .010f * Mathf.Max(0, 1 - i / 4f);
            var left = pairs[i][0] - across * .001f + Vector3.up * gap;
            var right = pairs[i][1] + across * .001f + Vector3.up * gap;
            vertices.Add(Local(left)); vertices.Add(Local(right));
            vertices.Add(Local(left + Vector3.up * .0012f)); vertices.Add(Local(right + Vector3.up * .0012f));
        }
        void Quad(int a, int b, int c, int d, Vector3 outward)
        {
            var n = Vector3.Cross(vertices[b]-vertices[a], vertices[c]-vertices[a]);
            int first = vertices.Count;
            vertices.AddRange(new[] { vertices[a], vertices[b], vertices[c], vertices[d] });
            if (Vector3.Dot(ceiling.TransformVector(n), pf.TransformVector(outward)) >= 0)
                triangles.AddRange(new[] { first,first+1,first+2,first,first+2,first+3 });
            else triangles.AddRange(new[] { first,first+2,first+1,first,first+3,first+2 });
        }
        for (int i = 1; i < pairs.Length; i++)
        {
            int a = (i-1)*4, b = i*4; var across = pairs[i][1] - pairs[i][0];
            Quad(a,a+1,b+1,b,Vector3.down); Quad(a+2,b+2,b+3,a+3,Vector3.up);
            Quad(a,b,b+2,a+2,-across); Quad(a+1,a+3,b+3,b+1,across);
        }
        Quad(0,2,3,1,(pairs[0][0]-pairs[1][0]).normalized);
        int last = (pairs.Length-1)*4;
        Quad(last,last+1,last+3,last+2,(pairs[pairs.Length-1][0]-pairs[pairs.Length-2][0]).normalized);
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0);
        mesh.SetUVs(0, vertices.Select(p => { var v = pf.InverseTransformPoint(ceiling.TransformPoint(p)); return new Vector2(v.x,v.z); }).ToList());
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        var filter = ceiling.GetComponent<MeshFilter>(); var renderer = ceiling.GetComponent<MeshRenderer>();
        var collider = ceiling.GetComponent<PrimitiveColliderComponent>();
        Undo.RecordObjects(new Object[] { filter, renderer, collider }, "Plafond VPE de Ramp1");
        string materialPath = Folder + "/Ramp1CeilingPlastic.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Ramp1CeilingPlastic", renderQueue = 3000 };
            material.SetColor("_BaseColor", new Color(.31f,.69f,.67f,.12f));
            material.SetFloat("_Surface",1); material.SetFloat("_Blend",0); material.SetFloat("_SrcBlend",5); material.SetFloat("_DstBlend",10);
            material.SetFloat("_ZWrite",0); material.SetFloat("_Cull",2); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("ShadowCaster",false);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        filter.sharedMesh = mesh; renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if(!preservePhysics)
        {
            collider.enabled = true; collider.OverwritePhysics = true; collider.CollisionReductionFactor = 0;
            collider.Elasticity = .05f; collider.ElasticityFalloff = 0; collider.Friction = .04f; collider.Scatter = 0;
            collider.HitEvent = true; collider.Threshold = .01f;
        }
        Record(filter); Record(renderer); Record(collider); EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh);
        BuildRoofClips(ramp, pf, pairs);
    }

    // Attaches fines entre les bordures courtes et le plafond, sans collision de décor.
    private static void BuildRoofClips(RampComponent ramp, Transform pf, Vector3[][] pairs)
    {
        var host = ramp.transform.Find("RoofClips");
        if (host == null)
        {
            var go = new GameObject("RoofClips"); Undo.RegisterCreatedObjectUndo(go, "Attaches du plafond de Ramp1");
            go.transform.SetParent(ramp.transform, false); host = go.transform;
            Undo.AddComponent<MeshFilter>(go); Undo.AddComponent<MeshRenderer>(go);
        }
        var vertices = new System.Collections.Generic.List<Vector3>(); var triangles = new System.Collections.Generic.List<int>();
        float travelled = 1;
        for (int i = 0; i < pairs.Length; i++)
        {
            if(i>0)travelled+=Vector3.Distance((pairs[i-1][0]+pairs[i-1][1])*.5f,(pairs[i][0]+pairs[i][1])*.5f);
            if(travelled<.080f && i!=pairs.Length-1)continue; travelled=0;
            float top=CeilingClearance+.010f*Mathf.Max(0,1-i/4f)+.0012f;
            var across=(pairs[i][1]-pairs[i][0]).normalized;
            var along=Vector3.Cross(across,Vector3.up).normalized;
            for(int side=0;side<2;side++)
            {
                var centre=pairs[i][side]+across*(side==0?-.0005f:.0005f)+Vector3.up*((WallHeight+top)*.5f);
                int first=vertices.Count;
                foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})foreach(int x in new[]{-1,1})
                {
                    var p=centre+across*(x*.0005f)+along*(z*.001f)+Vector3.up*(y*(top-WallHeight)*.5f);
                    vertices.Add(host.InverseTransformPoint(pf.TransformPoint(p)));
                }
                triangles.AddRange(new[]{0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5}.Select(n=>first+n));
            }
        }
        string path=Folder+"/Ramp1RoofClips.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh{name="Ramp1RoofClips"};AssetDatabase.CreateAsset(mesh,path);}
        Undo.RecordObject(mesh,"Attaches ajustées au plafond");mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var filter=host.GetComponent<MeshFilter>();var renderer=host.GetComponent<MeshRenderer>();
        Undo.RecordObjects(new Object[]{filter,renderer},"Attaches de cuivre");filter.sharedMesh=mesh;
        renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Industries/Remodel/WorkshopCopper.mat");
        Record(filter);Record(renderer);EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);
    }

    // Annule uniquement notre ancienne surélévation, identifiée par ses maillages dédiés.
    private static void RestoreGantry(Transform pf)
    {
        var gantry = pf.Find("WorkshopRemodel/FittedCarters/FactoryGantry");
        if (gantry == null || gantry.Find("RampClearance") == null) return;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Industries/WorkshopCarters.fbx");
        if (source == null) return;
        foreach (var filter in gantry.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(Folder + "/Gantry", StringComparison.Ordinal)) continue;
            var original = source.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == filter.name);
            if (original == null) continue;
            Undo.RecordObject(filter, "Portique à sa hauteur initiale"); filter.sharedMesh = original.sharedMesh; Record(filter);
        }
        var label = pf.Find("WorkshopRemodel/GantryName");
        if (label != null && Mathf.Abs(pf.InverseTransformPoint(label.position).y - .190f) < .0001f)
        { Undo.RecordObject(label, "Plaque du portique abaissée"); label.position -= pf.TransformVector(Vector3.up * .045f); Record(label); }
        Undo.DestroyObjectImmediate(gantry.Find("RampClearance").gameObject);
    }

    private static void AlignSensor(TriggerComponent trigger, Transform pf, Vector3 p, float width, float depth)
    {
        Undo.RecordObjects(new Object[] { trigger, trigger.transform, trigger.DragPointSpline, trigger.DragPointSpline.Container }, "Capteur sur Ramp1");
        trigger.transform.position = pf.TransformPoint(p); trigger.transform.rotation = pf.rotation;
        float k = VisualPinball.Unity.Physics.ScaleInv;
        var corners = new[] { new Vector3(-width/2,0,-depth/2), new Vector3(width/2,0,-depth/2), new Vector3(width/2,0,depth/2), new Vector3(-width/2,0,depth/2) };
        trigger.DragPoints = corners.Select(c => new DragPointData(c.x / k, -c.z / k)).ToArray();
        var collider = trigger.GetComponent<TriggerColliderComponent>(); Undo.RecordObject(collider, "Hauteur du capteur Ramp1");
        // Le retour est en descente : les tirs rapides survolent légèrement le sol.
        // Le volume couvre les guides de la rampe, tout en restant au-dessus des billes au sol.
        collider.HitHeight = (trigger.name == IndustriesProduction.TextileExit ? CeilingClearance : .027f) / k;
        trigger.RebuildMeshes();
        foreach (var obj in new Object[] { trigger, trigger.transform, trigger.DragPointSpline, trigger.DragPointSpline.Container, collider }) Record(obj);
    }
    private static void HideObsoleteCovers(IndustriesVpeGame game)
    {
        var presentation = game.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<IndustriesPresentation>(true)).FirstOrDefault();
        if (presentation == null) return;
        var so = new SerializedObject(presentation); var hidden = so.FindProperty("hiddenRenderers");
        // Le clapet du tracé VPE initial est sous le nouveau virage ; ses supports le traversaient.
        // Le plafond et les guides prennent le relais. L'hôte reste disponible aux références VPE.
        var gate = game.GetComponentsInChildren<GateComponent>(true).FirstOrDefault(g => g.name == "Gate");
        var gateCollider = gate != null ? gate.GetComponent<GateColliderComponent>() : null;
        if (gateCollider != null) { Undo.RecordObject(gateCollider, "Ancien clapet de rampe"); gateCollider.enabled = false; Record(gateCollider); }
        foreach (var renderer in game.GetComponentsInChildren<RampComponent>(true)
            .Where(r => r.name == "Ramp6" || r.name == "Ramp7").SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true))
            .Concat(gate != null ? gate.GetComponentsInChildren<MeshRenderer>(true) : Array.Empty<MeshRenderer>()))
        {
            bool found = false;
            for (int i = 0; i < hidden.arraySize; i++) if (hidden.GetArrayElementAtIndex(i).objectReferenceValue == renderer) found = true;
            if (!found) { hidden.InsertArrayElementAtIndex(hidden.arraySize); hidden.GetArrayElementAtIndex(hidden.arraySize - 1).objectReferenceValue = renderer; }
            Undo.RecordObject(renderer, "Ancien couvre-rampe"); renderer.enabled = false; Record(renderer);
        }
        so.ApplyModifiedProperties(); Record(presentation);
    }
    private static void Record(Object obj) => PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
}
