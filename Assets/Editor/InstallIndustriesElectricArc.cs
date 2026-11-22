using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Unity;

public static class InstallIndustriesElectricArc
{
    private const string Name = "EnergySparkGap";
    private const string Workshop = "Assets/Generated/Industries/Remodel/Workshop";

    [MenuItem("Flipper/Industries/Installer l’arc électrique (Undo)")]
    public static void Install()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "Industries")
            throw new InvalidOperationException("Ouvrir Industries en mode édition.");
        var engine = UnityEngine.Object.FindAnyObjectByType<IndustriesVpeGame>();
        var plate = engine?.GetComponentInChildren<PlayfieldComponent>()?.transform;
        var regulator = plate?.Find("KineticWorkshop/EnergyRegulator");
        if (regulator == null || regulator.gameObject.scene != scene)
            throw new InvalidOperationException("Régulateur Énergie introuvable.");
        if (regulator.Find(Name) != null) return;

        var copper = AssetDatabase.LoadAssetAtPath<Material>(Workshop + "Copper.mat");
        var ceramic = AssetDatabase.LoadAssetAtPath<Material>(Workshop + "Ivory.mat");
        var panel = AssetDatabase.LoadAssetAtPath<Material>(Workshop + "Panel.mat");
        var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Industries/Lighting/InsertGlow.mat");
        if (copper == null || ceramic == null || panel == null || glow == null)
            throw new InvalidOperationException("Matériaux du régulateur manquants.");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Arc électrique d’Industries");
        var root = NewChild(Name, regulator, new Vector3(.024f, 0f, .003f));
        Piece("Platine", root, PrimitiveType.Cube, new Vector3(0, .0012f, 0),
            new Vector3(.012f, .0024f, .029f), panel);
        var first = Terminal(root, -.010f, copper, ceramic);
        var second = Terminal(root, .010f, copper, ceramic);
        var effect = Undo.AddComponent<IndustriesElectricArc>(root.gameObject);
        var fields = new SerializedObject(effect);
        fields.FindProperty("firstTerminal").objectReferenceValue = first;
        fields.FindProperty("secondTerminal").objectReferenceValue = second;
        fields.FindProperty("glowMaterial").objectReferenceValue = glow;
        fields.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
    }

    private static Transform Terminal(Transform parent, float z, Material copper, Material ceramic)
    {
        var post = NewChild(z < 0 ? "Borne aval" : "Borne amont", parent, new Vector3(0, 0, z));
        Piece("Embase", post, PrimitiveType.Cylinder, new Vector3(0, .0031f, 0), new Vector3(.0072f, .0009f, .0072f), copper);
        Piece("Isolant", post, PrimitiveType.Cylinder, new Vector3(0, .0054f, 0), new Vector3(.0053f, .0018f, .0053f), ceramic);
        Piece("Électrode", post, PrimitiveType.Sphere, new Vector3(0, .0085f, 0), new Vector3(.0046f, .0034f, .0046f), copper);
        return NewChild("Pointe", post, new Vector3(0, .0095f, z < 0 ? .0014f : -.0014f));
    }

    private static void Piece(string name, Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
    {
        var obj = GameObject.CreatePrimitive(shape);
        obj.name = name;
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<UnityEngine.Collider>());
        Undo.RegisterCreatedObjectUndo(obj, "Créer " + name);
        Undo.SetTransformParent(obj.transform, parent, "Poser " + name);
        obj.transform.SetLocalPositionAndRotation(position, Quaternion.identity);
        obj.transform.localScale = scale;
        var renderer = obj.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static Transform NewChild(string name, Transform parent, Vector3 position)
    {
        var child = new GameObject(name).transform;
        Undo.RegisterCreatedObjectUndo(child.gameObject, "Créer " + name);
        Undo.SetTransformParent(child, parent, "Poser " + name);
        child.SetLocalPositionAndRotation(position, Quaternion.identity);
        child.localScale = Vector3.one;
        return child;
    }
}
