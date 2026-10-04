using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Unity;
using Object = UnityEngine.Object;

/// <summary>
/// Habillage Industries : GDD §Univers et direction artistique, demande de seconde table industrielle.
/// Migration visuelle explicite et annulable. Aucun collider, hôte mécanique ou réglage de jeu modifié.
/// Une seconde exécution conserve les ajustements manuels ; la scène n'est jamais enregistrée.
/// </summary>
public static class RemodelIndustriesPresentation
{
    private const string Folder = "Assets/Generated/Industries/Remodel";
    private const string Art = "Assets/Art/Industries/Remodel/";
    private const string Model = "Assets/Models/Industries/WorkshopCarters.fbx";
    private static readonly string[] Carters = { "Wall30", "Wall31", "Wall33", "Wall34", "Wall36", "Wall38", "Wall39", "Wall78" };

    [MenuItem("Flipper/Industries/Remodeler l'habillage atelier (Undo)")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Industries")
        { Debug.LogWarning("[Industries] Ouvrir Industries hors Play pour remodeler son habillage."); return; }
        var table = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TableComponent>(true)).FirstOrDefault();
        var pf = table != null ? table.GetComponentInChildren<PlayfieldComponent>()?.transform : null;
        var presentation = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<IndustriesPresentation>(true)).FirstOrDefault();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        var print = pf != null ? pf.Find("PrintedPlayfield")?.GetComponent<MeshRenderer>() : null;
        if (pf == null || presentation == null || model == null || print == null || TMP_Settings.defaultFontAsset == null ||
            Carters.Any(name => pf.Find("Walls/" + name + "/Top") == null) ||
            new[] { "Playfield", "Panels", "PressureDial" }.Any(name => AssetDatabase.LoadAssetAtPath<Texture2D>(Art + name + ".png") == null))
        { Debug.LogWarning("[Industries] Plateau adapté, présentation, carters, textures ou police manquants ; remodelage annulé."); return; }
        if (pf.Find("WorkshopRemodel") != null)
        { Debug.Log("[Industries] Habillage atelier déjà présent ; ajustements manuels conservés."); return; }
        EnsureFolder(Folder);
        PrepareTexture("Playfield", 4096);
        PrepareTexture("Panels", 4096);
        PrepareTexture("PressureDial", 512);
        var panel = Material("WorkshopPanel", new Color(.9f, .9f, .9f), 0, .38f, "Panels");
        var copper = Material("WorkshopCopper", new Color(.65f, .33f, .135f), 1, .57f);
        var steel = Material("WorkshopSteel", new Color(.21f, .28f, .31f), 1, .43f);
        var enamel = Material("WorkshopEnamel", new Color(.023f, .135f, .16f), 0, .55f);
        var ivory = Material("WorkshopIvory", new Color(.84f, .8f, .67f), 0, .53f);
        var rubber = Material("WorkshopRubber", new Color(.035f, .05f, .054f), 0, .22f);
        var dial = Material("WorkshopDial", Color.white, 0, .4f, "PressureDial");
        var floor = Material("WorkshopPlayfield", Color.white, 0, .38f, "Playfield");

        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Remodeler Industries : atelier");
        var root = New("WorkshopRemodel", pf);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        Undo.RegisterCreatedObjectUndo(instance, "Carters et portique Industries");
        instance.name = "FittedCarters";
        instance.transform.SetParent(root, false);
        foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m.name.StartsWith("WorkshopCopper") ? copper :
                m.name.StartsWith("WorkshopSteel") ? steel : renderer.transform.parent.name == "FactoryGantry" ? enamel : panel).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        var hidden = Carters.SelectMany(name => pf.Find("Walls/" + name).GetComponentsInChildren<Renderer>(true)
            .Where(r => r.name == "Top" || r.name == "Side")).ToList();
        foreach (var name in new[] { "LeftOutlane", "RightOutlane" })
        {
            var renderer = pf.Find("Triggers/" + name)?.GetComponent<Renderer>();
            if (renderer != null) hidden.Add(renderer);
        }
        // Hôtes actifs : seule la présentation VPE de démonstration est masquée.
        var so = new SerializedObject(presentation); var array = so.FindProperty("hiddenRenderers");
        var previous = Enumerable.Range(0, array.arraySize).Select(i => array.GetArrayElementAtIndex(i).objectReferenceValue as Renderer);
        var combined = previous.Concat(hidden).Where(r => r != null).Distinct().ToArray();
        Undo.RecordObject(presentation, "Remplacer les seuls carters visibles");
        array.arraySize = combined.Length;
        for (int i = 0; i < combined.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = combined[i];
        so.ApplyModifiedProperties();
        foreach (var renderer in hidden) { Undo.RecordObject(renderer, "Masquer habillage VPE"); renderer.enabled = false; Record(renderer); }
        Assign(print, floor);
        // Derniers visuels du template : chants bois, plots violets et caoutchoucs rouges.
        foreach (var renderer in table.GetComponentsInChildren<MeshRenderer>(true))
        {
            var names = renderer.sharedMaterials.Where(m => m != null).Select(m => m.name.ToLowerInvariant()).ToArray();
            if (names.Any(n => n.Contains("sidewood"))) Assign(renderer, steel);
            else if (names.Any(n => n.Contains("purple_peg"))) Assign(renderer, copper);
            else if (names.Any(n => n == "plastictrigger")) Assign(renderer, ivory);
            else if (names.Any(n => n.Contains("red_rubber"))) Assign(renderer, rubber);
        }
        foreach (var name in new[] { "Wall350", "Wall3", "Wall4", "Wall5top", "Wall5bottom" })
        {
            var host = pf.Find("Walls/" + name);
            if (host == null) continue;
            foreach (var renderer in host.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Top" || r.name == "Side")) Assign(renderer, steel);
        }
        foreach (var ramp in table.GetComponentsInChildren<RampComponent>(true))
        {
            if (ramp.name.StartsWith("ApronCard")) continue;
            foreach (var renderer in ramp.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool trim = renderer.name == "Wires" || (ramp.name == "Ramp6" || ramp.name == "Ramp7");
                Assign(renderer, trim ? copper : steel);
            }
        }
        foreach (var flipper in table.GetComponentsInChildren<FlipperComponent>(true))
        {
            Assign(flipper.transform.Find("Base")?.GetComponent<Renderer>(), ivory);
            Assign(flipper.transform.Find("Rubber")?.GetComponent<Renderer>(), rubber);
        }
        foreach (var renderer in table.GetComponentsInChildren<RubberComponent>(true).SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)))
            Assign(renderer, rubber);
        var shooter = pf.Find("Primitives/Shooter")?.GetComponent<Renderer>();
        Assign(shooter, enamel);
        foreach (var renderer in table.GetComponentsInChildren<PlungerComponent>(true).SelectMany(p => p.GetComponentsInChildren<Renderer>(true))) Assign(renderer, steel);

        foreach (var bumper in table.GetComponentsInChildren<BumperComponent>(true))
        {
            var cap = bumper.transform.Find("Cap");
            var capFilter = cap != null ? cap.GetComponent<MeshFilter>() : null;
            if (capFilter == null || capFilter.sharedMesh == null) continue;
            Assign(cap.GetComponent<Renderer>(), enamel);
            Assign(bumper.transform.Find("Base")?.GetComponent<Renderer>(), steel);
            var bounds = capFilter.sharedMesh.bounds;
            float radius = Mathf.Min(bounds.size.x, bounds.size.z) * .39f;
            var center = new Vector3(bounds.center.x, bounds.max.y + .0002f, bounds.center.z);
            var face = New("PressureDial", cap);
            face.localPosition = center; face.localScale = Vector3.one * radius;
            var mf = Undo.AddComponent<MeshFilter>(face.gameObject); mf.sharedMesh = Disc();
            var mr = Undo.AddComponent<MeshRenderer>(face.gameObject); mr.sharedMaterial = dial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            // New cap ornaments are children of the native animated cap, never static duplicates.
            var rim = New("CopperBezel", cap);
            rim.localPosition = center + Vector3.up * .00005f; rim.localScale = Vector3.one * radius * 1.08f;
            var rmf = Undo.AddComponent<MeshFilter>(rim.gameObject); rmf.sharedMesh = Ring();
            var rmr = Undo.AddComponent<MeshRenderer>(rim.gameObject); rmr.sharedMaterial = copper;
        }
        Label(root, "GantryName", "ATELIER DES VOSGES", new Vector3(.258f, .145f, -.200f), new Vector2(.225f, .013f));
        Label(root, "ApronName", "INDUSTRIES", new Vector3(.235f, .0435f, -1.045f), new Vector2(.13f, .018f));
        Record(presentation);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Debug.Log("[Industries] Carters, portique, manomètres, palette et plateau remodelés. Ctrl+Z annule ; scène non enregistrée.");
    }

    private static Transform New(string name, Transform parent)
    {
        var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Habillage Industries");
        go.transform.SetParent(parent, false); return go.transform;
    }
    private static void Assign(Renderer renderer, Material material)
    {
        if (renderer == null) return;
        Undo.RecordObject(renderer, "Matériau propre à Industries");
        renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
        Record(renderer);
    }
    private static void Record(Object obj) => PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
    private static void PrepareTexture(string name, int max)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + name + ".png");
        if (importer.maxTextureSize == max && importer.wrapMode == TextureWrapMode.Clamp && importer.textureCompression == TextureImporterCompression.CompressedHQ) return;
        importer.maxTextureSize = max; importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }
    private static Material Material(string name, Color color, float metallic, float smoothness, string texture = null)
    {
        var path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
        if (texture != null) material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + texture + ".png"));
        if (texture == "Playfield")
        {
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Industries/IndustriesMicroNormal.png"));
            material.SetFloat("_BumpScale", .14f); material.EnableKeyword("_NORMALMAP");
        }
        AssetDatabase.CreateAsset(material, path); return material;
    }
    private static Mesh Disc() => DialMesh(false);
    private static Mesh Ring() => DialMesh(true);
    private static Mesh DialMesh(bool ring)
    {
        var path = Folder + (ring ? "/PressureRim.asset" : "/PressureFace.asset");
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        const int segments = 64;
        var vertices = new Vector3[segments * 2]; var uv = new Vector2[segments * 2]; var triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            vertices[i * 2] = direction; vertices[i * 2 + 1] = direction * (ring ? .925f : 0);
            uv[i * 2] = new Vector2(direction.x, direction.z) * .5f + Vector2.one * .5f;
            uv[i * 2 + 1] = new Vector2(vertices[i * 2 + 1].x, vertices[i * 2 + 1].z) * .5f + Vector2.one * .5f;
            int next = (i + 1) % segments, offset = i * 6;
            triangles[offset] = i * 2; triangles[offset + 1] = i * 2 + 1; triangles[offset + 2] = next * 2;
            triangles[offset + 3] = next * 2; triangles[offset + 4] = i * 2 + 1; triangles[offset + 5] = next * 2 + 1;
        }
        var mesh = new Mesh { name = ring ? "PressureRim" : "PressureFace", vertices = vertices, uv = uv, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static void Label(Transform root, string name, string text, Vector3 position, Vector2 size)
    {
        var host = New(name, root); host.localPosition = position; host.localRotation = Quaternion.Euler(90, 0, 0);
        var label = Undo.AddComponent<TextMeshPro>(host.gameObject); label.font = TMP_Settings.defaultFontAsset;
        label.text = text; label.color = new Color(.93f, .85f, .65f); label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.rectTransform.sizeDelta = size;
        label.fontSize = 1; label.ForceMeshUpdate();
        var bounds = label.textBounds.size;
        if (bounds.x > 0 && bounds.y > 0) label.fontSize *= Mathf.Min(size.x / bounds.x, size.y / bounds.y);
        label.ForceMeshUpdate();
    }
}
