using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GDD: direction artistique, lisibilité des tirs, bumpers et audio.
/// Additive, undoable presentation for the measured Neutral table. Never saves the scene.
/// Existing geometry, physics, camera, materials and nonempty references are preserved.
/// </summary>
public static class FinishNeutralPresentation
{
    private const string Folder = "Assets/Generated/NeutralPresentation";
    private const string RootName = "VosgesPresentation";
    private const string UndoName = "Finition Vosges Mania";
    private static readonly Color Navy = Hex("102635");
    private static readonly Color Contour = Hex("244353");
    private static readonly Color Teal = Hex("367780");
    private static readonly Color Cyan = Hex("70DDD6");
    private static readonly Color Snow = Hex("D5E8E8");
    private static readonly Color Amber = Hex("F6B95C");

    [MenuItem("Flipper/Finition Neutral/Ajouter l'habillage et relier les champs vides")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/Neutral.unity")
            throw new InvalidOperationException("Ouvrir Neutral hors mode Play avant la finition.");
        var table = GameObject.Find("PinballTable");
        if (table == null) throw new InvalidOperationException("PinballTable absent.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        try
        {
            EnsureFolder(Folder);
            if (table.transform.Find(RootName) == null)
            {
                var root = NewObject(RootName, table.transform).transform;
                CreateArtwork(root);
                CreateLabels(root);
                CreateHardware(root);
            }
            CreateTrim(table.transform.Find(RootName));
            FillMissingReferences(table.transform);
            AssetDatabase.SaveAssets();
            Debug.Log("[Neutral] Habillage ajouté, références vides reliées. Physique et caméra conservées. Ctrl+S pour enregistrer la scène.");
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    private static void CreateTrim(Transform root)
    {
        if (root.Find("MachinedTrim") == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Neutral_Trim.fbx");
            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                Undo.RegisterCreatedObjectUndo(go, UndoName);
                go.name = "MachinedTrim";
                var dark = FinishMaterial("Anodized", Hex("10212C"), .65f, .42f);
                var chrome = FinishMaterial("Chrome", Hex("ADC5CB"), .85f, .76f);
                var cyan = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Ink_70DDD6.mat");
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = r.name.StartsWith("Cyan") ? cyan : r.name.StartsWith("Chrome") ? chrome : dark;
            }
        }
        if (root.Find("StudioGround") == null)
        {
            var ink = new Ink("StudioGround", Hex("090F18"), -1.6f);
            ink.Quad(-100, -100, 100, 100);
            ink.Build(root);
        }
    }

    private static Material FinishMaterial(string name, Color color, float metal, float smoothness)
    {
        string path = Folder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metal);
        mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void CreateArtwork(Transform root)
    {
        // All ink is a few millimetres above the existing playfield; no collider is created.
        var baseInk = new Ink("01_NavyPrint", Navy, .006f);
        baseInk.Quad(-6.65f, 1.9f, 6.55f, 21.65f);
        baseInk.Build(root);

        var contours = new Ink("02_TopographicContours", Contour, .010f);
        // Quiet contour lines leave the central ball path readable.
        for (int i = 0; i < 13; i++)
        {
            var points = new List<Vector2>();
            for (int j = 0; j <= 48; j++)
            {
                float x = -6.5f + j * 13f / 48;
                float z = 8.2f + i * .77f + .34f * Mathf.Sin(x * .9f + i * .37f)
                    + .18f * Mathf.Sin(x * 1.8f - i * .23f);
                points.Add(new Vector2(x, z));
            }
            contours.Path(points, .018f);
        }
        contours.Ring(0, 8.9f, 2.7f, .025f);
        contours.Ring(0, 8.9f, 2.85f, .012f);
        contours.Build(root);

        var ridges = new Ink("03_VosgesSilhouette", Teal, .014f);
        float[] xs = { -6.5f, -5.5f, -4.6f, -3.5f, -2.6f, -1.1f, .2f, 1.5f, 2.7f, 4f, 5f, 6.5f };
        float[] zs = { 19.5f, 20.3f, 19.8f, 21.15f, 20.25f, 20.65f, 19.85f, 21.25f, 20.1f, 20.8f, 19.8f, 20.4f };
        for (int i = 0; i < xs.Length - 1; i++)
            ridges.Polygon(new Vector2(xs[i], 18.9f), new Vector2(xs[i + 1], 18.9f), new Vector2(xs[i + 1], zs[i + 1]), new Vector2(xs[i], zs[i]));
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 9; i++) Pine(ridges, side * (5.9f + .18f * (i % 2)), 9 + i * 1.05f, .35f, .72f);
        ridges.Build(root);

        var ice = new Ink("04_IceMarkings", Cyan, .018f);
        ice.Path(new[] { new Vector2(-6.5f, 2.0f), new Vector2(-6.5f, 20.9f), new Vector2(-6.05f, 21.4f), new Vector2(6.05f, 21.4f), new Vector2(6.5f, 20.9f), new Vector2(6.5f, 2.0f) }, .036f);
        // The two shot paths end below the existing entrances; ramps themselves are untouched.
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side < 0 ? -3.6f : 3.2f;
            for (int i = 0; i < 3; i++) Arrow(ice, x, 7.2f + i * .4f, .38f, .18f);
            ice.Path(new[] { new Vector2(side * 2f, 2f), new Vector2(side * 3.4f, 3.4f), new Vector2(side * 3.4f, 4.2f) }, .028f);
        }
        ice.Path(new[] { new Vector2(-1.6f, 9.65f), new Vector2(-.65f, 10.55f), new Vector2(-.12f, 10.0f), new Vector2(.52f, 10.8f), new Vector2(1.6f, 9.65f) }, .055f);
        ice.Path(new[] { new Vector2(-1.6f, 9.6f), new Vector2(1.6f, 9.6f) }, .025f);
        ice.Build(root);

        var warm = new Ink("05_AmberMarkings", Amber, .022f);
        warm.Path(new[] { new Vector2(-1.6f, 3.25f), new Vector2(1.6f, 3.25f) }, .032f);
        warm.Path(new[] { new Vector2(-1.8f, 5.25f), new Vector2(1.8f, 5.25f) }, .025f);
        foreach (float x in new[] { -3.17f, -2.37f, -1.57f, 1.47f, 2.27f, 3.07f })
        {
            warm.Ring(x, 5.72f, .2f, .03f);
            Arrow(warm, x, 6.02f, .23f, .12f);
        }
        warm.Build(root);

        var snow = new Ink("06_SnowPeaks", Snow, .026f);
        snow.Polygon(new Vector2(-3.85f, 20.72f), new Vector2(-3.5f, 21.15f), new Vector2(-3.05f, 20.7f), new Vector2(-3.5f, 20.87f));
        snow.Polygon(new Vector2(1.15f, 20.85f), new Vector2(1.5f, 21.25f), new Vector2(1.95f, 20.77f), new Vector2(1.5f, 20.95f));
        snow.Build(root);

        var gameplay = root.parent.Find("Gameplay");
        for (int i = 1; i <= 3; i++)
        {
            var bumper = gameplay.Find("Bumper_0" + i);
            if (bumper == null) continue;
            var p = root.InverseTransformPoint(bumper.position);
            var halo = new Ink("Bumper_0" + i + "_Halo", Amber, .03f);
            halo.Ring(p.x, p.z, .83f, .065f);
            halo.Ring(p.x, p.z, .99f, .012f);
            halo.Build(root);
        }
    }

    private static void CreateLabels(Transform root)
    {
        Label(root, "Brand", "VOSGES", 0, 8.9f, 5.4f, .9f, 7.2f, Snow, FontStyles.Bold);
        Label(root, "BrandSubtitle", "M A N I A", 0, 8.05f, 4.8f, .7f, 5.4f, Cyan, FontStyles.Bold);
        Label(root, "Origin", "IUT  /  SAINT-DIÉ-DES-VOSGES", 0, 7.45f, 4.5f, .35f, 1.6f, Snow);
        Label(root, "Semestre", "UN SEMESTRE. TROIS BILLES.", 0, 4.7f, 3.2f, .35f, 1.45f, Amber);
        Label(root, "Instructions", "VISEZ LES CIBLES ALLUMÉES\nENCHAÎNEZ LES RAMPES", 0, 4.03f, 3.3f, .65f, 1.3f, Snow);
        Label(root, "LaunchHint", "MAINTENIR\nRELÂCHER\nLANCER", 7.02f, 4.0f, .7f, 1.2f, 1.2f, Amber);
        Label(root, "VosgesShot", "VOSGES", -4.15f, 7.25f, 1.6f, .4f, 1.9f, Cyan, FontStyles.Bold);
        Label(root, "IutShot", "IUT", 3.95f, 7.25f, 1.5f, .4f, 2.0f, Amber, FontStyles.Bold);
        Label(root, "BumperBank", "LES TROIS SOMMETS", 0, 14.05f, 4.4f, .4f, 2.0f, Snow);
        Label(root, "ProjetFinal", "PROJET FINAL", 0, 16.9f, 3.3f, .42f, 2.4f, Amber, FontStyles.Bold);
        string[] names = { "ALGO", "WEB", "BDD", "CAFÉ", "PROJET", "JAVA" };
        float[] x = { -3.17f, -2.37f, -1.57f, 1.47f, 2.27f, 3.07f };
        for (int i = 0; i < names.Length; i++) Label(root, "TargetLabel_" + i, names[i], x[i], 5.2f, .73f, .3f, 1.1f, Snow);
    }

    private static void CreateHardware(Transform root)
    {
        var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Metal_Mat.mat");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Parts/Screws/Bolt_10-32_-_Slotted_Flanged.fbx");
        if (asset == null) { Debug.LogWarning("[Neutral] Boulon Parts absent : finition des montants ignorée."); return; }
        foreach (float x in new[] { -7.91f, 7.73f })
        {
            for (int i = 0; i < 6; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
                Undo.RegisterCreatedObjectUndo(go, UndoName);
                go.name = "TrimBolt_" + (x < 0 ? "L_" : "R_") + i;
                go.transform.localPosition = Vector3.zero;
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var bounds = rs[0].bounds; foreach (var r in rs) bounds.Encapsulate(r.bounds);
                float extent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (extent > .00001f) go.transform.localScale *= .22f / extent;
                go.transform.localPosition = new Vector3(x, .64f, 1.2f + i * 3.95f);
                foreach (var r in rs)
                {
                    if (metal != null) r.sharedMaterials = Enumerable.Repeat(metal, r.sharedMaterials.Length).ToArray();
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                foreach (var c in go.GetComponentsInChildren<Collider>()) Undo.DestroyObjectImmediate(c);
            }
        }
    }

    private static void FillMissingReferences(Transform table)
    {
        var gameplay = table.Find("Gameplay");
        if (gameplay == null) return;
        var parts = table.Find("Table/Pinball_Table");
        foreach (var b in gameplay.GetComponentsInChildren<Bumper>(true))
        {
            var renderers = parts == null ? new List<Renderer>() : parts.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.StartsWith(b.name + "_", StringComparison.Ordinal)).ToList();
            var halo = table.Find(RootName + "/" + b.name + "_Halo");
            if (halo != null) renderers.Add(halo.GetComponent<Renderer>());
            FillArrayIfEmpty(b, "flashRenderers", renderers.ToArray());
        }
        foreach (var sling in gameplay.GetComponentsInChildren<Slingshot>(true))
            FillArrayIfEmpty(sling, "flashRenderers", sling.GetComponentsInChildren<Renderer>(true));
        var ball = gameplay.Find("Ball");
        var spawn = gameplay.Find("BallSpawnPoint");
        var bm = UnityEngine.Object.FindAnyObjectByType<BallManager>();
        if (ball != null) FillReference(bm, "sceneBall", ball.GetComponent<Rigidbody>());
        var multiball = UnityEngine.Object.FindAnyObjectByType<MultiballManager>();
        FillReference(multiball, "ballPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ball.prefab"));
        FillReference(multiball, "spawnPoint", spawn);
        var audio = UnityEngine.Object.FindAnyObjectByType<AudioManager>();
        var clips = new[] { "bumper", "slingshot", "target", "drain", "spinner" }
            .Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Audio/" + n + ".wav")).Where(c => c != null).ToArray();
        FillArrayIfEmpty(audio, "sfxClips", clips);
    }

    private static void FillReference(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        if (owner == null || value == null) return;
        var so = new SerializedObject(owner);
        var p = so.FindProperty(field);
        if (p == null || p.objectReferenceValue != null) return;
        p.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void FillArrayIfEmpty(UnityEngine.Object owner, string field, UnityEngine.Object[] values)
    {
        if (owner == null || values.Length == 0) return;
        var so = new SerializedObject(owner);
        var p = so.FindProperty(field);
        if (p == null) return;
        // A partially configured list belongs to the user; only wholly empty lists are repaired.
        for (int i = 0; i < p.arraySize; i++) if (p.GetArrayElementAtIndex(i).objectReferenceValue != null) return;
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedProperties();
    }

    private static void Label(Transform parent, string name, string text, float x, float z,
        float width, float height, float size, Color color, FontStyles style = FontStyles.Normal)
    {
        var go = NewObject(name, parent);
        var tmp = Undo.AddComponent<TextMeshPro>(go);
        tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.rectTransform.sizeDelta = new Vector2(width, height);
        tmp.transform.localPosition = new Vector3(x, .045f, z);
        tmp.transform.localRotation = Quaternion.Euler(90, 0, 0);
        tmp.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        tmp.raycastTarget = false;
    }

    private static GameObject NewObject(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, UndoName);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Arrow(Ink ink, float x, float z, float width, float height)
    {
        ink.Path(new[] { new Vector2(x - width / 2, z), new Vector2(x, z + height), new Vector2(x + width / 2, z) }, .038f);
    }

    private static void Pine(Ink ink, float x, float z, float width, float height)
    {
        ink.Polygon(new Vector2(x - width, z), new Vector2(x + width, z), new Vector2(x, z + height));
        ink.Quad(x - .035f, z - .15f, x + .035f, z);
    }

    private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, split));
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }

    private sealed class Ink
    {
        private readonly string name;
        private readonly Color color;
        private readonly float y;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        public Ink(string name, Color color, float y) { this.name = name; this.color = color; this.y = y; }
        public void Quad(float left, float bottom, float right, float top)
        { Polygon(new Vector2(left, bottom), new Vector2(right, bottom), new Vector2(right, top), new Vector2(left, top)); }
        public void Polygon(params Vector2[] points)
        {
            int offset = vertices.Count;
            foreach (var p in points) vertices.Add(new Vector3(p.x, y, p.y));
            for (int i = 1; i < points.Length - 1; i++)
            { triangles.Add(offset); triangles.Add(offset + i + 1); triangles.Add(offset + i); }
        }
        public void Path(IList<Vector2> points, float width)
        {
            for (int i = 1; i < points.Count; i++)
            {
                var a = points[i - 1]; var b = points[i]; var d = (b - a).normalized;
                var normal = new Vector2(-d.y, d.x) * width / 2;
                Polygon(a - normal, b - normal, b + normal, a + normal);
            }
        }
        public void Ring(float x, float z, float radius, float width)
        {
            var points = new List<Vector2>();
            for (int i = 0; i <= 96; i++) { float angle = i * Mathf.PI * 2 / 96; points.Add(new Vector2(x + Mathf.Cos(angle) * radius, z + Mathf.Sin(angle) * radius)); }
            Path(points, width);
        }
        public void Build(Transform parent)
        {
            string meshPath = Folder + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            string matPath = Folder + "/Ink_" + ColorUtility.ToHtmlStringRGB(color) + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Ink_" + ColorUtility.ToHtmlStringRGB(color) };
                mat.SetColor("_BaseColor", color); mat.SetFloat("_Cull", 0);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var go = NewObject(name, parent);
            Undo.AddComponent<MeshFilter>(go).sharedMesh = mesh;
            var r = Undo.AddComponent<MeshRenderer>(go);
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }
    }
}
