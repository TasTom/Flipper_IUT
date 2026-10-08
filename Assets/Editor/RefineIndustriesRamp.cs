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
    private const string Marker = "Ramp1Refined";
    public static readonly Vector3[] Profile = {
        new Vector3(.360f,0,-.580f), new Vector3(.363f,.0005f,-.572f),
        new Vector3(.367f,.004f,-.560f), new Vector3(.376f,.017f,-.537f),
        new Vector3(.396f,.041f,-.490f), new Vector3(.420f,.063f,-.445f),
        new Vector3(.440f,.075f,-.380f),
        new Vector3(.452f,.080f,-.30f), new Vector3(.422f,.084f,-.207f),
        new Vector3(.382f,.084f,-.139f), new Vector3(.339f,.079f,-.098f),
        new Vector3(.303f,.066f,-.081f), new Vector3(.260f,.047f,-.081f),
        new Vector3(.2411f,.03940175f,-.08096f)
    };

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
        if (layout.Find(Marker) != null) { Debug.Log("[Industries] Ramp1 déjà corrigée ; réglages manuels conservés."); return; }
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
            ClearGantry(pf);
            HideObsoleteCovers(game);
            AlignSensor(entry, pf, new Vector3(.363f,.002f,-.572f), .040f, .018f);
            AlignSensor(exit, pf, new Vector3(.273f,.052f,-.081f), .020f, .044f);
            foreach (var obsolete in game.GetComponentsInChildren<RampComponent>(true).Where(r => r.name == "ProductionWoodRamp").Select(r => r.gameObject)
                .Concat(sensors.Where(t => t.name == IndustriesProduction.WoodEntry || t.name == IndustriesProduction.WoodExit).Select(t => t.gameObject)).ToArray())
                Undo.DestroyObjectImmediate(obsolete);
            Undo.RecordObject(config, "Rampe de transformation commune");
            config.productionEnabled = true; config.sharedProductionRamp = true; EditorUtility.SetDirty(config);
            var marker = new GameObject(Marker); Undo.RegisterCreatedObjectUndo(marker, "Correction Ramp1"); marker.transform.SetParent(layout, false);
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssetIfDirty(config);
            Selection.activeGameObject = ramp.gameObject;
            Debug.Log("[Industries] Rampe bois et ses capteurs retirés ; Ramp1 dégagée et raccordée au retour gauche. Les deux matières passent par Ramp1. Ctrl+Z annule, Ctrl+S conserve.");
        }
        catch (Exception e) { Undo.RevertAllDownToGroup(group); Debug.LogError("[Industries] Correction annulée : " + e); }
    }

    // Le profil est dans le repère natif du plateau : la racine conserve sa pose et son échelle.
    public static void Rebuild(RampComponent ramp, Transform pf)
    {
        foreach (var filter in ramp.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name == "EdgeRails" || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) continue;
            string path = Folder + "/Ramp1" + filter.name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = Object.Instantiate(filter.sharedMesh); mesh.name = "Ramp1" + filter.name; AssetDatabase.CreateAsset(mesh, path); }
            Undo.RecordObject(filter, "Géométrie dédiée de Ramp1"); filter.sharedMesh = mesh; Record(filter); Undo.RecordObject(mesh, "Profil de Ramp1");
        }
        Undo.RecordObjects(new Object[] { ramp, ramp.DragPointSpline, ramp.DragPointSpline.Container, ramp.transform }, "Courbe et pente de Ramp1");
        float k = VisualPinball.Unity.Physics.ScaleInv;
        var position = ramp.transform.localPosition; position.y = 0; ramp.transform.localPosition = position;
        ramp._heightBottom = ramp._heightTop = 0;
        ramp._widthBottom = .036f / k; ramp._widthTop = 58f;
        ramp._leftWallHeightVisible = ramp._rightWallHeightVisible = .055f / k;
        ramp.DragPoints = Profile.Select(p => {
            var v = ramp.transform.InverseTransformPoint(pf.TransformPoint(p));
            return new DragPointData(new Vertex3D(v.x / k, -v.z / k, v.y / k)) { IsSmooth = true };
        }).ToArray();
        var collider = ramp.GetComponent<RampColliderComponent>(); Undo.RecordObject(collider, "Guides de Ramp1");
        collider.LeftWallHeight = collider.RightWallHeight = .055f / k;
        collider.OverwritePhysics = true; collider.Elasticity = .05f; collider.Friction = .08f; collider.Scatter = 0;
        ramp.RebuildMeshes();
        foreach (var filter in ramp.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null && AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(Folder, StringComparison.Ordinal))
            { EditorUtility.SetDirty(filter.sharedMesh); AssetDatabase.SaveAssetIfDirty(filter.sharedMesh); }
        foreach (var renderer in ramp.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name != "EdgeRails"))
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
        var lift = ramp.transform.InverseTransformVector(pf.TransformVector(Vector3.up * .055f));
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

    // La traverse initiale était à 111 mm : la bille sur la rampe la traversait.
    // Les pieds restent en place ; seules les parties hautes du décor sont rehaussées.
    public static void ClearGantry(Transform pf)
    {
        var gantry = pf.Find("WorkshopRemodel/FittedCarters/FactoryGantry");
        if (gantry == null || gantry.Find("RampClearance") != null) return;
        foreach (var filter in gantry.GetComponentsInChildren<MeshFilter>(true))
        {
            string path = Folder + "/Gantry" + filter.name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = Object.Instantiate(filter.sharedMesh); mesh.name = "Gantry" + filter.name;
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var p = pf.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                    p.y += Mathf.InverseLerp(.032f, .110f, p.y) * .045f;
                    vertices[i] = filter.transform.InverseTransformPoint(pf.TransformPoint(p));
                }
                mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path);
            }
            Undo.RecordObject(filter, "Traverse dégagée pour Ramp1"); filter.sharedMesh = mesh; Record(filter);
        }
        var label = pf.Find("WorkshopRemodel/GantryName");
        if (label != null) { Undo.RecordObject(label, "Plaque du portique rehaussée"); label.position += pf.TransformVector(Vector3.up * .045f); Record(label); }
        var marker = new GameObject("RampClearance"); Undo.RegisterCreatedObjectUndo(marker, "Passage sous portique"); marker.transform.SetParent(gantry, false);
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
        collider.HitHeight = (trigger.name == IndustriesProduction.TextileExit ? .055f : .027f) / k;
        trigger.RebuildMeshes();
        foreach (var obj in new Object[] { trigger, trigger.transform, trigger.DragPointSpline, trigger.DragPointSpline.Container, collider }) Record(obj);
    }
    private static void HideObsoleteCovers(IndustriesVpeGame game)
    {
        var presentation = game.GetComponent<IndustriesPresentation>();
        if (presentation == null) return;
        var so = new SerializedObject(presentation); var hidden = so.FindProperty("hiddenRenderers");
        foreach (var renderer in game.GetComponentsInChildren<RampComponent>(true)
            .Where(r => r.name == "Ramp6" || r.name == "Ramp7").SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)))
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
