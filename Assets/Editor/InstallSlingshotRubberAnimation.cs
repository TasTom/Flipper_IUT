using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Additive, Undoable wiring of the visual rubber animation. Never saves a scene.</summary>
public static class InstallSlingshotRubberAnimation
{
    private const string Folder = "Assets/Generated/NeutralRubberAnimation";
    private const string Action = "Animer le caoutchouc des slingshots";

    [MenuItem("Pinball/Neutral/Installer l'animation du caoutchouc")]
    public static void Install()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Neutral.unity") throw new InvalidOperationException("Neutral required.");
        GameObject table = GameObject.Find("PinballTable");
        if (table == null) throw new InvalidOperationException("PinballTable missing.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated", "NeutralRubberAnimation");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(Action);
        bool sceneChanged = false;
        try
        {
            string path = Folder + "/NeutralSlingshotRubberConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<SlingshotRubberConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SlingshotRubberConfig>();
                AssetDatabase.CreateAsset(config, path);
                Undo.RegisterCreatedObjectUndo(config, Action);
            }
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform host = table.transform.Find("Gameplay/Slingshot_" + side);
                Transform assembly = host != null ? host.Find("MechanicalAssembly") : null;
                Transform rubber = assembly != null ? assembly.Find("SlingRubber_" + side) : null;
                if (rubber == null) throw new InvalidOperationException("Missing rubber for " + side);
                var filter = rubber.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException("Missing rubber mesh for " + side);

                // A second invocation leaves an installed animation and its authoring intact.
                var animation = rubber.GetComponent<SlingshotRubberAnimation>();
                if (animation != null) continue;
                string meshPath = Folder + "/SlingRubber_" + side + "_Deformable.asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                float vertexScale = Mathf.Max(Mathf.Abs(filter.transform.lossyScale.x),
                    Mathf.Abs(filter.transform.lossyScale.y), Mathf.Abs(filter.transform.lossyScale.z));
                if (mesh == null)
                {
                    mesh = Subdivide(filter, table.transform, 0.075f, vertexScale);
                    mesh.name = "SlingRubber_" + side + "_Deformable";
                    AssetDatabase.CreateAsset(mesh, meshPath);
                    Undo.RegisterCreatedObjectUndo(mesh, Action);
                }
                Undo.RecordObject(filter, Action);
                Undo.RecordObject(filter.transform, Action);
                // FBX millimetric vertex coordinates and a scale near 1666 are valid for
                // static rendering, but too small for stable dynamic normal calculation.
                // This reciprocal conversion preserves every vertex's world position.
                filter.transform.localScale /= vertexScale;
                filter.sharedMesh = mesh;
                animation = Undo.AddComponent<SlingshotRubberAnimation>(rubber.gameObject);
                var serialized = new SerializedObject(animation);
                serialized.FindProperty("source").objectReferenceValue = host.GetComponent<Slingshot>();
                serialized.FindProperty("rubber").objectReferenceValue = filter;
                serialized.FindProperty("config").objectReferenceValue = config;
                serialized.FindProperty("playfield").objectReferenceValue = table.transform;
                serialized.FindProperty("startPost").objectReferenceValue = assembly.Find("SlingPost_1");
                serialized.FindProperty("endPost").objectReferenceValue = assembly.Find("SlingPost_2");
                serialized.FindProperty("backPost").objectReferenceValue = assembly.Find("SlingPost_3");
                serialized.ApplyModifiedProperties();
                sceneChanged = true;
            }
            AssetDatabase.SaveAssets();
            if (sceneChanged) EditorSceneManager.MarkSceneDirty(scene);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    private static Mesh Subdivide(MeshFilter filter, Transform frame, float maxEdge, float vertexScale)
    {
        Mesh source = filter.sharedMesh;
        var positions = new List<Vector3>(source.vertices);
        for (int i = 0; i < positions.Count; i++) positions[i] *= vertexScale;
        var normals = new List<Vector3>(source.normals);
        var uv = new List<Vector2>(source.uv);
        bool hasNormals = normals.Count == positions.Count;
        bool hasUv = uv.Count == positions.Count;
        var submeshes = new List<int[]>();
        for (int i = 0; i < source.subMeshCount; i++) submeshes.Add(source.GetTriangles(i));
        Matrix4x4 matrix = frame.worldToLocalMatrix * filter.transform.localToWorldMatrix *
            Matrix4x4.Scale(Vector3.one / vertexScale);
        float squared = maxEdge * maxEdge;

        // Split only long edges, including the matching edge of its neighbouring triangle.
        // Circular sections around the posts retain their imported shape and winding.
        for (int iteration = 0; iteration < 8; iteration++)
        {
            bool changed = false;
            var midpoints = new Dictionary<ulong, int>();
            int Mid(int a, int b)
            {
                uint low = (uint)Math.Min(a, b), high = (uint)Math.Max(a, b);
                ulong key = ((ulong)low << 32) | high;
                if (midpoints.TryGetValue(key, out int old)) return old;
                int index = positions.Count;
                positions.Add((positions[a] + positions[b]) * 0.5f);
                if (hasNormals) normals.Add((normals[a] + normals[b]).normalized);
                if (hasUv) uv.Add((uv[a] + uv[b]) * 0.5f);
                midpoints.Add(key, index);
                return index;
            }
            bool Long(int a, int b) => matrix.MultiplyVector(positions[a] - positions[b]).sqrMagnitude > squared;
            for (int s = 0; s < submeshes.Count; s++)
            {
                var input = submeshes[s];
                var output = new List<int>(input.Length);
                void Triangle(int a, int b, int c) { output.Add(a); output.Add(b); output.Add(c); }
                for (int i = 0; i < input.Length; i += 3)
                {
                    int a = input[i], b = input[i + 1], c = input[i + 2];
                    int mask = (Long(a, b) ? 1 : 0) | (Long(b, c) ? 2 : 0) | (Long(c, a) ? 4 : 0);
                    if (mask == 0) { Triangle(a, b, c); continue; }
                    changed = true;
                    int ab = (mask & 1) != 0 ? Mid(a, b) : -1;
                    int bc = (mask & 2) != 0 ? Mid(b, c) : -1;
                    int ca = (mask & 4) != 0 ? Mid(c, a) : -1;
                    switch (mask)
                    {
                        case 1: Triangle(a, ab, c); Triangle(ab, b, c); break;
                        case 2: Triangle(b, bc, a); Triangle(bc, c, a); break;
                        case 4: Triangle(c, ca, b); Triangle(ca, a, b); break;
                        case 3: Triangle(b, bc, ab); Triangle(a, ab, c); Triangle(ab, bc, c); break;
                        case 6: Triangle(c, ca, bc); Triangle(b, bc, a); Triangle(bc, ca, a); break;
                        case 5: Triangle(a, ab, ca); Triangle(c, ca, b); Triangle(ca, ab, b); break;
                        case 7: Triangle(a, ab, ca); Triangle(b, bc, ab); Triangle(c, ca, bc); Triangle(ab, bc, ca); break;
                    }
                }
                submeshes[s] = output.ToArray();
            }
            if (!changed) break;
        }
        var mesh = new Mesh();
        if (positions.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(positions);
        if (hasNormals) mesh.SetNormals(normals);
        if (hasUv) mesh.SetUVs(0, uv);
        mesh.subMeshCount = submeshes.Count;
        for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
        if (!hasNormals) mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (hasUv) mesh.RecalculateTangents();
        return mesh;
    }
}
