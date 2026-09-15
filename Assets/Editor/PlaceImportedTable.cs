using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Pose la table importée (<c>Assets/Models/Table/</c>) dans la scène active.
///
/// <para>Suit le contrat des trois autres menus : <b>ne crée que ce qui manque et ne touche
/// jamais aux réglages existants</b>. Un objet déjà en place — composants, colliders et valeurs
/// sérialisées compris — est laissé intact. Chaque création passe par
/// <see cref="Undo.RegisterCreatedObjectUndo"/> : Ctrl+Z annule tout. Le menu n'enregistre
/// jamais la scène.</para>
///
/// <para><b>Le caisson n'a aucun collider.</b> C'est du décor : un collider oublié dessus
/// casserait la physique de la table. Seuls les maillages de <c>Pinball_Table</c> en reçoivent
/// un. Les deux fichiers sont séparés à l'export précisément pour que cette règle s'applique
/// telle quelle.</para>
/// </summary>
public static class PlaceImportedTable
{
    const string TablePath = "Assets/Models/Table/Pinball_Table.fbx";
    const string CabinetPath = "Assets/Models/Table/Pinball_Cabinet.fbx";

    const string RootName = "PinballTable";
    const string GroupName = "Table";
    const string GameplayName = "Gameplay";

    /// <summary>Inclinaison réelle de la table, portée par le root (voir <c>TableGravity.cs</c>).</summary>
    const float TiltDegrees = -7f;

    /// <summary>
    /// Positions des hôtes de gameplay, en unités Unity, dans le repère de la table <b>à plat</b>
    /// (avant inclinaison). Elles sortent de <c>Tools/blender/build_table.py</c>, section
    /// « Hôtes de gameplay » : <c>x = 0</c> au centre de l'aire de jeu, <c>z = 0</c> au bord bas
    /// (drain), <c>z = 17.78</c> au mur du fond, <c>y = 0</c> à la surface du plateau.
    /// </summary>
    static readonly (string Name, Vector3 Local)[] Hosts =
    {
        ("Flipper_Left_Pivot", new Vector3(-1.428f, 0.000f, 1.533f)),
        ("Flipper_Right_Pivot", new Vector3(1.428f, 0.000f, 1.533f)),
        ("Slingshot_Left", new Vector3(-0.733f, 0.000f, 3.583f)),
        ("Slingshot_Right", new Vector3(0.733f, 0.000f, 3.583f)),
        ("Bumper_01", new Vector3(-1.417f, 0.000f, 11.000f)),
        ("Bumper_02", new Vector3(1.417f, 0.000f, 11.000f)),
        ("Bumper_03", new Vector3(0.000f, 0.000f, 12.583f)),
        ("Plunger", new Vector3(4.670f, 0.392f, 0.167f)),
        ("BallSpawnPoint", new Vector3(4.670f, 0.392f, 1.000f)),
        ("DrainZone", new Vector3(0.000f, -0.333f, 0.000f)),
    };

    [MenuItem("Flipper/Placer la table importée")]
    public static void Place()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[PlaceImportedTable] Indisponible en mode Play : " +
                           "Undo et MarkSceneDirty y sont interdits.");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        var root = FindRoot(scene, RootName);

        if (root == null)
        {
            Debug.LogError("[PlaceImportedTable] Aucun objet '" + RootName + "' dans la scène '" +
                           scene.name + "'. Lancer d'abord « Flipper > Assurer la structure MVP ».");
            return;
        }

        var group = EnsureGroup(root);
        var created = new List<string>();
        var kept = new List<string>();

        var table = EnsureInstance(TablePath, group, created, kept);
        var cabinet = EnsureInstance(CabinetPath, group, created, kept);

        int colliders = table != null ? EnsureTableColliders(table) : 0;
        int strayColliders = cabinet != null ? CountColliders(cabinet) : 0;
        int moved = PlaceHosts(root, scene);
        bool tilted = ApplyTilt(root);

        EditorSceneManager.MarkSceneDirty(scene);

        var report = new System.Text.StringBuilder();
        report.Append("[PlaceImportedTable] scène '").Append(scene.name).Append("' — ");

        report.Append(created.Count > 0
            ? "créés : " + string.Join(", ", created) + ". "
            : "rien à créer. ");

        if (kept.Count > 0)
        {
            report.Append("conservés tels quels : " + string.Join(", ", kept) + ". ");
        }

        report.Append(colliders).Append(" colliders de table posés. ");
        report.Append(moved).Append(" hôtes repositionnés. ");

        if (tilted)
        {
            report.Append("Root incliné à ").Append(TiltDegrees).Append("°. ");
        }

        if (strayColliders > 0)
        {
            report.Append("⚠ ").Append(strayColliders)
                  .Append(" collider(s) sur le caisson : c'est du décor, ils doivent disparaître. ");
        }

        report.Append("Scène modifiée, non enregistrée — Ctrl+S pour la garder.");

        Debug.Log(report.ToString());
    }

    static GameObject FindRoot(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var candidate in scene.GetRootGameObjects())
        {
            if (candidate.name == name)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Le dossier qui reçoit la table. Créé s'il manque, laissé tel quel sinon.</summary>
    static Transform EnsureGroup(GameObject root)
    {
        var existing = root.transform.Find(GroupName);

        if (existing != null)
        {
            return existing;
        }

        var group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Placer la table importée");
        group.transform.SetParent(root.transform, false);
        group.transform.localPosition = Vector3.zero;
        group.transform.localRotation = Quaternion.identity;
        group.transform.localScale = Vector3.one;

        return group.transform;
    }

    /// <summary>
    /// Instancie le modèle s'il n'est pas déjà là. Un modèle déjà posé est <b>conservé intact</b> :
    /// c'est la règle du contrat, et elle protège les réglages faits à la main.
    /// </summary>
    static GameObject EnsureInstance(string path, Transform parent,
                                     List<string> created, List<string> kept)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(path);
        var existing = FindDescendant(parent, name);

        if (existing != null)
        {
            kept.Add(name);
            return existing.gameObject;
        }

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (asset == null)
        {
            Debug.LogError("[PlaceImportedTable] Modèle introuvable : " + path +
                           ". Lancer Tools/blender/build_table.py pour le produire.");
            return null;
        }

        var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;

        if (instance == null)
        {
            Debug.LogError("[PlaceImportedTable] Instanciation impossible : " + path);
            return null;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Placer la table importée");
        instance.transform.SetParent(parent, false);

        // Ni position, ni rotation, ni échelle ne sont touchées : l'échelle de racine à ≈ 1666
        // posée par l'importeur (fileScale 0,01 : cm -> m) est la signature normale d'un FBX
        // Blender. Un « Reset » sur ce transform rendrait la table 1666 fois trop petite.
        created.Add(name);

        return instance;
    }

    /// <summary>
    /// Un <see cref="MeshCollider"/> par maillage de la table. Non convexe : la table est
    /// statique, et c'est la forme réelle des murs et du plateau qui doit arrêter la bille.
    /// </summary>
    static int EnsureTableColliders(GameObject table)
    {
        int added = 0;

        foreach (var filter in table.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.GetComponent<MeshCollider>() != null)
            {
                continue;
            }

            Undo.AddComponent<MeshCollider>(filter.gameObject).sharedMesh = filter.sharedMesh;
            added++;
        }

        return added;
    }

    static int CountColliders(GameObject target)
    {
        return target.GetComponentsInChildren<Collider>(true).Length;
    }

    /// <summary>
    /// Repositionne les hôtes sous <c>Gameplay</c>. Un hôte absent est signalé, jamais créé :
    /// c'est le rôle du menu de structure, et un hôte sans son script ne servirait à rien.
    ///
    /// <para>Un hôte trouvé <b>hors</b> de la table est rattaché à <c>Gameplay</c> : sinon il ne
    /// suivrait pas l'inclinaison de 7° portée par le root, et la bille apparaîtrait à côté du
    /// couloir de lancement.</para>
    /// </summary>
    static int PlaceHosts(GameObject root, UnityEngine.SceneManagement.Scene scene)
    {
        var gameplay = FindDescendant(root.transform, GameplayName);

        if (gameplay == null)
        {
            Debug.LogWarning("[PlaceImportedTable] Aucun '" + GameplayName + "' sous '" + RootName +
                             "' : les hôtes ne sont pas positionnés.");
            return 0;
        }

        int moved = 0;
        var missing = new List<string>();
        var attached = new List<string>();

        foreach (var host in Hosts)
        {
            var target = FindDescendant(gameplay, host.Name);

            if (target == null)
            {
                target = FindInScene(scene, host.Name);

                if (target == null)
                {
                    missing.Add(host.Name);
                    continue;
                }

                Undo.SetTransformParent(target, gameplay, "Placer la table importée");
                attached.Add(host.Name);
            }

            Undo.RecordObject(target, "Placer la table importée");
            target.localPosition = host.Local;
            moved++;
        }

        if (attached.Count > 0)
        {
            Debug.Log("[PlaceImportedTable] Hôtes rattachés à '" + GameplayName + "' (ils étaient " +
                      "hors de la table et n'auraient pas suivi l'inclinaison) : " +
                      string.Join(", ", attached) + ".");
        }

        if (missing.Count > 0)
        {
            Debug.LogWarning("[PlaceImportedTable] Hôtes absents, non positionnés : " +
                             string.Join(", ", missing) + ".");
        }

        return moved;
    }

    static Transform FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var candidate in scene.GetRootGameObjects())
        {
            var found = FindDescendant(candidate.transform, name);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Pose l'inclinaison sur le root — mais seulement s'il est encore à l'identité. Une rotation
    /// déjà réglée est un choix : elle est respectée et signalée.
    /// </summary>
    static bool ApplyTilt(GameObject root)
    {
        if (root.transform.localRotation != Quaternion.identity)
        {
            Debug.Log("[PlaceImportedTable] Root déjà orienté (" +
                      root.transform.localEulerAngles + ") : inclinaison laissée telle quelle.");
            return false;
        }

        Undo.RecordObject(root.transform, "Placer la table importée");
        root.transform.localRotation = Quaternion.Euler(TiltDegrees, 0f, 0f);

        return true;
    }

    static Transform FindDescendant(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            var found = FindDescendant(child, name);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
