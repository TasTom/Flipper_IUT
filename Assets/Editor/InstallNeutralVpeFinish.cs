using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Scene-local PBR finish based on VPE's authoring guides. Undoable; never saves a scene.</summary>
public static class InstallNeutralVpeFinish
{
    private const string Folder = "Assets/Generated/NeutralVpeFinish";
    private const string Action = "Installer la finition PBR de Neutral";

    [MenuItem("Pinball/Neutral/Installer la finition PBR VPE")]
    public static void Install()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Neutral.unity") throw new InvalidOperationException("Neutral required.");
        var table = GameObject.Find("PinballTable");
        if (table == null) throw new InvalidOperationException("PinballTable missing.");
        var art = table.transform.Find("ReferencePlayfield/ProfessionalArt");
        if (art == null) throw new InvalidOperationException("ProfessionalArt missing.");
        // Installation is a one-time authoring operation. Later manual changes stay intact.
        if (art.Find("PbrFinish") != null) return;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated", "NeutralVpeFinish");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(Action);
        try
        {
            var normal = Texture("SurfaceMicroNormal", true, false);
            var brushNormal = Texture("SteelBrushNormal", true, true);
            var dielectricMask = Texture("DielectricSmoothness", false, false);
            var metalMask = Texture("SteelSmoothness", false, true);
            var replacements = new Dictionary<Material, Material>();
            foreach (var renderer in table.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null || original.shader.name != "Universal Render Pipeline/Lit") continue;
                    if (!replacements.TryGetValue(original, out var replacement))
                    {
                        replacement = MaterialFor(original, normal, brushNormal, dielectricMask, metalMask);
                        replacements.Add(original, replacement);
                    }
                    if (replacement == original) continue;
                    materials[i] = replacement;
                    changed = true;
                }
                if (!changed) continue;
                Undo.RecordObject(renderer, Action);
                renderer.sharedMaterials = materials;
            }
            FlattenPrintedGraphics(table.transform);
            BindBumperFlashes(table.transform);
            var marker = new GameObject("PbrFinish");
            Undo.RegisterCreatedObjectUndo(marker, Action);
            marker.transform.SetParent(art, false);
            var volume = Undo.AddComponent<Volume>(marker);
            volume.isGlobal = true;
            volume.priority = 20;
            volume.sharedProfile = Profile();
            var probe = art.Find("CabinetReflection");
            if (probe != null)
            {
                var reflection = probe.GetComponent<ReflectionProbe>();
                if (reflection != null)
                {
                    Undo.RecordObject(reflection, Action);
                    reflection.resolution = 512;
                    reflection.boxProjection = true;
                    reflection.renderDynamicObjects = true;
                    reflection.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                    reflection.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                    reflection.intensity = 0.70f;
                    reflection.shadowDistance = 30;
                    reflection.farClipPlane = 40;
                    reflection.nearClipPlane = .05f;
                    reflection.RenderProbe();
                }
            }
            var directional = UnityEngine.Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            if (directional != null)
            {
                Undo.RecordObject(directional, Action);
                directional.color = new Color(1f, .97f, .93f);
                directional.intensity = 1.15f;
                directional.shadowBias = .03f;
                directional.shadowNormalBias = .08f;
                var data = directional.GetComponent<UniversalAdditionalLightData>();
                if (data == null) data = Undo.AddComponent<UniversalAdditionalLightData>(directional.gameObject);
                Undo.RecordObject(data, Action);
                data.usePipelineSettings = false;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
        finally { Undo.CollapseUndoOperations(group); }
    }

    private static Material MaterialFor(Material source, Texture2D normal, Texture2D brushNormal, Texture2D dielectricMask, Texture2D metalMask)
    {
        string name = source.name;
        bool steel = name.Contains("Steel") || name.Contains("Zinc");
        bool transparent = name == "ClearRamp" || name == "ClearGuard" || name == "WireformClearLiner";
        bool playfield = name == "PlayfieldPrint";
        bool rubber = name == "RubberIvory" || name == "BumperRed_Mat";
        bool emissive = name == "AmberInsert" || name == "WarmIvory" || name == "ForestPlastic" || name == "TerminalScreen" || name == "TerminalGlyph";
        bool known = steel || transparent || playfield || rubber || name == "ForestEnamel" || name == "ForestPlastic" || name == "WarmIvory" || name == "FlipperOrange_Mat" || name == "EnamelShell" || name == "EnamelMidnight" || name == "IvoryPrint" || name == "AmberInsert" || name == "TerminalScreen" || name == "TerminalGlyph";
        if (!known) return source;
        string path = Folder + "/" + name.Replace("/", "_").Replace("\"", "") + "_PBR.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(source) { name = name + "_PBR" };
        if (playfield) material.shader = Shader.Find("Universal Render Pipeline/Complex Lit");
        material.SetFloat("_Metallic", steel ? 1f : 0f);
        material.SetFloat("_SpecularHighlights", 1f);
        material.SetFloat("_EnvironmentReflections", 1f);
        material.enableInstancing = true;
        if (transparent)
        {
            var colour = material.GetColor("_BaseColor");
            colour.a = name == "ClearRamp" ? .10f : .012f;
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_BlendModePreserveSpecular", 1f);
            material.SetFloat("_Smoothness", .82f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetShaderPassEnabled("ShadowCaster", false);
        }
        else
        {
            float gloss = steel ? (name.Contains("Brushed") ? .68f : .86f) : rubber ? .28f : playfield ? .74f : name == "IvoryPrint" ? .42f : .68f;
            material.SetFloat("_Smoothness", gloss);
            material.SetTexture("_MetallicGlossMap", steel ? metalMask : dielectricMask);
            if (playfield)
            {
                material.SetFloat("_ClearCoat", 1);
                material.SetFloat("_ClearCoatMask", .55f);
                material.SetFloat("_ClearCoatSmoothness", .88f);
                material.SetTexture("_DetailNormalMap", normal);
                material.SetFloat("_DetailNormalMapScale", .18f);
                material.SetTextureScale("_DetailAlbedoMap", new Vector2(18, 36));
            }
            else if (steel || rubber || name == "ForestPlastic")
            {
                material.SetTexture("_BumpMap", steel ? brushNormal : normal);
                material.SetFloat("_BumpScale", steel ? .20f : .12f);
            }
            if (name == "WarmIvory") material.SetColor("_BaseColor", new Color(.82f, .78f, .61f));
        }
        // Gameplay uses property blocks for transient light output, including from zero.
        // URP 17.6 validates the keyword against the emissive flags. A tiny rest
        // value keeps that validation stable for materials flashed through blocks.
        material.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.None;
        if (emissive && material.GetColor("_EmissionColor").maxColorComponent < .001f)
            material.SetColor("_EmissionColor", new Color(.001f, .001f, .001f));
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        if (playfield) material.EnableKeyword("_DETAIL_MULX2");
        AssetDatabase.CreateAsset(material, path);
        Undo.RegisterCreatedObjectUndo(material, Action);
        return material;
    }

    private static Texture2D Texture(string name, bool normal, bool metal)
    {
        string path = Folder + "/" + name + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        const int size = 256;
        var image = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float u = x / (float)size, v = y / (float)size;
            float grain = Mathf.Sin(u * 2 * Mathf.PI * 61 + Mathf.Sin(v * 2 * Mathf.PI * 3));
            float micro = Mathf.Sin(u * 2 * Mathf.PI * 23) * Mathf.Sin(v * 2 * Mathf.PI * 29);
            if (normal)
            {
                var direction = new Vector3((metal ? grain : micro) * .045f, metal ? micro * .006f : grain * .025f, 1).normalized;
                pixels[y * size + x] = new Color(direction.x * .5f + .5f, direction.y * .5f + .5f, direction.z * .5f + .5f, 1);
            }
            else pixels[y * size + x] = new Color(metal ? 1 : 0, 1, 0, .96f + (metal ? grain : micro) * .035f);
        }
        image.SetPixels32(pixels);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Undo.RecordObject(importer, Action);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 4;
        importer.maxTextureSize = size;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static VolumeProfile Profile()
    {
        string path = Folder + "/NeutralFinishVolume.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile != null) return profile;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "NeutralFinishVolume";
        AssetDatabase.CreateAsset(profile, path);
        Undo.RegisterCreatedObjectUndo(profile, Action);
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.value = TonemappingMode.ACES;
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = .12f;
        bloom.threshold.value = 1.2f;
        bloom.scatter.value = .35f;
        bloom.clamp.value = 5;
        bloom.highQualityFiltering.value = true;
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.value = 0;
        var colour = profile.Add<ColorAdjustments>(true);
        colour.contrast.value = 4;
        colour.saturation.value = -3;
        foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void FlattenPrintedGraphics(Transform table)
    {
        foreach (string side in new[] { "Left", "Right" })
        {
            var assembly = table.Find("Gameplay/Slingshot_" + side + "/MechanicalAssembly");
            if (assembly == null) continue;
            var plastic = assembly.Find("SlingPlastic_" + side);
            if (plastic == null) continue;
            var plasticFilter = plastic.GetComponent<MeshFilter>();
            if (plasticFilter == null || plasticFilter.sharedMesh == null) continue;
            var positions = plasticFilter.sharedMesh.vertices.Select(v => table.InverseTransformPoint(plastic.TransformPoint(v))).ToArray();
            float top = positions.Max(v => v.y) + .001f;
            float minX = positions.Min(v => v.x), maxX = positions.Max(v => v.x);
            float minZ = positions.Min(v => v.z), maxZ = positions.Max(v => v.z);
            foreach (var child in assembly.Cast<Transform>().ToArray())
            {
                if (!child.name.StartsWith("SlingGraphic_") && !child.name.StartsWith("SlingPrintBorder_")) continue;
                var filter = child.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                string path = Folder + "/" + child.name + "_Printed.asset";
                var printed = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (printed == null)
                {
                    var source = filter.sharedMesh;
                    var vertices = source.vertices;
                    var frame = vertices.Select(v => table.InverseTransformPoint(child.TransformPoint(v))).ToArray();
                    var indices = source.triangles;
                    var triangles = new List<int>();
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var a = frame[indices[i]]; var b = frame[indices[i + 1]]; var c = frame[indices[i + 2]];
                        if (Vector3.Cross(b - a, c - a).y <= 0.0000001f) continue;
                        triangles.Add(indices[i]); triangles.Add(indices[i + 1]); triangles.Add(indices[i + 2]);
                    }
                    if (triangles.Count == 0) throw new InvalidOperationException("No printable top surface: " + child.name);
                    var uv = new Vector2[vertices.Length];
                    var normals = new Vector3[vertices.Length];
                    var normalToMesh = (table.worldToLocalMatrix * child.localToWorldMatrix).transpose;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var point = frame[i]; point.y = top;
                        vertices[i] = child.InverseTransformPoint(table.TransformPoint(point));
                        uv[i] = new Vector2((point.x - minX) / (maxX - minX), (point.z - minZ) / (maxZ - minZ));
                        normals[i] = normalToMesh.MultiplyVector(Vector3.up).normalized;
                    }
                    printed = new Mesh { name = child.name + "_Printed", vertices = vertices, normals = normals, uv = uv, triangles = triangles.ToArray() };
                    printed.RecalculateBounds();
                    AssetDatabase.CreateAsset(printed, path);
                    Undo.RegisterCreatedObjectUndo(printed, Action);
                }
                Undo.RecordObject(filter, Action);
                filter.sharedMesh = printed;
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null) { Undo.RecordObject(renderer, Action); renderer.shadowCastingMode = ShadowCastingMode.Off; }
                Undo.SetTransformParent(child, plastic, Action);
            }
        }
    }

    private static void BindBumperFlashes(Transform table)
    {
        foreach (var bumper in table.GetComponentsInChildren<Bumper>())
        {
            var cap = bumper.GetComponentsInChildren<Renderer>().FirstOrDefault(r =>
                r.enabled && r.gameObject.activeInHierarchy && r.name.Contains("Bumper Cap"));
            if (cap == null) { Debug.LogWarning("[NeutralFinish] No visible bumper cap: " + bumper.name, bumper); continue; }
            Undo.RecordObject(bumper, Action);
            var serialized = new SerializedObject(bumper);
            var flashes = serialized.FindProperty("flashRenderers");
            flashes.arraySize = 1;
            flashes.GetArrayElementAtIndex(0).objectReferenceValue = cap;
            var capField = serialized.FindProperty("cap");
            if (capField.objectReferenceValue == null) capField.objectReferenceValue = cap.transform;
            serialized.ApplyModifiedProperties();
        }
    }
}
