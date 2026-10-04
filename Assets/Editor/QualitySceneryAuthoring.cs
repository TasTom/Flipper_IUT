using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>GDD §Habillage et §Zone haute : vraie architecture du campus et patrimoine
/// industriel des Vosges. Static visual prefabs only; no gameplay colliders or physics.
/// Explicit Undoable replacement, then add-missing-only on subsequent runs. Never saves scenes.</summary>
public static class QualitySceneryAuthoring
{
    private const string Folder = "Assets/Art/Scenery/Quality";
    [Serializable] private sealed class ModelList { public Model[] models; }
    [Serializable] private sealed class Model { public string asset; public MaterialSpec[] materials; }
    [Serializable] private sealed class MaterialSpec { public string name; public float[] color; public float metallic; public float roughness; }
    [Serializable] private sealed class TextureList { public TextureSpec[] materials; }
    [Serializable] private sealed class TextureSpec { public string name; public string Diffuse; public string Normal; public string Mask; public bool doubleSided; }

    [MenuItem("Flipper/Décor/Préparer les assets qualitatifs")]
    public static void PrepareAssets()
    {
        var models = JsonUtility.FromJson<ModelList>(File.ReadAllText(Folder + "/ModelMaterials.json")).models;
        var textures = JsonUtility.FromJson<TextureList>(File.ReadAllText(Folder + "/TextureMaterials.json")).materials;
        EnsureFolder(Folder + "/Materials"); EnsureFolder(Folder + "/Prefabs");
        foreach (var model in models)
        {
            var modelPath = Folder + "/" + model.asset + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            // Source OBJ/glTF normals use different axes; calculate from final
            // Unity geometry rather than carrying stale custom split normals.
            if (importer.importNormals != ModelImporterNormals.Calculate || importer.importTangents != ModelImporterTangents.CalculateMikk)
            {
                importer.importNormals = ModelImporterNormals.Calculate;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.normalSmoothingSource = ModelImporterNormalSmoothingSource.PreferSmoothingGroups;
                importer.normalSmoothingAngle = 60;
                importer.SaveAndReimport();
            }
            var prefabPath = Folder + "/Prefabs/" + model.asset + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) continue;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null) throw new InvalidOperationException("Modèle non importé : " + model.asset);
            var instance = Object.Instantiate(source);
            try
            {
                instance.name = model.asset;
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                    {
                        var name = original.name;
                        var spec = model.materials.FirstOrDefault(m => m.name == name);
                        if (spec == null) throw new InvalidOperationException("Slot de matériau inconnu : " + model.asset + " / " + name);
                        return Material(spec, textures.FirstOrDefault(t => t.name == name));
                    }).ToArray();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                if (instance.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("Un décor importé ne doit pas avoir de collider.");
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            }
            finally { Object.DestroyImmediate(instance); }
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Flipper/Décor/Remplacer les maquettes simplifiées (Undo)")]
    public static void ApplyQualityReplacement()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || (scene.name != "Neutral" && scene.name != "Industries"))
            throw new InvalidOperationException("Neutral ou Industries en mode édition attendu.");
        bool industries = scene.name == "Industries";
        var host = industries ? GameObject.Find("ExampleTable/Playfield")?.transform : GameObject.Find("PinballTable")?.transform;
        if (host == null) throw new InvalidOperationException("Racine du plateau absente.");
        // Check every required asset before touching existing renderers.
        var required = industries ? new[] { "small_lpg_tank", "spinning_wheel_01", "hand_plane_no4", "drill_press_01", "bench_vice_01", "tree_stump_01" }
                                  : new[] { "IUT_Campus_Actual", "fir_sapling", "CampusDisplayMount" };
        foreach (var asset in required)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/" + asset + ".prefab") == null)
                throw new InvalidOperationException("Préparer les assets avant le remplacement : " + asset);
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Décors détaillés de flipper");
        int objectsBefore = host.GetComponentsInChildren<Transform>(true).Length;
        var root = host.Find("QualityScenery"); if (root == null) root = New("QualityScenery", host);
        bool first = root.Find("ReplacementApplied") == null;
        if (industries)
        {
            Place(root, "EnergyVessel", "small_lpg_tank", new Vector3(-2.79f, .495f, 15.25f), .75f, 15, true);
            Place(root, "TextileSpinningWheel", "spinning_wheel_01", new Vector3(3.08f, .495f, 9.47f), .69f, 90, true);
            Place(root, "TimberStump", "tree_stump_01", new Vector3(-3.25f, .495f, 9.45f), .64f, 0, true);
            Place(root, "TimberHandPlane", "hand_plane_no4", new Vector3(-3.25f, .735f, 9.45f), .38f, -15, true);
            Place(root, "WorkshopDrillPress", "drill_press_01", new Vector3(-.66f, .450f, 17.25f), .88f, 0, true);
            Place(root, "WorkshopVice", "bench_vice_01", new Vector3(.18f, .450f, 17.25f), .68f, 0, true);
            if (first)
            {
                Hide(host, "FactoryFacade", "EnergyBoiler", "TextileWorkshop", "TimberWorkshop");
                foreach (var name in new[] { "EnergyPlate", "TimberPlate", "TextilePlate" })
                {
                    var label = host.Find("MiniatureScenery/" + name); if (label == null) continue;
                    Undo.RecordObject(label, "Poser les légendes sur les plastiques"); var p = label.position; p.y = .505f; label.position = p;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                }
            }
        }
        else
        {
            Place(root, "CampusDisplayMount", "CampusDisplayMount", new Vector3(.90f, .72f, 15.42f), 2.10f, 0, false);
            Place(root, "IUTCampusActual", "IUT_Campus_Actual", new Vector3(.90f, .809f, 15.48f), 1.24f, 90, false);
            Place(root, "VosgesFirLeft", "fir_sapling", new Vector3(.06f, .809f, 15.58f), .43f, 20, false);
            Place(root, "VosgesFirRight", "fir_sapling", new Vector3(1.74f, .809f, 15.58f), .48f, 145, false);
            AddCampusSpot(root);
            if (first)
            {
                Hide(host, "IutCampus", "FirLeft", "FirRight", "CampusDeck");
                var label = host.Find("MiniatureScenery/CampusPlate");
                if (label != null)
                {
                    Undo.RecordObject(label, "Dégager la légende du campus"); label.localPosition = new Vector3(.90f, .810f, 14.925f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                }
            }
        }
        if (first) New("ReplacementApplied", root);
        if (first || objectsBefore != host.GetComponentsInChildren<Transform>(true).Length)
            EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
    }

    private static Material Material(MaterialSpec spec, TextureSpec texture)
    {
        var path = Folder + "/Materials/" + spec.name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = spec.name };
        material.SetColor("_BaseColor", texture == null ? new Color(spec.color[0], spec.color[1], spec.color[2], spec.color[3]) : Color.white);
        material.SetFloat("_Metallic", spec.metallic);
        material.SetFloat("_Smoothness", 1 - spec.roughness);
        // The supplied OBJ has architectural sheets with mixed face winding.
        // Both roof sides are required; keep its original geometry intact.
        if (spec.name.StartsWith("IUT_", StringComparison.Ordinal))
        { material.SetFloat("_Cull", 0); material.doubleSidedGI = true; }
        if (texture != null)
        {
            material.SetTexture("_BaseMap", Texture(texture.Diffuse, false, true));
            material.SetTexture("_BumpMap", Texture(texture.Normal, true, false)); material.EnableKeyword("_NORMALMAP");
            var mask = Texture(texture.Mask, false, false);
            if (mask != null)
            {
                material.SetTexture("_MetallicGlossMap", mask); material.SetTexture("_OcclusionMap", mask);
                material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
                material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
            }
            if (texture.doubleSided) material.SetFloat("_Cull", 0);
        }
        AssetDatabase.CreateAsset(material, path); return material;
    }
    private static void AddCampusSpot(Transform root)
    {
        if (root.Find("CampusAccent") != null) return;
        var t = New("CampusAccent", root); t.localPosition = new Vector3(.90f, 1.85f, 14.55f);
        t.localRotation = Quaternion.LookRotation(new Vector3(.90f, .96f, 15.48f) - t.localPosition, Vector3.up);
        var light = Undo.AddComponent<Light>(t.gameObject); light.type = LightType.Spot;
        light.color = new Color(1, .91f, .78f); light.intensity = 3; light.range = 2.8f;
        light.spotAngle = 65; light.innerSpotAngle = 40; light.shadows = LightShadows.None;
    }
    private static Texture2D Texture(string path, bool normal, bool srgb)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("Texture absente : " + path);
        var expected = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != expected || importer.sRGBTexture != srgb || importer.maxTextureSize != 1024)
        {
            importer.textureType = expected; importer.sRGBTexture = srgb; importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static void Place(Transform parent, string name, string asset, Vector3 position, float scale, float yaw, bool world)
    {
        if (parent.Find(name) != null) return;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/" + asset + ".prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        Undo.RegisterCreatedObjectUndo(instance, "Placer " + name); instance.name = name;
        var t = instance.transform; t.SetParent(parent, false);
        if (world)
        {
            t.position = position; t.rotation = Quaternion.Euler(0, yaw, 0) * source.transform.rotation;
            t.localScale = source.transform.localScale * (scale / parent.lossyScale.x);
        }
        else
        {
            t.localPosition = position; t.localRotation = Quaternion.Euler(0, yaw, 0) * source.transform.localRotation;
            t.localScale = source.transform.localScale * scale;
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
    }
    private static void Hide(Transform host, params string[] names)
    {
        var old = host.Find("MiniatureScenery"); if (old == null) return;
        foreach (var name in names)
        {
            var item = old.Find(name); if (item == null) continue;
            foreach (var renderer in item.GetComponentsInChildren<MeshRenderer>(true))
            { Undo.RecordObject(renderer, "Remplacer " + name); renderer.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); }
        }
    }
    private static Transform New(string name, Transform parent)
    {
        var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Créer " + name); go.transform.SetParent(parent, false); return go.transform;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
}
