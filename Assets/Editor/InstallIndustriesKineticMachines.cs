using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VisualPinball.Unity;
using Joint = IndustriesKineticMachine.Joint;
using Motion = IndustriesKineticMachine.Motion;

public static class InstallIndustriesKineticMachines
{
    private const string Models = "Assets/Models/Industries/Kinetics/";
    private const string Materials = "Assets/Generated/Industries/Remodel/Workshop";
    private const string Root = "KineticWorkshop";

    [MenuItem("Flipper/Industries/Installer les machines animées (Undo)")]
    public static void Install()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "Industries")
            throw new InvalidOperationException("Ouvrir la scène Industries en mode édition.");
        var table = UnityEngine.Object.FindAnyObjectByType<IndustriesVpeGame>();
        var playfield = table != null ? table.GetComponentInChildren<PlayfieldComponent>()?.transform : null;
        if (playfield == null || playfield.gameObject.scene != scene)
            throw new InvalidOperationException("Plateau Industries introuvable.");

        var palette = new Dictionary<string, Material>();
        foreach (string key in new[] { "Steel", "Copper", "Panel", "Ivory", "Enamel", "Rubber" })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + key + ".mat");
            if (material == null) throw new InvalidOperationException("Matériau d’atelier introuvable " + key);
            palette.Add(key, material);
        }

        var specs = new[]
        {
            Station("ApronFlywheel", new Vector3(.077f, .0271f, -1.034f),
                JointSpec("Drive", Motion.Turn, Vector3.up, .22f),
                JointSpec("Follower", Motion.Turn, Vector3.up, -.3143f, phase: .0357f)),
            Station("ApronPump", new Vector3(.385f, .0271f, -1.034f),
                JointSpec("Piston1", Motion.Slide, Vector3.right, .57f, .0032f),
                JointSpec("Piston2", Motion.Slide, Vector3.right, .57f, .0032f, .5f)),
            Station("TextileSpindle", new Vector3(.457f, .0312f, -.680f),
                JointSpec("Spool1", Motion.Turn, Vector3.right, .48f),
                JointSpec("Spool2", Motion.Turn, Vector3.right, -.35f, phase: .25f)),
            Station("EnergyRegulator", new Vector3(.099f, .0312f, -.322f),
                JointSpec("Needle", Motion.Rock, Vector3.up, .33f, 41f),
                JointSpec("Relief", Motion.Slide, Vector3.up, .66f, .0017f, .25f)),
            Station("RampConveyor", new Vector3(.437f, .0312f, -.315f), ConveyorJoints())
        };
        var root = playfield.Find(Root);
        var pending = new List<(GameObject model, string name, Vector3 position, (string name, Joint joint)[] joints)>();
        foreach (var spec in specs)
        {
            if (root != null && root.Find(spec.name) != null) continue;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + spec.name + ".fbx");
            if (model == null) throw new InvalidOperationException("Modèle d’atelier introuvable " + spec.name);
            foreach (var joint in spec.joints)
                if (FindPivot(model.transform, joint.name) == null)
                    throw new InvalidOperationException("Pivot introuvable " + spec.name + "/" + joint.name);
            pending.Add((model, spec.name, spec.position, spec.joints));
        }
        if (pending.Count == 0) return;

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Machines animées d’Industries");
        if (root == null)
        {
            root = new GameObject(Root).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Créer les machines d’atelier");
            Undo.SetTransformParent(root, playfield, "Poser les machines sur le plateau");
            root.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.localScale = Vector3.one;
        }
        foreach (var spec in pending)
        {
            var machine = (GameObject)PrefabUtility.InstantiatePrefab(spec.model, root);
            Undo.RegisterCreatedObjectUndo(machine, "Installer " + spec.name);
            machine.name = spec.name;
            machine.transform.SetLocalPositionAndRotation(spec.position, Quaternion.identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(machine.transform);
            foreach (var renderer in machine.GetComponentsInChildren<MeshRenderer>())
            {
                string key = renderer.name.Substring(renderer.name.LastIndexOf('_') + 1);
                if (!palette.TryGetValue(key, out var material)) continue;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            var animation = Undo.AddComponent<IndustriesKineticMachine>(machine);
            var joints = new Joint[spec.joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                joints[i] = spec.joints[i].joint;
                joints[i].pivot = FindPivot(machine.transform, spec.joints[i].name);
            }
            animation.Configure(joints);
            EditorUtility.SetDirty(animation);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undo);
    }

    private static (string name, Vector3 position, (string name, Joint joint)[] joints) Station(
        string name, Vector3 position, params (string name, Joint joint)[] joints) => (name, position, joints);

    private static (string name, Joint joint) JointSpec(string name, Motion motion, Vector3 axis,
        float speed, float travel = 0f, float phase = 0f) => (name, new Joint
        { motion = motion, axis = axis, cyclesPerSecond = speed, travel = travel, phase = phase });

    private static (string name, Joint joint)[] ConveyorJoints()
    {
        var joints = new (string name, Joint joint)[8];
        for (int i = 0; i < 7; i++)
            joints[i] = JointSpec("Roller" + i, Motion.Turn, Vector3.right, .36f, phase: i * .12f);
        joints[7] = JointSpec("Shuttle", Motion.Slide, Vector3.forward, .27f, .015f);
        return joints;
    }

    private static Transform FindPivot(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }
}
