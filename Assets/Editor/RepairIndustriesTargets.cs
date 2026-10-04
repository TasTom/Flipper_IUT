using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VisualPinball.Unity;
using Object = UnityEngine.Object;

/// <summary>
/// Correction explicite des six cibles d'Industries (GDD : cibles, état visuel).
/// SW1–3 servent de référence ; les réglages mécaniques VPE sont conservés.
/// La commande est annulable et n'enregistre jamais la scène.
/// </summary>
public static class RepairIndustriesTargets
{
    private const string Folder = "Assets/Generated/Industries";
    private const string CleanTexture = "Assets/Art/Industries/IndustriesPlayfieldWithoutTargets.png";
    private static readonly string[] TargetNames = { "sw1", "sw2", "sw3", "sw11", "sw12", "sw13" };
    private static readonly string[] LampNames = { "l1", "l2", "l3", "l11", "l12", "l13" };

    [MenuItem("Flipper/Industries/Corriger les six cibles et leurs numéros")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Industries")
        {
            Debug.LogWarning("[Industries] Ouvrir Industries hors Play pour corriger ses cibles.");
            return;
        }
        var table = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TableComponent>(true)).FirstOrDefault();
        var config = AssetDatabase.LoadAssetAtPath<IndustriesConfig>(Folder + "/IndustriesConfig.asset");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CleanTexture);
        var print = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).FirstOrDefault(r => r.name == "PrintedPlayfield");
        if (table == null || config == null || texture == null || print == null || TMP_Settings.defaultFontAsset == null)
        {
            Debug.LogWarning("[Industries] Table, config, plateau sans numéros ou police TMP manquant ; correction annulée.");
            return;
        }
        var playfield = table.GetComponentInChildren<PlayfieldComponent>();
        var allTargets = table.GetComponentsInChildren<TargetComponent>(true);
        var allLamps = table.GetComponentsInChildren<LightComponent>(true);
        var targets = TargetNames.Select(name => allTargets.FirstOrDefault(t => t.name == name)).ToArray();
        var lamps = LampNames.Select(name => allLamps.FirstOrDefault(l => l.name == name)).ToArray();
        if (playfield == null || targets.Any(t => t == null || t.GetComponent<MeshFilter>()?.sharedMesh == null ||
            t.GetComponent<MeshRenderer>()?.sharedMaterial == null) || lamps.Any(l => l == null) ||
            targets.Take(3).Any(t => !(t is DropTargetComponent)) || targets.Skip(3).Any(t => !(t is HitTargetComponent)) ||
            targets.Skip(3).Any(t => t.GetComponent<HitTargetColliderComponent>() == null) || print.sharedMaterial == null)
        {
            Debug.LogWarning("[Industries] Les six hôtes VPE, leurs meshes et voyants doivent exister ; correction annulée.");
            return;
        }
        if (config.targetNumberFrontGap <= config.targetIndicatorRadius || config.targetIndicatorRadius <= 0 || config.targetIndicatorLightRange <= 0)
        {
            Debug.LogWarning("[Industries] Le recul du numéro doit dépasser son rayon ; vérifier IndustriesConfig.");
            return;
        }
        var body = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/TargetCollisionBodyAligned.asset");
        if (body == null)
        {
            Debug.LogWarning("[Industries] Le collider de référence des cibles manque ; correction annulée.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Corriger les six cibles Industries");
        var pf = playfield.transform;
        // Axe des deux banques existantes, sans supposer que le couloir du lanceur est centré.
        float axis = targets.Average(t => pf.InverseTransformPoint(t.transform.position).x);
        var referenceMesh = targets[0].GetComponent<MeshFilter>().sharedMesh;
        var referenceMaterial = targets[0].GetComponent<MeshRenderer>().sharedMaterial;
        for (int i = 0; i < 3; i++)
        {
            var source = targets[i].transform;
            var destination = targets[i + 3].transform;
            var position = pf.InverseTransformPoint(source.position);
            position.x = 2 * axis - position.x;
            Undo.RecordObject(destination, "Banques de cibles en miroir");
            destination.position = pf.TransformPoint(position);
            var relativeRotation = Quaternion.Inverse(pf.rotation) * source.rotation;
            destination.rotation = pf.rotation * Quaternion.Euler(0, -relativeRotation.eulerAngles.y, 0);
            Record(destination);

            var filter = targets[i + 3].GetComponent<MeshFilter>();
            var renderer = targets[i + 3].GetComponent<MeshRenderer>();
            var collider = targets[i + 3].GetComponent<HitTargetColliderComponent>();
            Undo.RecordObjects(new Object[] { filter, renderer, collider }, "Gabarit commun des cibles");
            filter.sharedMesh = referenceMesh;
            renderer.sharedMaterial = referenceMaterial;
            collider.FrontColliderMesh = body;
            Record(filter); Record(renderer); Record(collider);
        }

        var insertMesh = GetDiscMesh();
        var ringMesh = GetRingMesh();
        var dark = GetMaterial("TargetIndicatorBackground", new Color(.025f, .065f, .072f), Color.clear);
        var glow = GetMaterial("TargetIndicatorRing", new Color(.68f, .4f, .13f), new Color(2f, 1.1f, .35f, 0));
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i].transform;
            var mesh = target.GetComponent<MeshFilter>().sharedMesh;
            var front = VisibleFront(mesh);
            var point = target.TransformPoint(new Vector3(front.x, 0, front.z - config.targetNumberFrontGap));
            point = pf.InverseTransformPoint(point);
            point.y = .0005f;
            var lamp = lamps[i].transform;
            Undo.RecordObject(lamp, "Voyant centré devant la cible");
            lamp.position = pf.TransformPoint(point);
            // La rotation de la cible n'oriente pas le texte : les six chiffres se lisent du bas de la table.
            lamp.rotation = pf.rotation;
            Record(lamp);
            // Les hôtes Light VPE portent déjà la conversion VPX→mètres. Ne pas la réinitialiser.
            float visualScale = Mathf.Abs(pf.lossyScale.x / lamp.lossyScale.x);
            var background = MeshChild(lamp, "TargetNumberBackground", insertMesh, dark);
            var ring = MeshChild(lamp, "TargetNumberRing", ringMesh, glow);
            SetTransform(background, Vector3.zero, Quaternion.identity, Vector3.one * config.targetIndicatorRadius * visualScale);
            SetTransform(ring, Vector3.up * .00005f * visualScale, Quaternion.identity, Vector3.one * config.targetIndicatorRadius * visualScale);
            var labelHost = Child(lamp, "TargetNumber", true);
            var label = labelHost.GetComponent<TextMeshPro>();
            if (label == null) label = Undo.AddComponent<TextMeshPro>(labelHost.gameObject);
            Undo.RecordObject(label, "Numéro de cible");
            label.font = TMP_Settings.defaultFontAsset;
            label.text = (i + 1).ToString();
            label.color = new Color(.91f, .81f, .63f);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = Vector2.one * config.targetIndicatorRadius * 2;
            SetTransform(labelHost, Vector3.up * .0001f * visualScale, Quaternion.Euler(90, 0, 0), Vector3.one * visualScale);
            label.fontSize = 1;
            label.ForceMeshUpdate();
            float height = label.textBounds.size.y;
            if (height > 0) label.fontSize *= config.targetIndicatorRadius * 1.2f / height;
            label.ForceMeshUpdate();
            Record(label);
            foreach (var light in lamps[i].GetComponentsInChildren<Light>(true))
            {
                Undo.RecordObject(light, "Portée de voyant adaptée à l'échelle");
                light.range = config.targetIndicatorLightRange * Mathf.Abs(pf.lossyScale.x);
                light.intensity = config.targetIndicatorLightIntensity;
                Record(light);
            }
        }
        var materialPath = Folder + "/PlayfieldWithTargetIndicators.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(print.sharedMaterial) { name = "PlayfieldWithTargetIndicators" };
            material.SetTexture("_BaseMap", texture);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        Undo.RecordObject(print, "Plateau sans anciens numéros imprimés");
        print.sharedMaterial = material;
        Record(print);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssetIfDirty(config);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Debug.Log("[Industries] Six cibles harmonisées, banques en miroir et numéros centrés. Ctrl+Z annule ; scène non enregistrée.");
    }

    private static Vector3 VisibleFront(Mesh mesh)
    {
        var visible = new Bounds();
        bool first = true;
        foreach (var vertex in mesh.vertices)
        {
            if (vertex.y < 0) continue;
            if (first) { visible = new Bounds(vertex, Vector3.zero); first = false; }
            else visible.Encapsulate(vertex);
        }
        return new Vector3(visible.center.x, 0, visible.min.z);
    }

    private static Transform Child(Transform parent, string name, bool rect = false)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;
        var go = rect ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Numéro de cible Industries");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Transform MeshChild(Transform parent, string name, Mesh mesh, Material material)
    {
        var child = Child(parent, name);
        var filter = child.GetComponent<MeshFilter>();
        if (filter == null) filter = Undo.AddComponent<MeshFilter>(child.gameObject);
        var renderer = child.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = Undo.AddComponent<MeshRenderer>(child.gameObject);
        Undo.RecordObjects(new Object[] { filter, renderer }, "Voyant de cible");
        filter.sharedMesh = mesh; renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Record(filter); Record(renderer);
        return child;
    }

    private static void SetTransform(Transform transform, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Undo.RecordObject(transform, "Présentation des numéros");
        transform.localPosition = position; transform.localRotation = rotation; transform.localScale = scale;
        Record(transform);
    }

    private static Material GetMaterial(string name, Color color, Color emission)
    {
        var path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", .35f);
        material.SetColor("_EmissionColor", emission);
        if (emission.maxColorComponent > 0) material.EnableKeyword("_EMISSION");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Mesh GetDiscMesh() => GetIndicatorMesh(false);
    private static Mesh GetRingMesh() => GetIndicatorMesh(true);
    private static Mesh GetIndicatorMesh(bool ring)
    {
        string name = ring ? "TargetIndicatorRing" : "TargetIndicatorDisc";
        string path = Folder + "/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        const int segments = 48;
        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            vertices[i * 2] = direction;
            vertices[i * 2 + 1] = direction * (ring ? .84f : 0);
            int next = (i + 1) % segments;
            int offset = i * 6;
            triangles[offset] = i * 2; triangles[offset + 1] = i * 2 + 1; triangles[offset + 2] = next * 2;
            triangles[offset + 3] = next * 2; triangles[offset + 4] = i * 2 + 1; triangles[offset + 5] = next * 2 + 1;
        }
        mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void Record(Object obj) => PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
}
