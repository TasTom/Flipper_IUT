using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>GDD §Performances : explicit import-time collision preparation, no geometry or collider changes.</summary>
public static class PrepareNeutralCollisionMeshes
{
    [MenuItem("Pinball/Neutral/Préparer les meshes de collision")]
    public static void Prepare()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/Neutral.unity")
            throw new InvalidOperationException("Neutral hors Play requise.");
        var table = GameObject.Find("PinballTable");
        if (table == null) throw new InvalidOperationException("PinballTable absente.");
        var colliders = table.GetComponentsInChildren<MeshCollider>(true).Where(c => c.sharedMesh != null).ToArray();
        var models = new Dictionary<string, (bool convex, bool triangles)>();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Préparer les collisions Neutral");
        int native = 0, imported = 0;
        try
        {
            foreach (var use in colliders.GroupBy(c => c.sharedMesh))
            {
                var mesh = use.Key;
                string path = AssetDatabase.GetAssetPath(mesh);
                bool convex = use.Any(c => c.convex), triangles = use.Any(c => !c.convex);
                if (AssetImporter.GetAtPath(path) is ModelImporter)
                {
                    models.TryGetValue(path, out var modes);
                    models[path] = (modes.convex || convex, modes.triangles || triangles);
                    continue;
                }
                if (!path.StartsWith("Assets/")) continue;
                var serialized = new SerializedObject(mesh);
                var convexFlag = serialized.FindProperty("m_PreBakeConvexCollisionMesh");
                var triangleFlag = serialized.FindProperty("m_PreBakeTriangleCollisionMesh");
                if (convexFlag == null || triangleFlag == null) throw new InvalidOperationException("Pre-bake flags absent on " + path);
                if ((!convex || convexFlag.boolValue) && (!triangles || triangleFlag.boolValue)) continue;
                Undo.RecordObject(mesh, "Préparer les collisions Neutral");
                if (convex) convexFlag.boolValue = true;
                if (triangles) triangleFlag.boolValue = true;
                serialized.ApplyModifiedProperties();
                native++;
            }
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var pair in models)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(pair.Key);
                var convex = typeof(ModelImporter).GetProperty("preBakeConvexCollisionMesh", flags);
                var triangle = typeof(ModelImporter).GetProperty("preBakeTriangleCollisionMesh", flags);
                if (convex == null || triangle == null) throw new InvalidOperationException("Unity collision import settings unavailable.");
                bool updateConvex = pair.Value.convex && !(bool)convex.GetValue(importer);
                bool updateTriangle = pair.Value.triangles && !(bool)triangle.GetValue(importer);
                if (!updateConvex && !updateTriangle) continue;
                Undo.RecordObject(importer, "Préparer les collisions Neutral");
                if (updateConvex) convex.SetValue(importer, true);
                if (updateTriangle) triangle.SetValue(importer, true);
                importer.SaveAndReimport();
                imported++;
            }
            AssetDatabase.SaveAssets();
            string result = $"Collision preparation: nativeMeshes={native}, modelImports={imported}, colliderMeshes={colliders.Select(c=>c.sharedMesh).Distinct().Count()}";
            System.IO.File.WriteAllText("Tools/unity/out/vpe-collision-preparation.txt", result);
            Debug.Log(result);
        }
        finally { Undo.CollapseUndoOperations(group); }
    }
}
