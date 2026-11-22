using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Unity;

public static class InstallIndustriesFurnace
{
    private const string Name = "FoundryFurnace";
    private const string Art = "Assets/Art/Industries/";
    private const string Workshop = "Assets/Generated/Industries/Remodel/Workshop";

    [MenuItem("Flipper/Industries/Installer le foyer (Undo)")]
    public static void Install()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "Industries")
            throw new InvalidOperationException("Ouvrir Industries en mode édition.");
        var game = UnityEngine.Object.FindAnyObjectByType<IndustriesVpeGame>();
        var playfield = game?.GetComponentInChildren<PlayfieldComponent>();
        var workshop = playfield?.transform.Find("KineticWorkshop");
        if (workshop == null || workshop.gameObject.scene != scene)
            throw new InvalidOperationException("L’atelier Industries est introuvable.");
        if (workshop.Find(Name) != null) return;

        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Industries/Kinetics/FoundryFurnace.fbx");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Art + "FoundryFlame.shader");
        if (model == null || shader == null)
            throw new InvalidOperationException("Modèle ou shader du foyer manquant.");

        var palette = new Dictionary<string, Material>();
        foreach (string key in new[] { "Panel", "Copper", "Steel", "Rubber" })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Workshop + key + ".mat");
            if (material == null) throw new InvalidOperationException("Matériau du foyer manquant " + key);
            palette.Add(key, material);
        }
        string materialPath = Art + "FoundryFlame.mat";
        var flame = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (flame == null)
        {
            flame = new Material(shader) { name = "FoundryFlame" };
            AssetDatabase.CreateAsset(flame, materialPath);
        }

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Foyer animé d’Industries");
        var furnace = new GameObject(Name).transform;
        Undo.RegisterCreatedObjectUndo(furnace.gameObject, "Installer le foyer");
        Undo.SetTransformParent(furnace, workshop, "Poser le foyer sur le carter");
        furnace.SetLocalPositionAndRotation(new Vector3(.431f, .0312f, -.236f), Quaternion.identity);
        furnace.localScale = Vector3.one;
        var housing = (GameObject)PrefabUtility.InstantiatePrefab(model, furnace);
        Undo.RegisterCreatedObjectUndo(housing, "Poser le carter du foyer");
        housing.transform.SetLocalPositionAndRotation(Vector3.zero, model.transform.localRotation);
        housing.transform.localScale = model.transform.localScale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(housing.transform);
        foreach (var renderer in housing.GetComponentsInChildren<MeshRenderer>())
        {
            var key = renderer.name.Substring(renderer.name.LastIndexOf('_') + 1);
            if (!palette.TryGetValue(key, out var material)) continue;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
        card.name = "Flammes et étincelles";
        UnityEngine.Object.DestroyImmediate(card.GetComponent<UnityEngine.Collider>());
        Undo.RegisterCreatedObjectUndo(card, "Créer les flammes");
        Undo.SetTransformParent(card.transform, furnace, "Ancrer les flammes dans le foyer");
        card.transform.SetLocalPositionAndRotation(new Vector3(0f, .0179f, -.0011f), Quaternion.Euler(53f, 0f, 0f));
        card.transform.localScale = new Vector3(.0285f, .034f, 1f);
        var flameRenderer = card.GetComponent<MeshRenderer>();
        flameRenderer.sharedMaterial = flame;
        flameRenderer.shadowCastingMode = ShadowCastingMode.Off;
        flameRenderer.receiveShadows = false;
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undo);
    }
}
