using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Render-only mesh variants. Collider meshes, vertices, normals and placement remain authored.</summary>
public static class InstallNeutralSurfaceCoordinates
{
    private const string Folder = "Assets/Generated/NeutralSurfaceCoordinates";
    private const string Action = "Compléter les coordonnées des surfaces Neutral";

    [MenuItem("Pinball/Neutral/Compléter les coordonnées des surfaces")]
    public static void Install()
    {
        if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/Neutral.unity")
            throw new InvalidOperationException("Ouvrir Neutral hors Play.");
        var table = GameObject.Find("PinballTable");
        if (table == null) throw new InvalidOperationException("PinballTable absent.");
        var root = table.transform.Find("ReferencePlayfield/ProfessionalArt/PbrFinish");
        if (root == null) throw new InvalidOperationException("Finition PBR absente.");
        if (root.Find("SurfaceCoordinates") != null) return;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated", "NeutralSurfaceCoordinates");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(Action);
        int index = 0;
        var variants = new Dictionary<Mesh, Mesh>();
        Material rubber = null;
        try
        {
            foreach (var renderer in table.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy))
            {
                if (!renderer.sharedMaterials.Any(UsesNormals)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var source = filter.sharedMesh;
                // A microscopic normal is unnecessary on the deforming belt. Keep its analytic normals.
                if (renderer.GetComponent<SlingshotRubberAnimation>() != null)
                {
                    if (rubber == null)
                    {
                        rubber = UnityEngine.Object.Instantiate(renderer.sharedMaterial);
                        rubber.name = "AnimatedRubber_PBR";
                        rubber.SetTexture("_BumpMap", null);
                        rubber.DisableKeyword("_NORMALMAP");
                        AssetDatabase.CreateAsset(rubber, Folder + "/AnimatedRubber_PBR.mat");
                        Undo.RegisterCreatedObjectUndo(rubber, Action);
                    }
                    Undo.RecordObject(renderer, Action);
                    renderer.sharedMaterial = rubber;
                    continue;
                }
                if (source.uv.Length == source.vertexCount && source.tangents.Length == source.vertexCount) continue;
                if (!variants.TryGetValue(source, out var mesh))
                {
                    mesh = source.uv.Length == source.vertexCount ? UnityEngine.Object.Instantiate(source) : BoxMap(source);
                    mesh.name = source.name + "_SurfaceCoordinates";
                    mesh.RecalculateTangents();
                    AssetDatabase.CreateAsset(mesh, Folder + "/" + (index++).ToString("D3") + "_" + mesh.name.Replace('/', '_') + ".asset");
                    Undo.RegisterCreatedObjectUndo(mesh, Action);
                    variants.Add(source, mesh);
                }
                Undo.RecordObject(filter, Action);
                filter.sharedMesh = mesh;
            }
            var marker = new GameObject("SurfaceCoordinates");
            Undo.RegisterCreatedObjectUndo(marker, Action);
            marker.transform.SetParent(root, false);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Neutral] {variants.Count} maillages visuels avec UV/tangentes ; colliders inchangés.");
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    private static bool UsesNormals(Material material) => material != null &&
        (material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null ||
         material.HasProperty("_DetailNormalMap") && material.GetTexture("_DetailNormalMap") != null);

    // Box projection splits vertices only at projection changes; smooth authored normals are preserved.
    internal static Mesh BoxMap(Mesh source)
    {
        var positions = source.vertices;
        var normals = source.normals;
        var vertices = new List<Vector3>();
        var mappedNormals = new List<Vector3>();
        var uv = new List<Vector2>();
        var lookup = new Dictionary<(int, int), int>();
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        var submeshes = new List<int[]>();
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            var triangles = source.GetTriangles(sub);
            var mapped = new int[triangles.Length];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var n = Vector3.Cross(positions[triangles[i+1]] - positions[triangles[i]], positions[triangles[i+2]] - positions[triangles[i]]);
                int axis = Mathf.Abs(n.y) >= Mathf.Abs(n.x) && Mathf.Abs(n.y) >= Mathf.Abs(n.z) ? 1 : Mathf.Abs(n.x) >= Mathf.Abs(n.z) ? 0 : 2;
                int plane = axis * 2 + (n[axis] < 0 ? 1 : 0);
                for (int j = 0; j < 3; j++)
                {
                    int original = triangles[i+j];
                    if (!lookup.TryGetValue((original, plane), out int id))
                    {
                        id = vertices.Count;
                        lookup.Add((original, plane), id);
                        var p = positions[original];
                        vertices.Add(p);
                        mappedNormals.Add(normals.Length == positions.Length ? normals[original] : n.normalized);
                        var coords = axis == 0 ? new Vector2(p.z, p.y) : axis == 1 ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
                        if (n[axis] < 0) coords.x = -coords.x;
                        uv.Add(coords);
                    }
                    mapped[i+j] = id;
                }
            }
            submeshes.Add(mapped);
        }
        mesh.SetVertices(vertices);
        mesh.SetNormals(mappedNormals);
        mesh.SetUVs(0, uv);
        mesh.subMeshCount = source.subMeshCount;
        for (int sub = 0; sub < submeshes.Count; sub++) mesh.SetTriangles(submeshes[sub], sub, false);
        mesh.bounds = source.bounds;
        return mesh;
    }
}
