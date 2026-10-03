using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Neutral's explicitly requested closed-outlane variant (GDD: table layout, ramp
/// returns and central drain). Adds separate side guides; never reshapes InlaneOuter.
/// Undoable, preserves subsequent manual adjustments, and never saves the scene.
/// </summary>
public static class CloseNeutralOutlanes
{
    private const string Folder = "Assets/Generated/NeutralClosedOutlanes";
    private const string Action = "Fermer les passages extérieurs de Neutral";

    [MenuItem("Pinball/Neutral/Fermer les passages extérieurs")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/Neutral.unity")
            throw new InvalidOperationException("Ouvrir Neutral hors Play pour fermer ses passages extérieurs.");

        var table = GameObject.Find("PinballTable")?.transform;
        var rails = table != null ? table.Find("Rails") : null;
        var routes = rails != null ? rails.Find("MechanismRevision") : null;
        if (routes == null) throw new InvalidOperationException("Les retours de rampe de Neutral sont absents.");
        if (rails.Find("ClosedOutlanes") != null)
        {
            Undo.IncrementCurrentGroup();
            int completion = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(Action);
            try { CompleteBackstops(table, rails.Find("ClosedOutlanes")); }
            catch { Undo.RevertAllDownToGroup(completion); throw; }
            finally { Undo.CollapseUndoOperations(completion); }
            Debug.Log("[Neutral] Passages extérieurs déjà fermés ; réglages manuels conservés.");
            return;
        }

        var guards = new[]
        {
            routes.Find("IUT_Route/IUT_UndercutGuard"),
            routes.Find("Vosges_UndercutGuard")
        };
        var drains = new[]
        {
            table.Find("Gameplay/Outlane_Drain_Left")?.GetComponent<Collider>(),
            table.Find("Gameplay/Outlane_Drain_Right")?.GetComponent<Collider>()
        };
        if (guards.Any(g => g == null || g.GetComponent<MeshCollider>()?.sharedMesh == null ||
                            g.GetComponent<MeshFilter>()?.sharedMesh == null) || drains.Any(d => d == null))
            throw new InvalidOperationException("Un support de retour ou un drain latéral manque ; aucune modification.");
        var steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/NeutralReference/Art/PolishedSteel.mat");
        var surface = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Generated/NeutralPresentation/RampSurface.physicMaterial");
        if (steel == null || surface == null) throw new InvalidOperationException("Matériaux de guide absents.");

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated", "NeutralClosedOutlanes");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(Action);
        try
        {
            var root = New("ClosedOutlanes", rails);
            foreach (bool left in new[] { true, false })
            {
                string side = left ? "Left" : "Right";
                var start = new Vector3(left ? -4.15f : 3.45f, 0, 8.20f);
                var first = new Vector3(start.x, 0, 7.80f);
                var second = new Vector3(left ? -3.70f : 2.94f, 0, 7.70f);
                var join = new Vector3(left ? -3.47f : 2.71f, 0, 7.40f);
                var path = new List<Vector3>();
                var normals = new List<Vector3>();
                for (int i = 0; i <= 64; i++)
                {
                    float t = i / 64f, u = 1 - t;
                    path.Add(u * u * u * start + 3 * u * u * t * first + 3 * u * t * t * second + t * t * t * join);
                    var tangent = 3 * u * u * (first - start) + 6 * u * t * (second - first) + 3 * t * t * (join - second);
                    normals.Add(new Vector3(tangent.z, 0, -tangent.x).normalized);
                }
                AddWall("OutlaneClosure_" + side, root, Wall(path, normals), steel, surface);

                // A floor-level ball must leave the descending return before the
                // clearance becomes smaller than its diameter. This diagonal guide
                // also ends below target 01: the earlier z=6.55 outlet pinched the ball
                // against that target. The verified outlet is z=6.15.
                string ramp = left ? "IUT" : "Vosges";
                var exit = new Vector3(left ? -2.73f : 1.97f, 0, 6.15f);
                var direction = exit - join;
                var normal = new Vector3(direction.z, 0, -direction.x).normalized;
                AddWall("ReturnUndersideDeflector_" + ramp, root,
                    Wall(new[] { join, exit }, new[] { normal, normal }), steel, surface);

                // Retain the upper and entrance supports. Replace only the lower
                // closed underside with the diagonal bypass; the ramp itself,
                // its gutters, rails, cover and exit sensor remain untouched.
                var guard = guards[left ? 0 : 1];
                var collider = guard.GetComponent<MeshCollider>();
                var filter = guard.GetComponent<MeshFilter>();
                var physical = CutLowerSupport(collider.sharedMesh, guard, table, left);
                physical.name = ramp + "_UndercutGuard_ClosedOutlane";
                var visual = CutLowerSupport(filter.sharedMesh, guard, table, left);
                visual.name = physical.name + "_Surface";
                visual.RecalculateTangents();
                physical = Asset(physical, physical.name);
                visual = Asset(visual, visual.name);
                Undo.RecordObjects(new UnityEngine.Object[] { collider, filter }, Action);
                collider.sharedMesh = physical;
                filter.sharedMesh = visual;
                EditorUtility.SetDirty(collider);
                EditorUtility.SetDirty(filter);
            }
            foreach (var drain in drains)
            {
                Undo.RecordObject(drain, Action);
                drain.enabled = false;
                EditorUtility.SetDirty(drain);
            }
            CompleteBackstops(table, root);
            Physics.SyncTransforms();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    private static void CompleteBackstops(Transform table, Transform root)
    {
        var flow = table.Find("ReferencePlayfield/FlowRefinement");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/NeutralReference/Art/PolishedSteel.mat");
        var surface = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Generated/NeutralPresentation/RampSurface.physicMaterial");
        if (material == null || surface == null || flow == null)
            throw new InvalidOperationException("Matériaux ou guides des protections latérales absents.");
        bool changed = false;
        foreach (bool left in new[] { true, false })
        {
            string side = left ? "Left" : "Right", name = "OutlaneBackstop_" + side;
            if (root.Find(name) != null) continue;
            var guide = flow.Find("InlaneOuter_" + side)?.GetComponent<MeshCollider>();
            if (guide?.sharedMesh == null || guide.sharedMesh.vertexCount != 260)
                throw new InvalidOperationException("Guide différent de la géométrie vérifiée ; conserver les réglages manuels.");
            var source = guide.sharedMesh.vertices;
            var v = new Vector3[source.Length];
            for (int i = 0; i < source.Length / 4; i++)
            {
                var a = table.InverseTransformPoint(guide.transform.TransformPoint(source[i * 4]));
                var b = table.InverseTransformPoint(guide.transform.TransformPoint(source[i * 4 + 1]));
                var outward = (b - a).normalized;
                if (left ? outward.x > 0 : outward.x < 0) outward = -outward;
                // A separate metal backstop sits immediately behind the original
                // guide. Its inner face meets the old outer face, leaving no ball
                // pocket. The old guide's shape, height and asset stay unchanged.
                // A landing rebound reached centre y=.69; .58 retains that ball.
                float thickness = Vector3.Distance(a, b);
                for (int j = 0; j < 4; j++)
                {
                    var p = table.InverseTransformPoint(guide.transform.TransformPoint(source[i * 4 + j]));
                    p += outward * thickness;
                    if (p.y > .01f) p.y = .58f;
                    v[i * 4 + j] = p;
                }
            }
            var mesh = new Mesh();
            mesh.vertices = v;
            mesh.triangles = guide.sharedMesh.triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AddWall(name, root, mesh, material, surface);
            changed = true;
        }
        // Retire misleading decorative legends only; keep their GameObjects and
        // any attached scripts intact, as required by the scene contract.
        foreach (var text in table.GetComponentsInChildren<TMPro.TMP_Text>(true).Where(t => t.text == "SORTIE"))
            foreach (var renderer in text.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled))
            {
                Undo.RecordObject(renderer, Action);
                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
                changed = true;
            }
        if (changed)
        {
            AssetDatabase.SaveAssets();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(table.gameObject.scene);
        }
    }

    private static Transform New(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, Action);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Mesh Wall(IList<Vector3> path, IList<Vector3> normals)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < path.Count; i++)
        {
            var n = normals[i] * .025f;
            vertices.Add(path[i] - n);
            vertices.Add(path[i] + n);
            vertices.Add(path[i] + n + Vector3.up * .32f);
            vertices.Add(path[i] - n + Vector3.up * .32f);
        }
        for (int i = 0; i < path.Count - 1; i++)
            for (int j = 0; j < 4; j++)
            {
                int a = i * 4 + j, b = i * 4 + (j + 1) % 4;
                triangles.AddRange(new[] { a, b, b + 4, a, b + 4, a + 4 });
            }
        int last = (path.Count - 1) * 4;
        triangles.AddRange(new[] { 0, 2, 1, 0, 3, 2, last, last + 1, last + 2, last, last + 2, last + 3 });
        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CutLowerSupport(Mesh source, Transform guard, Transform table, bool left)
    {
        var mesh = UnityEngine.Object.Instantiate(source);
        var vertices = source.vertices;
        var indices = source.triangles;
        var kept = new List<int>();
        for (int i = 0; i < indices.Length; i += 3)
        {
            bool lowerReturn = true;
            for (int j = 0; j < 3; j++)
            {
                var p = table.InverseTransformPoint(guard.TransformPoint(vertices[indices[i + j]]));
                if (p.z > 8.0f || (left ? p.x > -2.6f : p.x < 1.85f)) lowerReturn = false;
            }
            if (!lowerReturn) kept.AddRange(new[] { indices[i], indices[i + 1], indices[i + 2] });
        }
        mesh.triangles = kept.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddWall(string name, Transform parent, Mesh mesh, Material material, PhysicsMaterial surface)
    {
        mesh.name = name + "_Collision";
        var physical = Asset(mesh, mesh.name);
        var visual = InstallNeutralSurfaceCoordinates.BoxMap(physical);
        visual.name = name + "_Surface";
        visual.RecalculateTangents();
        visual = Asset(visual, visual.name);
        var host = New(name, parent).gameObject;
        host.AddComponent<MeshFilter>().sharedMesh = visual;
        host.AddComponent<MeshRenderer>().sharedMaterial = material;
        var collider = host.AddComponent<MeshCollider>();
        collider.sharedMesh = physical;
        collider.sharedMaterial = surface;
    }

    private static Mesh Asset(Mesh mesh, string name)
    {
        string path = Folder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        Undo.RegisterCreatedObjectUndo(mesh, Action);
        return mesh;
    }
}
