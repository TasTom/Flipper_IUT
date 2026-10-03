using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity;
using VisualPinball.Unity.Editor;
// Les classes d'élément portent le nom de leur dossier, qui est aussi un espace de noms :
// `using VisualPinball.Engine.VPT.Flipper` masquerait la classe `Flipper` par le namespace.
// Les alias lèvent l'ambiguïté une fois pour toutes.
using BumperItem = VisualPinball.Engine.VPT.Bumper.Bumper;
using FlipperItem = VisualPinball.Engine.VPT.Flipper.Flipper;
using PlungerItem = VisualPinball.Engine.VPT.Plunger.Plunger;
using Vertex2D = VisualPinball.Engine.Math.Vertex2D;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;
using Object = UnityEngine.Object;

/// <summary>
/// Construit la seconde table — « Atelier des Vosges » — sur VPE, thème industries, montagne, IUT.
///
/// <para>La table est une vraie table VPE : elle porte un <see cref="TableComponent"/>, un
/// plateau, le moteur physique de VPE, un <c>Player</c> et un moteur de règles
/// (<see cref="DefaultGamelogicEngine"/>). Ses éléments — flippers, bumpers, slingshots, cibles —
/// sont les prefabs de VPE, donc leur physique, leurs bobines et leurs contacts sont ceux du
/// moteur, pas une réécriture.</para>
///
/// <para>La construction passe par les API publiques de VPE pour la table elle-même, et par
/// <c>InstantiateAndPersistPrefab</c> pour les éléments. Cette dernière est interne au package :
/// c'est celle qu'utilise le Toolbox de VPE quand on clique sur « Flipper » ou « Bumper », et
/// elle fait un travail qu'on ne saurait pas reproduire à moitié — instancier le prefab, lui
/// appliquer ses données, le ranger dans son groupe. Elle est appelée par réflexion, avec un
/// message clair si le package change.</para>
///
/// <para>Le menu ne touche pas à la scène ouverte : il en ouvre une nouvelle, la construit et
/// l'enregistre. Une scène modifiée fait échouer la commande plutôt que d'être perdue.</para>
/// </summary>
public static class BuildIndustriesTable
{
    private const string ScenePath = "Assets/Scenes/Industries.unity";
    private const string TableName = "Atelier des Vosges";
    private const string ThemeFolder = "Assets/VpeUrp/Art/Theme";

    /// <summary>
    /// La « Full Example Table » de Visual Pinball, celle que les docs VPE importent.
    ///
    /// <para>Sert de base à la table : elle apporte ce qu'on ne sait pas inventer — les 50 murs du
    /// plateau, le couloir de lanceur, le drain, les 18 rampes, les 36 lumières et le câblage des
    /// contacts et des bobines. Un <c>new FileTableContainer()</c> produit une table vide, où les
    /// flippers flottent sans rien à toucher.</para>
    ///
    /// <para>Le fichier vient du build VPinballX de Visual Pinball : <c>assets/exampleTable.vpx</c>
    /// dans l'archive de release. Il est déposé sous <c>Tools/</c>, qui n'est pas suivi par Git —
    /// le fichier pèse 19 Mo et se retélécharge en une commande.</para>
    /// </summary>
    private const string BaseTableVpx = "Tools/VisualPinball/assets/exampleTable.vpx";

    /// <summary>
    /// Échelle de VPE : la table fait 952 x 2162 unités pour 0,51 x 1,17 m, soit environ
    /// 1867 unités par mètre. Sert à convertir les cotes réelles des décors.
    /// </summary>
    private const float VpxUnitsPerMeter = 1867f;

    // Repère VPX : x va de 0 (bord gauche) à 952 (bord droit), y de 0 (fond de table, côté
    // orbit) à 2162 (côté joueur, là où sont les flippers). Le centre de l'aire de jeu est donc
    // x = 476.
    private const float CenterX = 476f;

    [MenuItem("Flipper/Construire la seconde table (VPE Industries)", false, 60)]
    public static void Build()
    {
        if (!ConfirmSceneCanBeReplaced())
        {
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Industries";

        ConfigureRenderSettings();

        var table = CreateTable();
        if (table == null)
        {
            return;
        }

        AddLighting(table);
        BuildTheme(table);
        AddCamera();

        AssetDatabase.SaveAssets();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);

        Debug.Log($"[BuildIndustriesTable] « {TableName} » construite et enregistrée dans {ScenePath}.");
    }

    /// <summary>
    /// Refuse de remplacer une scène sur le point d'être perdue.
    ///
    /// <para>Construire la table ouvre une nouvelle scène, ce qui jette celle qui est ouverte. Le
    /// travail de scène appartient à l'utilisateur : le détruire en silence serait le pire défaut
    /// possible de cet outil.</para>
    /// </summary>
    private static bool ConfirmSceneCanBeReplaced()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (!active.isDirty)
        {
            return true;
        }

        var keep = EditorUtility.DisplayDialog(
            "Scène modifiée",
            $"La scène « {active.name} » a des modifications non enregistrées.\n\n" +
            "Construire la seconde table en ouvre une nouvelle et perdrait ces modifications.\n\n" +
            "Enregistre la scène, puis relance la commande.",
            "Annuler la construction", "Ouvrir quand même");

        // Le bouton « Ouvrir quand même » laisse passer, mais il faut le choisir explicitement :
        // c'est le seul cas où l'utilisateur accepte de perdre son travail.
        return !keep;
    }

    /// <summary>
    /// Crée la table en important la Full Example Table de Visual Pinball.
    ///
    /// <para>C'est le parcours des docs VPE — <c>Pinball &gt; Import</c> — et c'est la seule façon
    /// d'obtenir une table réellement jouable : le plateau, ses murs, son couloir de lanceur, son
    /// drain et le câblage de ses contacts viennent du fichier, pas d'une reconstruction.</para>
    /// </summary>
    private static TableComponent CreateTable()
    {
        if (!System.IO.File.Exists(BaseTableVpx))
        {
            Debug.LogError(
                $"[BuildIndustriesTable] La table de base est absente : « {BaseTableVpx} ».\n" +
                "Elle vient du build VPinballX de Visual Pinball (assets/exampleTable.vpx) :\n" +
                "  https://github.com/vpinball/vpinball/releases → Developer.VPinballX-*-Release-win-x64.zip\n" +
                "Sans elle, la table ne peut pas être construite : un plateau vide n'est pas jouable.");
            return null;
        }

        GameObject root;
        try
        {
            root = VpxImportEngine.ImportIntoScene(BaseTableVpx, tableName: TableName);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[BuildIndustriesTable] L'import de la table de base a échoué : " + e.Message + "\n" +
                "Vérifie que l'adaptateur URP est actif — c'est lui qui fournit les prefabs des " +
                "éléments et les matériaux.");
            return null;
        }

        var table = root != null ? root.GetComponent<TableComponent>() : null;
        if (table == null)
        {
            Debug.LogError("[BuildIndustriesTable] L'import n'a produit aucune TableComponent.");
            return null;
        }

        // L'import pose la table à l'échelle du moteur ; on la recentre à l'origine pour que les
        // décors du thème, qui sont en mètres, tombent au bon endroit.
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;

        return table;
    }

    /// <summary>
    /// Pose les éléments de jeu si la scène partait d'une table vide.
    ///
    /// <para>Conservé pour le cas où la table de base serait un <c>blankTable.vpx</c> sans
    /// éléments. Avec la Full Example Table, elle n'est jamais appelée : les 2 flippers,
    /// 5 bumpers et le lanceur viennent déjà de l'import, et les redoubler créerait deux jeux
    /// d'éléments superposés.</para>
    /// </summary>
    private static void AddGameplayElements(TableComponent table)
    {
        // --- Flippers ---------------------------------------------------------------------
        // Le pivot est au bord intérieur de l'aire de jeu ; l'angle de départ les fait pointer
        // vers le bas et l'angle de fin les fait remonter.
        AddFlipper(table, "Flipper.Left", 300f, 1985f, startAngle: 121f, endAngle: 70f);
        AddFlipper(table, "Flipper.Right", 652f, 1985f, startAngle: 59f, endAngle: 110f);

        // --- Bumpers ----------------------------------------------------------------------
        // Triangle : deux en haut, un en bas, décalé pour renvoyer la bille vers les côtés.
        AddBumper(table, "Bumper.1", 366f, 1030f);
        AddBumper(table, "Bumper.2", 586f, 1030f);
        AddBumper(table, "Bumper.3", 476f, 1236f);

        // --- Lanceur ---------------------------------------------------------------------
        // Le couloir de lancement est à droite, hors de l'aire de jeu, comme sur la table du
        // projet (chenal à droite, convention du dépôt).
        AddPlunger(table, "Plunger", 884f, 1900f);

        // Les slingshots et les murs sont des surfaces à points de contrôle : leur tracé se fait
        // dans l'éditeur de points de VPE, pas par une position. Ils restent donc à poser à la
        // main — voir la note en fin de commande.
    }

    private static void AddFlipper(TableComponent table, string name, float x, float y,
                                   float startAngle, float endAngle)
    {
        var flipper = FlipperItem.GetDefault(table.TableContainer.Table);
        flipper.Name = name;
        flipper.Data.Center = new Vertex2D(x, y);
        flipper.Data.StartAngle = startAngle;
        flipper.Data.EndAngle = endAngle;

        Instantiate(table, flipper, name);
    }

    private static void AddBumper(TableComponent table, string name, float x, float y)
    {
        var bumper = BumperItem.GetDefault(table.TableContainer.Table);
        bumper.Name = name;
        bumper.Data.Center = new Vertex2D(x, y);

        Instantiate(table, bumper, name);
    }

    private static void AddPlunger(TableComponent table, string name, float x, float y)
    {
        var plunger = PlungerItem.GetDefault(table.TableContainer.Table);
        plunger.Name = name;
        plunger.Data.Center = new Vertex2D(x, y);

        Instantiate(table, plunger, name);
    }

    /// <summary>
    /// Instancie un élément de table via VPE.
    ///
    /// <para>Passe par <c>InstantiateAndPersistPrefab</c>, interne au package : c'est la même
    /// porte que le Toolbox de VPE, et elle applique les données de l'élément, choisit le prefab
    /// du bon type et range l'objet dans son groupe. La reproduire à moitié donnerait un élément
    /// visible mais sans physique.</para>
    /// </summary>
    private static GameObject Instantiate(TableComponent table, IItem item, string label)
    {
        var converter = new VpxSceneConverter(table);
        table.TableContainer.Refresh();

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var method = typeof(VpxSceneConverter).GetMethod("InstantiateAndPersistPrefab", flags);
        if (method == null)
        {
            Debug.LogError(
                "[BuildIndustriesTable] VPE a changé : « InstantiateAndPersistPrefab » est " +
                $"introuvable sur {nameof(VpxSceneConverter)}. Aucun élément de table n'a été posé.");
            return null;
        }

        object prefab;
        try
        {
            prefab = method.Invoke(converter, new object[] { item, null });
        }
        catch (TargetInvocationException e)
        {
            Debug.LogError($"[BuildIndustriesTable] Création de « {label} » refusée par VPE : " +
                           $"{e.InnerException?.Message ?? e.Message}");
            return null;
        }

        var go = prefab?.GetType().GetProperty("GameObject")?.GetValue(prefab) as GameObject;
        if (go == null)
        {
            Debug.LogError($"[BuildIndustriesTable] « {label} » n'a produit aucun GameObject.");
        }

        return go;
    }

    /// <summary>
    /// Génère les maillages des éléments.
    ///
    /// <para>Un élément VPE est <b>paramétrique</b> : le prefab ne porte qu'un composant et un
    /// <c>MeshFilter</c> vide. Sans cette passe, la table contient bien ses flippers et ses
    /// bumpers, avec leurs colliders et leur physique, mais rien à l'écran — les maillages ont
    /// une taille nulle.</para>
    /// </summary>
    private static void RebuildElementMeshes(TableComponent table)
    {
        var rebuilt = 0;
        foreach (var renderable in table.GetComponentsInChildren<IMainRenderableComponent>(true))
        {
            if (renderable == null)
            {
                continue;
            }

            renderable.RebuildMeshes();
            rebuilt++;
        }

        Debug.Log($"[BuildIndustriesTable] {rebuilt} maillage(s) d'élément régénérés.");

        FillMissingMeshes(table);
    }

    /// <summary>
    /// Donne un maillage aux pièces qui n'en ont pas.
    ///
    /// <para>Les bumpers, cibles et autres pièces de VPE ne sont pas paramétriques : leur prefab
    /// attend des maillages assignés, fournis par la bibliothèque de pièces
    /// (<c>org.visualpinball.unity.assets</c>, 212 Mo). Sans elle, ces pièces existent — colliders,
    /// physique et contacts fonctionnent — mais rien ne les dessine, et la table paraît vide.</para>
    ///
    /// <para>Cette passe pose un cylindre aux dimensions de la pièce à la place du maillage
    /// absent. C'est un repère de travail, pas un habillage : le disque du bumper est au bon
    /// rayon et à la bonne hauteur, donc la table se joue et se voit, mais c'est à l'utilisateur
    /// de poser le vrai maillage quand il l'a.</para>
    /// </summary>
    private static void FillMissingMeshes(TableComponent table)
    {
        var diameter = TableMeters(90f);
        var filled = 0;

        foreach (var filter in table.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh != null || filter.GetComponent<MeshRenderer>() == null)
            {
                continue;
            }

            // Les dimensions viennent du nom de la pièce : c'est le seul repère disponible ici,
            // et il est stable — ce sont les noms du prefab de VPE.
            var (radius, height, y) = filter.name switch {
                "Base" => (diameter * 0.50f, diameter * 0.14f, diameter * 0.07f),
                "Skirt" => (diameter * 0.46f, diameter * 0.18f, diameter * 0.16f),
                "Ring" => (diameter * 0.42f, diameter * 0.10f, diameter * 0.30f),
                "Cap" => (diameter * 0.34f, diameter * 0.34f, diameter * 0.44f),
                _ => (diameter * 0.40f, diameter * 0.20f, diameter * 0.10f)
            };

            var mesh = BuildCylinder(radius, height);
            mesh.name = filter.name + " (repère)";
            filter.sharedMesh = mesh;
            filter.transform.localPosition = new Vector3(0f, y, 0f);
            filled++;
        }

        if (filled > 0)
        {
            Debug.Log($"[BuildIndustriesTable] {filled} pièce(s) sans maillage ont reçu un cylindre " +
                      "de repère. Poser le maillage définitif dans l'Inspector, ou installer la " +
                      "bibliothèque de pièces de VPE (org.visualpinball.unity.assets).");
        }
    }

    /// <summary>Cylindre fermé d'axe vertical, aux dimensions demandées en mètres.</summary>
    private static Mesh BuildCylinder(float radius, float height, int segments = 16)
    {
        var mesh = new Mesh();

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        var half = height * 0.5f;

        // Deux centres, puis deux anneaux : les faces sont des bandes entre anneaux, les
        // couvercles des éventails autour du centre.
        vertices.Add(new Vector3(0f, -half, 0f));   // 0 : centre bas
        vertices.Add(new Vector3(0f, half, 0f));    // 1 : centre haut
        var bottomStart = vertices.Count;
        for (var i = 0; i < segments; i++)
        {
            var a = i / (float)segments * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(a) * radius, -half, Mathf.Sin(a) * radius));
        }
        var topStart = vertices.Count;
        for (var i = 0; i < segments; i++)
        {
            var a = i / (float)segments * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(a) * radius, half, Mathf.Sin(a) * radius));
        }

        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            var b0 = bottomStart + i;
            var b1 = bottomStart + next;
            var t0 = topStart + i;
            var t1 = topStart + next;

            triangles.Add(b0); triangles.Add(t1); triangles.Add(t0);
            triangles.Add(b0); triangles.Add(b1); triangles.Add(t1);

            triangles.Add(0); triangles.Add(b1); triangles.Add(b0);
            triangles.Add(1); triangles.Add(t0); triangles.Add(t1);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddLighting(TableComponent table)
    {
        // La lumière principale éclaire la table par le dessus et un peu de côté : un éclairage
        // bien dans l'axe aplatit complètement un plateau.
        var sun = new GameObject("Sun");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.90f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.65f;
        sun.transform.rotation = Quaternion.Euler(52f, -34f, 0f);

        // Un contre-jour froid, bleu de montagne, qui détache la table du fond.
        var rim = new GameObject("RimLight");
        var rimLight = rim.AddComponent<Light>();
        rimLight.type = LightType.Directional;
        rimLight.color = new Color(0.62f, 0.76f, 1f);
        rimLight.intensity = 0.45f;
        rimLight.shadows = LightShadows.None;
        rim.transform.rotation = Quaternion.Euler(28f, 152f, 0f);

        var ambient = new GameObject("Ambient");
        var ambientLight = ambient.AddComponent<Light>();
        ambientLight.type = LightType.Point;
        ambientLight.color = new Color(1f, 0.72f, 0.45f);
        ambientLight.intensity = 0.9f;
        ambientLight.range = TableMeters(1400f);
        ambient.transform.position = new Vector3(TableMeters(CenterX), 0.9f, TableMeters(-1200f));
    }

    /// <summary>Caméra de jeu, au-dessus des flippers, comme sur une borne.</summary>
    private static void AddCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";

        var camera = go.AddComponent<Camera>();
        camera.fieldOfView = 42f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 60f;

        go.AddComponent<AudioListener>();
        go.AddComponent<UniversalAdditionalCameraData>();

        // Le point de vue est celui du joueur : derrière le bas de table, surélevé, incliné vers
        // le fond. La table va d'environ 0,5 m de large sur 1,17 m de long, centrée en x sur
        // 0,26 m et en z entre -1,17 et 0.
        var center = new Vector3(TableMeters(CenterX), 0f, TableMeters(-1080f));
        go.transform.position = center + new Vector3(0f, 0.52f, 0.78f);
        go.transform.rotation = Quaternion.Euler(38f, 180f, 0f);
    }

    /// <summary>
    /// Décors du thème : industries, montagne, IUT.
    ///
    /// <para>Ils sont posés <b>hors</b> de l'arbre de la table et sans collider : ce sont des
    /// décors, et un collider oublié ici casserait la physique validée de la table. C'est la
    /// convention du projet pour tout son décor.</para>
    /// </summary>
    private static void BuildTheme(TableComponent table)
    {
        var root = new GameObject("Theme").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Theme");

        var steel = EnsureMaterial("Steel", new Color(0.42f, 0.45f, 0.48f), 1f, 0.45f);
        var hazard = EnsureMaterial("Hazard", new Color(0.95f, 0.42f, 0.06f), 0.25f, 0.35f);
        var rock = EnsureMaterial("AlpineRock", new Color(0.32f, 0.33f, 0.36f), 0f, 0.18f);
        var pine = EnsureMaterial("Pine", new Color(0.10f, 0.26f, 0.16f), 0f, 0.25f);
        var snow = EnsureMaterial("Snow", new Color(0.92f, 0.95f, 0.99f), 0f, 0.30f);
        var iutBlue = EnsureMaterial("IutBlue", new Color(0.09f, 0.27f, 0.52f), 0.1f, 0.55f);

        BuildIndustrialFrame(root, steel, hazard);
        BuildMountainBackdrop(root, rock, pine, snow);
        BuildIutSignage(root, iutBlue, steel);
    }

    /// <summary>Charpente d'atelier : poutres, conduites et chevilles de sécurité.</summary>
    private static void BuildIndustrialFrame(Transform parent, Material steel, Material hazard)
    {
        var frame = new GameObject("Industries").transform;
        frame.SetParent(parent, false);

        // Quatre montants aux coins, qui encadrent la table sans la toucher.
        var w = TableMeters(700f);
        var l = TableMeters(1400f);
        for (var i = 0; i < 4; i++)
        {
            var x = (i % 2 == 0 ? -1f : 1f) * w;
            var z = (i < 2 ? -1f : 1f) * l;
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = $"Post.{i + 1}";
            StripCollider(post);
            post.transform.SetParent(frame, false);
            post.transform.localScale = new Vector3(0.07f, 1.5f, 0.07f);
            post.transform.position = new Vector3(x, 0.75f, z);
            post.GetComponent<MeshRenderer>().sharedMaterial = steel;
        }

        // Deux conduites qui courent au-dessus de la table, dans l'axe du jeu.
        for (var i = 0; i < 2; i++)
        {
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = $"Pipe.{i + 1}";
            StripCollider(pipe);
            pipe.transform.SetParent(frame, false);
            pipe.transform.localScale = new Vector3(0.06f, l, 0.06f);
            pipe.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            pipe.transform.position = new Vector3((i == 0 ? -1f : 1f) * w * 0.55f, 1.32f, 0f);
            pipe.GetComponent<MeshRenderer>().sharedMaterial = steel;
        }

        // Bande de sécurité en travers du fond : le repère industriel du thème.
        var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
        band.name = "HazardBand";
        StripCollider(band);
        band.transform.SetParent(frame, false);
        band.transform.localScale = new Vector3(w * 2f + 0.2f, 0.12f, 0.05f);
        band.transform.position = new Vector3(0f, 0.62f, TableMeters(1620f));
        band.GetComponent<MeshRenderer>().sharedMaterial = hazard;
    }

    /// <summary>Crête vosgienne et sapins en anneau, derrière et autour de la table.</summary>
    private static void BuildMountainBackdrop(Transform parent, Material rock, Material pine, Material snow)
    {
        var mountains = new GameObject("Montagne").transform;
        mountains.SetParent(parent, false);

        // Trois plans de crêtes de plus en plus loin : la superposition donne la profondeur
        // sans aucun coût de modèle.
        for (var ridge = 0; ridge < 3; ridge++)
        {
            var distance = 3.4f + ridge * 1.9f;
            var height = 1.5f - ridge * 0.28f;
            var segments = 9 + ridge * 3;

            for (var i = 0; i < segments; i++)
            {
                var t = (i + 0.5f) / segments;
                var x = Mathf.Lerp(-5.2f, 5.2f, t);
                var peak = Mathf.Sin(t * Mathf.PI) * height * (0.65f + 0.35f * Mathf.Sin(i * 2.3f + ridge));
                if (peak < 0.1f)
                {
                    continue;
                }

                var peakGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                peakGo.name = $"Ridge{ridge}.{i}";
                StripCollider(peakGo);
                peakGo.transform.SetParent(mountains, false);
                peakGo.transform.localScale = new Vector3(0.85f, peak * 2f, 0.5f);
                peakGo.transform.position = new Vector3(x, peak - 0.7f, distance);
                peakGo.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                peakGo.GetComponent<MeshRenderer>().sharedMaterial = ridge == 0 ? snow : rock;
            }
        }

        // Sapins : un cône sur un tronc, en anneau autour de la table.
        for (var i = 0; i < 26; i++)
        {
            var angle = i / 26f * Mathf.PI * 2f;
            var radius = 4.6f + (i % 3) * 0.7f;
            var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius * 1.6f + 0.4f);
            var scale = 0.7f + (i % 4) * 0.12f;

            var tree = new GameObject($"Pine.{i + 1}").transform;
            tree.SetParent(mountains, false);
            tree.position = position;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            StripCollider(trunk);
            trunk.transform.SetParent(tree, false);
            trunk.transform.localScale = new Vector3(0.06f, 0.16f, 0.06f);
            trunk.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = rock;

            var crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crown.name = "Crown";
            StripCollider(crown);
            crown.transform.SetParent(tree, false);
            crown.transform.localScale = new Vector3(0.34f, 0.62f, 0.34f);
            crown.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            crown.GetComponent<MeshRenderer>().sharedMaterial = pine;

            tree.localScale = Vector3.one * scale;
        }
    }

    /// <summary>Signalétique de l'IUT : un panneau par département, derrière la table.</summary>
    private static void BuildIutSignage(Transform parent, Material panel, Material frame)
    {
        var signage = new GameObject("IUT").transform;
        signage.SetParent(parent, false);

        var departments = new[] { "INFORMATIQUE", "RÉSEAUX", "GÉNIE INDUSTRIEL", "MESURES PHYSIQUES" };

        for (var i = 0; i < departments.Length; i++)
        {
            var t = (i + 0.5f) / departments.Length;
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Panel." + departments[i].Split(' ')[0];
            StripCollider(board);
            board.transform.SetParent(signage, false);
            board.transform.localScale = new Vector3(1.05f, 0.32f, 0.04f);
            board.transform.position = new Vector3(Mathf.Lerp(-3.2f, 3.2f, t) - 1.6f, 1.95f, -2.6f);
            board.transform.rotation = Quaternion.Euler(0f, 8f, 0f);
            board.GetComponent<MeshRenderer>().sharedMaterial = panel;

            var label = new GameObject("Label", typeof(TextMesh));
            label.transform.SetParent(board.transform, false);
            var text = label.GetComponent<TextMesh>();
            text.text = departments[i];
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = UnityEngine.TextAlignment.Center;
            text.characterSize = 0.028f;
            text.fontSize = 64;
            text.color = Color.white;
            label.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }

    /// <summary>
    /// Retire le collider d'une primitive de décor.
    ///
    /// <para><see cref="GameObject.CreatePrimitive"/> en pose toujours un. Sur du décor, c'est un
    /// collider qui n'a rien à faire là : la table validate du projet a déjà été cassée une fois
    /// par un collider oublié.</para>
    /// </summary>
    private static void StripCollider(GameObject go)
    {
        var collider = go.GetComponent<UnityEngine.Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
    }

    private static float TableMeters(float vpxUnits) => vpxUnits / VpxUnitsPerMeter;

    /// <summary>
    /// Crée (ou réutilise) un matériau URP du thème, enregistré comme asset pour survivre à la
    /// scène et être inclus au build.
    /// </summary>
    private static Material EnsureMaterial(string name, Color color, float metallic, float smoothness)
    {
        var path = $"{ThemeFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        if (!AssetDatabase.IsValidFolder(ThemeFolder))
        {
            AssetDatabase.CreateFolder("Assets/VpeUrp/Art", "Theme");
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[BuildIndustriesTable] Shader URP Lit introuvable : le thème " +
                           "utilisera le matériau par défaut.");
            return null;
        }

        var material = new Material(shader) { name = name };
        material.SetColor(Shader.PropertyToID("_BaseColor"), color);
        material.SetFloat(Shader.PropertyToID("_Metallic"), metallic);
        material.SetFloat(Shader.PropertyToID("_Smoothness"), smoothness);

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void ConfigureRenderSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.50f, 0.60f);
        RenderSettings.ambientEquatorColor = new Color(0.30f, 0.33f, 0.38f);
        RenderSettings.ambientGroundColor = new Color(0.14f, 0.14f, 0.16f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.80f, 0.86f, 0.94f);
        RenderSettings.fogDensity = 0.012f;
    }

    private static void AddSceneToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var scene in scenes)
        {
            if (scene.path == path)
            {
                scene.enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
