using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>User-requested replacement of Neutral's ramps and six reward targets.
/// Explicit menu only: no automatic scene construction and no scene save.</summary>
public static class RebuildNeutralShots
{
    const string Folder = "Assets/Generated/NeutralPresentation";
    const string UndoLabel = "Refonte rampes et six cibles Neutral";

    [MenuItem("Flipper/Finition Neutral/Remplacer les rampes et repositionner les six cibles")]
    public static void Apply()
    {
        if (Application.isPlaying || EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Neutral.unity")
            throw new InvalidOperationException("Ouvrir Neutral hors Play.");
        var table = GameObject.Find("PinballTable").transform;
        var gameplay = table.Find("Gameplay");
        if (gameplay.Find("Ramps_V2") != null)
            throw new InvalidOperationException("Ramps_V2 existe : préserver les ajustements manuels.");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Neutral_Ramps_V2.fbx");
        var targetAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Parts/Switches/Target_Rect_Thin.fbx");
        if (asset == null || targetAsset == null) throw new InvalidOperationException("FBX requis absent.");
        var targets = gameplay.GetComponentsInChildren<SubjectTarget>();
        if (targets.Length != 6) throw new InvalidOperationException("Attendu exactement six hôtes de récompense.");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(UndoLabel);
        try
        {
            // Their replacement is explicitly requested. Remove both visuals AND old colliders.
            foreach (string name in new[] { "Ramp_Vosges", "Ramp_IUT", "Retour_Vosges", "Retour_IUT" })
            {
                var old = gameplay.Find(name);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            }
            var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, gameplay);
            Undo.RegisterCreatedObjectUndo(root, UndoLabel); root.name = "Ramps_V2";
            var steel = FinishMaterial("RampSteel", new Color(.34f, .43f, .48f), .85f, .6f);
            var cyan = FinishMaterial("RampCyan", new Color(.12f, .75f, .72f), .65f, .55f);
            var amber = FinishMaterial("RampAmber", new Color(.96f, .55f, .12f), .65f, .55f);
            var physics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Folder + "/RampSurface.physicMaterial");
            if (physics == null)
            {
                physics = new PhysicsMaterial("RampSurface") { dynamicFriction = .025f, staticFriction = .025f,
                    bounciness = 0, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
                AssetDatabase.CreateAsset(physics, Folder + "/RampSurface.physicMaterial");
            }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                bool rim = mf.name.Contains("_rim_");
                mf.GetComponent<Renderer>().sharedMaterial = rim ? (mf.name.Contains("IUT") ? amber : cyan) : steel;
                if (!rim)
                {
                    var col = Undo.AddComponent<MeshCollider>(mf.gameObject);
                    col.sharedMesh = mf.sharedMesh; col.convex = false; col.sharedMaterial = physics;
                }
            }
            RefreshRampMeshes();
            foreach (bool left in new[] { true, false })
            {
                string prefix = "Ramp_" + (left ? "Vosges" : "IUT");
                // Gate at the flared mouth, then beyond the elevated U-turn.
                PlaceGate(gameplay.Find(prefix + "_In"), left ? new Vector3(-4.39f,.30f,10.82f) : new Vector3(-6.1f,.30f,12.02f), Vector3.forward, 1.25f);
                PlaceGate(gameplay.Find(prefix + "_Out"), left ? new Vector3(4.7f,1.53f,14.3f) : new Vector3(-4.2f,1.79f,16.5f), Vector3.back, .9f);
            }

            string[] names = { "Target_Matieres_01", "Target_Matieres_02", "Target_Matieres_03", "Target_Cafe_01", "Target_Projet_01", "Target_Java_01" };
            string[] labels = { "ALGO", "WEB", "BDD", "CAFÉ", "PROJET", "JAVA" };
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            for (int i = 0; i < names.Length; i++)
            {
                var host = gameplay.Find(names[i]);
                if (host == null) throw new InvalidOperationException(names[i] + " absent.");
                Undo.RecordObject(host, UndoLabel);
                host.localPosition = new Vector3(i < 3 ? -2.9f : 3.2f, 0, (i < 3 ? 7.8f : 10.2f) + (i % 3) * .9f);
                host.localRotation = Quaternion.Euler(0, i < 3 ? -75 : 75, 0);
                foreach (Transform child in host.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(targetAsset, host);
                Undo.RegisterCreatedObjectUndo(model, UndoLabel); model.name = "Target_Pro";
                // Sink the mounting stem below the playfield instead of leaving it floating.
                model.transform.localPosition = new Vector3(.025f, .09f, -.025f);
                var mats = new[] { "TargetYellow_Mat", "Metal_Mat", "BumperRed_Mat" }
                    .Select(n => AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + n + ".mat")).ToArray();
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                    r.sharedMaterials = Enumerable.Range(0, r.sharedMaterials.Length).Select(j => mats[Math.Min(j, 2)]).ToArray();
                foreach (var c in host.GetComponents<Collider>()) Undo.DestroyObjectImmediate(c);
                var col = Undo.AddComponent<BoxCollider>(host.gameObject);
                col.center = new Vector3(0, .30f, 0); col.size = new Vector3(.40f, .60f, .15f);
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Undo.RegisterCreatedObjectUndo(lamp, UndoLabel); lamp.name = "RewardInsert"; lamp.transform.SetParent(host, false);
                lamp.transform.localPosition = new Vector3(0, .035f, -.52f);
                lamp.transform.localScale = new Vector3(.32f, .008f, .32f);
                Undo.DestroyObjectImmediate(lamp.GetComponent<Collider>());
                lamp.GetComponent<Renderer>().sharedMaterial = amber;
                var so = new SerializedObject(host.GetComponent<SubjectTarget>());
                var vis = so.FindProperty("visuels"); vis.arraySize = 1; vis.GetArrayElementAtIndex(0).objectReferenceValue = lamp.GetComponent<Renderer>();
                so.ApplyModifiedProperties();
                var text = new GameObject("RewardLabel"); Undo.RegisterCreatedObjectUndo(text, UndoLabel); text.transform.SetParent(host, false);
                var tmp = Undo.AddComponent<TextMeshPro>(text); tmp.font = font; tmp.text = labels[i];
                tmp.fontSize = 1.3f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(.83f,.94f,.94f);
                tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.rectTransform.sizeDelta = new Vector2(.72f,.25f);
                text.transform.localPosition = new Vector3(0,.045f,-.91f); text.transform.localRotation = Quaternion.Euler(90,0,0);
            }
            // Hide superseded printed target references, keeping unrelated hosts active.
            var inserts = gameplay.Find("Inserts");
            if (inserts != null) foreach (Transform child in inserts)
                if (child.name.StartsWith("Insert_Target_") || child.name.StartsWith("Insert_Rampe_")) HideRenderers(child);
            var presentation = table.Find("VosgesPresentation");
            if (presentation != null) foreach (Transform child in presentation)
                if (child.name.StartsWith("TargetLabel_") || child.name == "05_AmberMarkings") HideRenderers(child);
            AssetDatabase.SaveAssets();
            Debug.Log("[Neutral V2] Deux pistes continues et six cibles posées. Ctrl+Z annule. Tester avant Ctrl+S.");
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
        finally { Undo.CollapseUndoOperations(group); }
    }

    // The user explicitly requested a whole-table layout inspired by images.jpg.
    // Kept separate from the additive finish menu to avoid overwriting authored placements on rerun.
    public static void ApplyReferenceArrangement()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit mode required.");
        var table = GameObject.Find("PinballTable").transform;
        var gp = table.Find("Gameplay");
        foreach (string name in new[] { "Loop_Montant", "Loop_Descendant" })
        { var old = gp.Find(name); if (old != null) Undo.DestroyObjectImmediate(old.gameObject); }
        // Former rear wall is now in the middle of the stretched table: open the upper field.
        var oldWall = table.Find("Table/Pinball_Table/Walls/Top");
        if (oldWall != null) { HideRenderers(oldWall); foreach(var c in oldWall.GetComponents<Collider>()) { Undo.RecordObject(c,UndoLabel); c.enabled=false; } }
        var parts = table.Find("Table/Pinball_Table");
        Vector3[] positions = { new Vector3(-1.3f,0,16.6f), new Vector3(.8f,0,18f), new Vector3(-1.8f,0,18.4f) };
        for(int i=0;i<3;i++)
        {
            string name="Bumper_0"+(i+1);var host=gp.Find(name);var delta=positions[i]-host.localPosition;
            Undo.RecordObject(host,UndoLabel);host.localPosition=positions[i];
            foreach(Transform t in parts) if(t.name.StartsWith(name+"_")) {Undo.RecordObject(t,UndoLabel);t.position+=table.TransformVector(delta);}
            var halo=table.Find("VosgesPresentation/"+name+"_Halo");
            if(halo!=null){Undo.RecordObject(halo,UndoLabel);halo.localPosition+=delta;}
            string insertName=i==0?"Insert_Bumper_G":i==1?"Insert_Bumper_D":"Insert_Bumper_M";
            var insert=gp.Find("Inserts/"+insertName);if(insert!=null){Undo.RecordObject(insert,UndoLabel);insert.position+=table.TransformVector(delta);}
        }
        var boss=gp.Find("Boss_ProjetFinal");Undo.RecordObject(boss,UndoLabel);boss.localPosition=new Vector3(0,0,19.8f);
        var misplaced=boss.Find("Caisson/Leveler_Piece");if(misplaced!=null)HideRenderers(misplaced);
        var glass=table.Find("Vitre");Undo.RecordObject(glass,UndoLabel);var glassPos=glass.localPosition;glassPos.y=2.45f;glass.localPosition=glassPos;
        var lanes=gp.Find("TopLanes");
        foreach(Transform child in lanes)
        {
            if(child.name.StartsWith("Rollover_"))
            {
                int index=int.Parse(child.name.Substring(child.name.Length-2))-1;float angle=(65+index*25)*Mathf.Deg2Rad;
                Undo.RecordObject(child,UndoLabel);child.position=table.TransformPoint(new Vector3(-.375f-6.225f*Mathf.Cos(angle),.25f,16.8f+4.275f*Mathf.Sin(angle)));
            }
            else {HideRenderers(child);foreach(var c in child.GetComponents<Collider>()){Undo.RecordObject(c,UndoLabel);c.enabled=false;}}
        }
        PlaceGate(gp.Find("Loop_At_Entry"),new Vector3(-6.6f,.28f,17.1f),Vector3.forward,1.0f);
        PlaceGate(gp.Find("Loop_At_Exit"),new Vector3(5.85f,.28f,17.1f),Vector3.back,1.0f);
        foreach(var item in new[]{("VosgesShot",-4.4f,9.8f),("IutShot",-6.1f,11.1f),("BumperBank",0f,17f),("ProjetFinal",0f,19.0f)})
        {var label=table.Find("VosgesPresentation/"+item.Item1);if(label!=null){Undo.RecordObject(label,UndoLabel);label.localPosition=new Vector3(item.Item2,.045f,item.Item3);}}
        var oldArrows=table.Find("VosgesPresentation/04_IceMarkings");if(oldArrows!=null)HideRenderers(oldArrows);
    }

    static void PlaceGate(Transform gate, Vector3 position, Vector3 forward, float width)
    {
        if (gate == null) throw new InvalidOperationException("RampGate absent.");
        Undo.RecordObject(gate, UndoLabel); gate.localPosition = position; gate.localRotation = Quaternion.LookRotation(forward);
        var col = gate.GetComponent<BoxCollider>(); Undo.RecordObject(col, UndoLabel);
        col.isTrigger = true; col.center = Vector3.zero; col.size = new Vector3(width,.48f,.12f);
        var ramp = gate.GetComponent<RampGate>();
        if(ramp != null) { var so = new SerializedObject(ramp); so.FindProperty("requireForwardPassage").boolValue = true; so.ApplyModifiedProperties(); }
    }

    public static void RefreshRampMeshes()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit mode required.");
        var root = GameObject.Find("PinballTable/Gameplay/Ramps_V2");
        if (root == null) throw new InvalidOperationException("Ramps_V2 absent.");
        var cover = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/RampCover.mat");
        if (cover == null)
        {
            cover = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "RampCover" };
            cover.SetColor("_BaseColor", new Color(.30f,.72f,.76f,.12f));
            cover.SetFloat("_Surface", 1); cover.SetFloat("_Blend", 0);
            cover.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            cover.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            cover.SetFloat("_ZWrite", 0); cover.SetFloat("_Cull", 0); cover.SetFloat("_Smoothness",.8f);
            cover.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); cover.renderQueue = 3000;
            cover.SetOverrideTag("RenderType", "Transparent");
            AssetDatabase.CreateAsset(cover, Folder + "/RampCover.mat");
        }
        foreach(var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            bool rim = mf.name.Contains("_rim_"); bool lid = mf.name.EndsWith("_cover");
            var renderer = mf.GetComponent<Renderer>(); Undo.RecordObject(renderer, UndoLabel);
            renderer.sharedMaterial = lid ? cover : AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" +
                (rim ? (mf.name.Contains("IUT") ? "RampAmber" : "RampCyan") : "RampSteel") + ".mat");
            if(lid) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if(!rim)
            {
                var col = mf.GetComponent<MeshCollider>(); if(col == null) col = Undo.AddComponent<MeshCollider>(mf.gameObject);
                Undo.RecordObject(col, UndoLabel); col.sharedMesh = mf.sharedMesh; col.convex = false;
                col.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Folder + "/RampSurface.physicMaterial");
            }
        }
        var contact = root.GetComponent<RampSurfaceContact>();
        if (contact == null) contact = Undo.AddComponent<RampSurfaceContact>(root);
        var so = new SerializedObject(contact);
        so.FindProperty("surface").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Folder + "/RampSurface.physicMaterial");
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    static void HideRenderers(Transform root)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { Undo.RecordObject(r, UndoLabel); r.enabled = false; }
    }

    static Material FinishMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = Folder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path); return mat;
    }
}
