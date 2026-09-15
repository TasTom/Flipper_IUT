// Builds the world around the pinball table from the CC0 packs listed in
// Assets/References/ASSET_CATALOG.md, step 4 ("Add Kenney arcade/forest props only after the table
// proportions are approved"):
//
//   Mini Arcade           -> the bay the machine stands in: floor, walls, columns, cabinets, vending
//   City Kit (Industrial) -> the IUT surroundings outside the bay
//   Mini Forest           -> snowy campus silhouettes behind the buildings
//   Poly Haven HDRI       -> winter sky/ambient light and the reflections on the chrome parts
//
// Scale reasoning. The game camera sits at (0.565, 17, -13) with a 40 degree field of view, which
// puts roughly x in [-8.2, 9.3] on screen while the cabinet occupies x in [-3.83, 4.96]. The bay is
// therefore built so that its left wall face lands on the left frame edge and its floor fills the
// rest, matching the reference image, where only a wall sliver and bare floor are visible around the
// table. Everything further out (campus, forest) is sized to clear the backbox at the top of frame.
//
// The environment is deliberately collider-free: it is dressing, and the table's physics tuning must
// not be disturbed by a stray collider on a background cabinet.
//
// Menu: Flipper > Rebuild Arcade Environment
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildArcadeEnvironment
{
    // --- scale -------------------------------------------------------------------------------
    const float EnvScale = 6.5f;    // arcade pack: 0.73-unit cabinet -> 4.7-unit cabinet
    const float CityScale = 30f;    // industrial pack: 1.47-unit building -> 44-unit building
    const float ForestScale = 36f;  // forest pack: 1.68-unit tree -> 60-unit tree

    // --- bay ---------------------------------------------------------------------------------
    const float BayCentreX = 0.565f;   // same centre as the table assembly
    const float WallLeftX = -8.60f;    // inner face lands on the left frame edge at x = -8.0
    const float WallRightX = 17.60f;   // closed off-screen: the reference shows bare floor there
    const float WallBackZ = 12.60f;
    const float WallFrontZ = -20.60f;
    const float FloorTile = 6f;
    const float FloorTopY = -0.35f;    // arcade floor, just under the cabinet
    const float GroundTopY = -0.55f;   // exterior snow, a shade lower so it cannot z-fight
    const float WallW = 6f, WallH = 10f, WallT = 1.2f;

    const string ArcadeDir = "Assets/Models/Kenney/Arcade/";
    const string CityDir = "Assets/Models/Kenney/CityKit/";
    const string ForestDir = "Assets/Models/Kenney/Forest/";
    const string HdrPath = "Assets/Textures/CC0/PolyHaven/snowy_park_01_1k.hdr";

    [MenuItem("Flipper/Rebuild Arcade Environment")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[BuildArcadeEnvironment] Stop play mode before rebuilding the environment.");
            return;
        }

        var root = FindOrCreate("Environment", null).transform;
        for (int i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);

        var floor = FindOrCreate("ArcadeFloor", root).transform;
        var walls = FindOrCreate("ArcadeWalls", root).transform;
        var props = FindOrCreate("ArcadeProps", root).transform;
        var campus = FindOrCreate("CampusExterior", root).transform;

        int n = 0;
        n += BuildFloor(floor);
        n += BuildWalls(walls);
        n += BuildProps(props);
        n += BuildCampus(campus);
        TuneKenneyMaterials();
        ConfigureWinterSkybox();

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[BuildArcadeEnvironment] rebuilt {n} environment objects from the CC0 packs");
    }

    // ---------------------------------------------------------------- floor

    static int BuildFloor(Transform parent)
    {
        // 4 x 6 tiles of 6 units: the pattern stays readable in the strip of floor the camera sees.
        const int nx = 4, nz = 6;
        float originX = -(nx - 1) * FloorTile / 2f;
        float originZ = -(nz - 1) * FloorTile / 2f;
        int count = 0;
        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            var pos = new Vector3(BayCentreX + originX + ix * FloorTile, FloorTopY - 0.18f,
                                  originZ + iz * FloorTile);
            if (Place(parent, ArcadeDir + "floor.glb", pos, 0f, Vector3.one * FloorTile) != null) count++;
        }
        return count;
    }

    // ---------------------------------------------------------------- walls

    static int BuildWalls(Transform parent)
    {
        int count = 0;

        // Side walls run along Z; the panel's width axis is rotated onto Z so the thickness stays
        // on X. Kenney's panel is 1 x 1 x 0.6 units, hence the non-uniform scale.
        var sideScale = new Vector3(WallT, WallH, WallW);
        for (int i = -3; i <= 3; i++)
        {
            float z = i * WallW;
            string left = (i == 1) ? "wall-window.glb" : (i == -2 ? "wall-door-rotate.glb" : "wall.glb");
            if (Place(parent, ArcadeDir + left, new Vector3(WallLeftX, FloorTopY, z), 0f, sideScale) != null) count++;
            if (Place(parent, ArcadeDir + "wall.glb", new Vector3(WallRightX, FloorTopY, z), 0f, sideScale) != null) count++;
        }

        var backScale = new Vector3(WallW, WallH, WallT);
        for (int i = -2; i <= 3; i++)
        {
            float x = BayCentreX + i * WallW;
            string model = (i == 2) ? "wall-window.glb" : "wall.glb";
            if (Place(parent, ArcadeDir + model, new Vector3(x, FloorTopY, WallBackZ), 0f, backScale) != null) count++;
        }

        for (int i = -3; i <= 3; i++)
            if (Place(parent, ArcadeDir + "wall.glb", new Vector3(BayCentreX + i * WallW, FloorTopY, WallFrontZ),
                      0f, backScale) != null) count++;

        // Columns break up the long runs and give the corners a reason to exist.
        foreach (var p in new[]
        {
            new Vector3(WallLeftX, FloorTopY, WallBackZ), new Vector3(WallLeftX, FloorTopY, WallFrontZ),
            new Vector3(WallRightX, FloorTopY, WallBackZ), new Vector3(WallRightX, FloorTopY, WallFrontZ),
        })
            if (Place(parent, ArcadeDir + "column.glb", p, 0f, Vector3.one * 10f) != null) count++;

        return count;
    }

    // ---------------------------------------------------------------- props

    static int BuildProps(Transform parent)
    {
        // Cabinets line the walls. Yaw 90 turns the 0.54-unit depth onto X so the row faces the
        // table; at EnvScale the cabinet body is 3.6 x 6.6 x 4.9 units.
        int count = 0;

        var leftRow = new[] { "arcade-machine", "pinball", "claw-machine", "arcade-machine" };
        for (int i = 0; i < leftRow.Length; i++)
        {
            float z = -10.5f + i * 7f;
            if (Place(parent, ArcadeDir + leftRow[i] + ".glb",
                      new Vector3(-8.2f, FloorTopY, z), 90f, Vector3.one * EnvScale) != null) count++;
        }

        var rightRow = new[] { "dance-machine", "vending-machine", "gambling-machine", "ticket-machine" };
        for (int i = 0; i < rightRow.Length; i++)
        {
            float z = -10.5f + i * 7f;
            if (Place(parent, ArcadeDir + rightRow[i] + ".glb",
                      new Vector3(10.0f, FloorTopY, z), -90f, Vector3.one * EnvScale) != null) count++;
        }

        // Back of the room: the cabinets that read over the backbox.
        var backRow = new[] { "air-hockey", "basketball-game", "prize-wheel", "cash-register", "prizes" };
        for (int i = 0; i < backRow.Length; i++)
        {
            float x = -10f + i * 6f;
            if (Place(parent, ArcadeDir + backRow[i] + ".glb",
                      new Vector3(x, FloorTopY, 12.0f), 180f, Vector3.one * EnvScale) != null) count++;
        }

        // Two gamers in the aisle, well clear of the table's footprint.
        if (Place(parent, ArcadeDir + "character-gamer.glb", new Vector3(-6.4f, FloorTopY, 7.5f), 150f,
                  Vector3.one * EnvScale) != null) count++;
        if (Place(parent, ArcadeDir + "character-employee.glb", new Vector3(8.0f, FloorTopY, 8.4f), -160f,
                  Vector3.one * EnvScale) != null) count++;

        return count;
    }

    // ---------------------------------------------------------------- exterior

    static int BuildCampus(Transform parent)
    {
        int count = 0;

        // Snow ground under everything outside the bay.
        var ground = FindOrCreate("Snow_Ground", parent);
        ground.transform.localPosition = new Vector3(BayCentreX, GroundTopY - 0.2f, 0f);
        ground.transform.localRotation = Quaternion.identity;
        ground.transform.localScale = new Vector3(420f, 0.4f, 420f);
        EnsureBox(ground, LoadOrCreateSnow());
        count++;

        // Industrial ring behind the bay. At this radius only the tall blocks clear the backbox,
        // which is exactly the "background buildings" role the catalog assigns them.
        var ring = new[]
        {
            "building-a", "building-e", "building-h", "building-c", "building-m",
            "building-b", "building-k", "building-f", "building-p", "building-r",
        };
        for (int i = 0; i < ring.Length; i++)
        {
            float t = (i + 0.5f) / ring.Length * Mathf.PI * 2f;
            float r = 62f + (i % 3) * 14f;
            var pos = new Vector3(BayCentreX + Mathf.Sin(t) * r, GroundTopY, Mathf.Cos(t) * r + 8f);
            if (Place(parent, CityDir + ring[i] + ".glb", pos, -t * Mathf.Rad2Deg,
                      Vector3.one * CityScale) != null) count++;
        }

        // Landmarks: water tower, windmill, chimneys and containers.
        var landmarks = new (string model, float x, float z, float yaw)[]
        {
            ("water-tower",             -40f,  56f,  20f),
            ("windmill",                 46f,  50f, -30f),
            ("chimney-large",           -58f, -34f,  90f),
            ("chimney-medium",           52f, -44f,  90f),
            ("shipping-container-a",     36f, -26f,  35f),
            ("shipping-container-b",     40f, -22f,  35f),
            ("solar-panel-landscape",   -46f,  16f, -15f),
            ("detail-tank-large",        30f,  34f,   0f),
        };
        foreach (var (model, x, z, yaw) in landmarks)
            if (Place(parent, CityDir + model + ".glb", new Vector3(x, GroundTopY, z), yaw,
                      Vector3.one * CityScale) != null) count++;

        // Snowy forest band outside the buildings. The radius is chosen against the game camera's
        // sightline: at a 40 degree field of view and 54 degrees pitch, a tree at z = +96 has to be
        // ~50 units tall to clear the backbox, and one at z = +120 would need 70. The ring therefore
        // sits at 96-150 with a 60-unit trunk; wider spacing keeps the campus visible through it.
        var treeRing = new[] { "tree-high", "tree", "tree", "tree-high", "tree" };
        for (int i = 0; i < 26; i++)
        {
            float t = i / 26f * Mathf.PI * 2f + 0.4f;
            float r = 98f + (i % 5) * 13f;
            var pos = new Vector3(BayCentreX + Mathf.Sin(t) * r, GroundTopY, Mathf.Cos(t) * r + 8f);
            float s = ForestScale * (0.85f + (i % 4) * 0.12f);
            if (Place(parent, ForestDir + treeRing[i % treeRing.Length] + ".glb", pos, i * 37f,
                      Vector3.one * s) != null) count++;
        }

        // Rocks and ground patches to break the treeline.
        var shrubs = new (string model, float x, float z)[]
        {
            ("rocks-low", -26f, 30f), ("rocks-high", 28f, 26f), ("stones", -22f, -30f),
            ("rocks-low", 24f, -28f), ("patch-grass", 18f, -24f), ("patch-dirt", -20f, 22f),
        };
        foreach (var (model, x, z) in shrubs)
            if (Place(parent, ForestDir + model + ".glb", new Vector3(x, GroundTopY, z), 0f,
                      Vector3.one * ForestScale) != null) count++;

        return count;
    }

    // ---------------------------------------------------------------- material tuning

    /// <summary>
    /// Kenney ships every model in a pack on one bright atlas. Left untouched, the arcade floor reads
    /// as a high-contrast checker that pulls the eye off the table, so the shared base colour of the
    /// floor and wall assets is pulled down and their roughness raised. Each GLB owns its own
    /// copy of the material, so tinting the floor does not touch the cabinets.
    /// </summary>
    static void TuneKenneyMaterials()
    {
        Tint(ArcadeDir + "floor.glb", new Color(0.42f, 0.46f, 0.52f), 0.88f);
        Tint(ArcadeDir + "wall.glb", new Color(0.72f, 0.74f, 0.80f), 0.90f);
        Tint(ArcadeDir + "wall-window.glb", new Color(0.72f, 0.74f, 0.80f), 0.90f);
        Tint(ArcadeDir + "wall-door-rotate.glb", new Color(0.72f, 0.74f, 0.80f), 0.90f);
        Tint(ArcadeDir + "wall-corner.glb", new Color(0.72f, 0.74f, 0.80f), 0.90f);
        Tint(ArcadeDir + "column.glb", new Color(0.66f, 0.68f, 0.74f), 0.85f);
    }

    static void Tint(string glbPath, Color factor, float roughness)
    {
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(glbPath))
        {
            var m = a as Material;
            if (m == null) continue;
            if (m.HasProperty("baseColorFactor")) m.SetColor("baseColorFactor", factor);
            else if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", factor);
            if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", roughness);
            else if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f - roughness);
            EditorUtility.SetDirty(m);
        }
    }

    // ---------------------------------------------------------------- skybox / ambient

    static void ConfigureWinterSkybox()
    {
        var importer = AssetImporter.GetAtPath(HdrPath) as TextureImporter;
        if (importer != null && (importer.sRGBTexture || importer.wrapMode != TextureWrapMode.Clamp))
        {
            // A sky panorama is linear data and must not tile horizontally.
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        var hdr = AssetDatabase.LoadAssetAtPath<Texture2D>(HdrPath);
        if (hdr == null)
        {
            Debug.LogWarning($"[BuildArcadeEnvironment] {HdrPath} missing; keeping the default skybox");
            return;
        }

        const string matName = "WinterSky_Mat";
        string path = $"Assets/Materials/{matName}.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Skybox/Panoramic");
        if (shader == null) { Debug.LogWarning("[BuildArcadeEnvironment] Skybox/Panoramic shader unavailable"); return; }

        if (sky == null)
        {
            sky = new Material(shader) { name = matName };
            AssetDatabase.CreateAsset(sky, path);
        }
        else if (sky.shader != shader)
        {
            sky.shader = shader;
        }
        sky.SetTexture("_MainTex", hdr);
        sky.SetFloat("_Mapping", 1f);      // latitude/longitude layout
        sky.SetFloat("_ImageType", 0f);
        sky.SetFloat("_Exposure", 1.15f);
        EditorUtility.SetDirty(sky);

        RenderSettings.skybox = sky;
        // Reflect the winter sky so the chrome ball and rails pick up the surroundings, but keep the
        // ambient low: the playfield is lit by the inserts and the point lights, not by the sky.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 0.55f;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;

        // The scene shipped with an exponential-squared fog at a near-black navy (0.05, 0.10, 0.14),
        // which swallowed everything past ~50 units and turned the campus into a dark blue smear.
        // A winter haze that matches the sky lets the far buildings fade out instead of black out.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.80f, 0.86f, 0.94f);
        RenderSettings.fogDensity = 0.0022f;

        DynamicGI.UpdateEnvironment();
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Instantiates a Kenney GLB under <paramref name="parent"/> at a fixed pose, without colliders.</summary>
    static Transform Place(Transform parent, string glbPath, Vector3 pos, float yaw, Vector3 scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[BuildArcadeEnvironment] missing model {glbPath}");
            return null;
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        inst.name = System.IO.Path.GetFileNameWithoutExtension(glbPath);
        inst.transform.localPosition = pos;
        inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        inst.transform.localScale = scale;

        // Dressing must never take part in the simulation.
        foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        return inst.transform;
    }

    static void EnsureBox(GameObject go, Material mat)
    {
        var mf = go.GetComponent<MeshFilter>();
        if (mf == null) mf = go.AddComponent<MeshFilter>();
        if (mf.sharedMesh == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mf.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }
        var r = go.GetComponent<MeshRenderer>();
        if (r == null) r = go.AddComponent<MeshRenderer>();
        if (mat != null) r.sharedMaterial = mat;
    }

    static Material LoadOrCreateSnow()
    {
        // Snow015 is snow *over grass*: its albedo carries green through, which the catalog confines
        // to narrow playfield accents. The exterior ground is thousands of units wide, so it uses a
        // flat cold white instead and keeps the texture out of the equation entirely.
        string path = "Assets/Materials/SnowGround_Mat.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "SnowGround_Mat" };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_BaseMap", null);
        m.SetColor("_BaseColor", new Color(0.87f, 0.92f, 1.00f));
        m.SetFloat("_Metallic", 0.0f);
        m.SetFloat("_Smoothness", 0.30f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject FindOrCreate(string name, Transform parent)
    {
        var found = GameObject.Find(name);
        if (found != null)
        {
            if (parent != null && found.transform.parent != parent) found.transform.SetParent(parent, false);
            return found;
        }
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>Model folders and sky panorama that the environment expects on disk.</summary>
    public static List<string> MissingModels()
    {
        var missing = new List<string>();
        foreach (var dir in new[] { ArcadeDir, CityDir, ForestDir })
            if (!AssetDatabase.IsValidFolder(dir.TrimEnd('/'))) missing.Add(dir);
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(HdrPath) == null) missing.Add(HdrPath);
        return missing;
    }
}
