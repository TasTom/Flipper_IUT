// Rebuilds the Flipper_IUT table so its structure matches the vertical reference in
// Assets/References/, following the hybrid strategy in ASSET_CATALOG.md:
//   - mechanical parts  : the generated Krea GLBs already parented to the gameplay hosts
//   - cabinet + rails   : CC0 wood / metal / snow / ice materials
//   - school identity   : the IUT campus model in the backglass
//
// Re-runnable: it repositions the existing gameplay hosts (Bumper_xx, Target_xx, Flipper_xx_Pivot,
// Plunger, ...) instead of deleting them, so their scripts, colliders and Krea children survive.
//
// Menu: Flipper > Rebuild Table Layout
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildPinballTable
{
    // ---------------------------------------------------------------- layout constants
    // Play area keeps the existing wall positions so the gameplay tuning stays valid.
    const float HalfX = 3.475f;   // inner face of the play area (left wall mirrors this)
    const float HalfZ = 7.30f;    // inner half length (bottom apron -> top wall)
    const float WallT = 0.35f;
    const float WallH = 1.00f;    // realistic wall height: ~5% of table width, not 26%
    const float SlabY = 0.14f;    // playfield slab thickness
    const float PlayY = 0.07f;    // top surface of the playfield slab

    // Raised shooter lane. PlayY is the playfield top; the lane floor sits LaneFloorTop above it
    // so a ball that has left the lane can never roll back in — see BuildPlungerLane.
    const float LaneFloorTop = 0.27f;
    const float LaneFloorThickness = 0.40f;
    const float BallRadius = 0.225f;

    // Plunger lane. These are measured between wall FACES, not centres: using centre-to-centre
    // distances here leaves a 0.55 gap for a 0.45 ball, which jams it against the divider.
    const float LaneClear = 0.78f;
    const float LaneInnerX = HalfX + WallT / 2f;                 // divider centre (also the play wall)
    const float LaneInnerFace = HalfX + WallT;                   // divider's lane-side face
    const float LaneCentreX = LaneInnerFace + LaneClear / 2f;    // plunger / ball spawn axis
    const float LaneOuterX = LaneInnerFace + LaneClear + WallT / 2f;  // cabinet outer wall centre

    // Centre of the whole assembly (play area + plunger lane): everything that should read as
    // "middle of the table" uses this, not the lane axis.
    const float TableCenterX = (-HalfX - WallT + LaneOuterX + WallT / 2f) / 2f;

    const float FlipperZ = -4.80f;
    const float FlipperX = 2.30f;

    // Portrait framing like the reference. The lift/back pair sets how much of the arcade bay built
    // by BuildArcadeEnvironment stays in shot. At 17/13 the table keeps the validated framing and
    // the bay's left wall / floor land on the frame edges, exactly as in the reference image.
    const float CamLift = 17.0f;
    const float CamBack = -13.0f;
    const float CamPitch = 54.0f;
    const float CamFov = 40.0f;

    static readonly List<string> Built = new List<string>();

    [MenuItem("Flipper/Rebuild Table Layout")]
    public static void Rebuild()
    {
        // Scene edits and MarkSceneDirty are illegal in play mode; the menu item would otherwise
        // throw InvalidOperationException halfway through the rebuild.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[BuildPinballTable] Stop play mode before rebuilding the table.");
            return;
        }

        Built.Clear();

        var root = FindOrCreate("PinballTable", null).transform;
        var furniture = FindOrCreate("Furniture", root).transform;
        var rails = FindOrCreate("Rails", root).transform;
        var lights = FindOrCreate("TableLights", root).transform;

        ClearGenerated(furniture);
        ClearGenerated(rails);
        ClearGenerated(lights);
        PurgeLegacy();

        BuildSurface(furniture);
        BuildCabinet(furniture);
        BuildPlungerLane(furniture);
        BuildApron(furniture);
        BuildInlanes(rails);
        BuildOrbit(rails);
        BuildWireformRamp(rails);
        BuildInserts(furniture);
        BuildBackbox(furniture);
        BuildGlass(furniture);
        BuildLighting(lights);

        PlaceGameplayParts();
        LayoutHud();
        MaterialiseAll();

        // ASSET_CATALOG step 4: the Kenney arcade/forest props and the winter HDRI. The environment
        // keeps its own root so it can be rebuilt on its own from Flipper > Rebuild Arcade Environment.
        BuildArcadeEnvironment.Rebuild();

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[BuildPinballTable] rebuilt {Built.Count} furniture objects");
    }

    // ---------------------------------------------------------------- surface

    static void BuildSurface(Transform parent)
    {
        Prim(parent, "Playfield_Surface", new Vector3(TableCenterX, -SlabY / 2f, 0f),
            new Vector3(HalfX * 2f + LaneClear + WallT * 2f, SlabY, HalfZ * 2f + WallT * 2f));

        // Progressively darker inlays read as the reference's printed playfield art.
        Prim(parent, "Playfield_Art", new Vector3(TableCenterX, PlayY - 0.005f, 0.35f),
            new Vector3(HalfX * 2f - 0.1f, 0.01f, HalfZ * 2f - 1.1f));

        Prim(parent, "Playfield_Center", new Vector3(TableCenterX, PlayY - 0.004f, 1.2f),
            new Vector3(4.6f, 0.01f, 6.4f));

        // Snow drifting against the rails: the catalog's "playfield snow accents".
        Prim(parent, "Snow_EdgeL", new Vector3(-HalfX + 0.32f, PlayY + 0.004f, 0f),
            new Vector3(0.55f, 0.008f, HalfZ * 2f));
        Prim(parent, "Snow_EdgeR", new Vector3(HalfX - 0.32f, PlayY + 0.004f, 0f),
            new Vector3(0.55f, 0.008f, HalfZ * 2f));
        Prim(parent, "Snow_EdgeTop", new Vector3(TableCenterX, PlayY + 0.004f, HalfZ - 0.30f),
            new Vector3(HalfX * 2f, 0.008f, 0.55f));
        Prim(parent, "Snow_EdgeBottom", new Vector3(TableCenterX, PlayY + 0.004f, -HalfZ + 2.30f),
            new Vector3(HalfX * 2f, 0.008f, 0.75f));
        Prim(parent, "Snow_Apron", new Vector3(TableCenterX + 1.9f, 0.585f, -HalfZ + 1.40f),
            new Vector3(2.2f, 0.008f, 0.5f));
    }

    // ---------------------------------------------------------------- cabinet

    static void BuildCabinet(Transform parent)
    {
        // Left wall, top wall, and the outer wall of the plunger lane.
        Box(parent, "Cab_Left", new Vector3(-HalfX - WallT / 2f, WallH / 2f, 0f), new Vector3(WallT, WallH, HalfZ * 2f + WallT * 2f));
        Box(parent, "Cab_Top", new Vector3(TableCenterX, WallH / 2f, HalfZ + WallT / 2f), new Vector3(HalfX * 2f + LaneClear + WallT * 2f, WallH, WallT));
        Box(parent, "Cab_Right", new Vector3(LaneOuterX, WallH / 2f, 0f), new Vector3(WallT, WallH, HalfZ * 2f + WallT * 2f));

        // The play-area wall doubles as the plunger lane divider; leave a gap at the top so the
        // ball can leave the lane and join the orbit.
        float laneTop = HalfZ - 1.1f;
        float dividerLen = laneTop - (-HalfZ);
        float dividerZ = (-HalfZ + laneTop) / 2f;
        Box(parent, "Cab_LaneDivider", new Vector3(LaneInnerX, WallH / 2f, dividerZ), new Vector3(WallT, WallH, dividerLen));

        // Cabinet body under the playfield.
        Prim(parent, "Cab_Body", new Vector3(TableCenterX, -0.85f, 0f),
            new Vector3(HalfX * 2f + LaneClear + WallT * 2f, 1.5f, HalfZ * 2f + WallT * 2f));
    }

    static void BuildPlungerLane(Transform parent)
    {
        // Raised lane floor. It stands LaneFloorTop above the playfield, so leaving the lane at the
        // top and dropping onto the playfield is a one-way trip: a ball coming back down the
        // playfield would have to climb the step. That is the job a one-way gate does on a real
        // table. Without it a ball that came back down re-entered the lane, and since the lane is a
        // legitimate resting place the anti-stuck in BallManager just looped on it forever.
        //
        // The slab runs the lane's full length and stops exactly at the lane's inner face, so its
        // edge is the step — it must not spill into the playfield.
        Box(parent, "Lane_Floor", new Vector3(LaneCentreX, LaneFloorTop - LaneFloorThickness / 2f, 0f),
            new Vector3(LaneClear, LaneFloorThickness, HalfZ * 2f));

        // Lane exit. The ball leaves the lane at 14+ m/s and has to be turned from +Z to -X. A wall
        // whose face points up-and-right reflects it exactly left; a wall facing up-left (the first
        // attempt) sent it straight back down the lane.
        //
        // Two things matter here: the chamfer must stop just short of the top wall, and just short
        // of the lane's outer wall. Overlapping it with either creates an internal corner that wedges
        // the ball and fires it vertically out of the table.
        Box(parent, "Lane_Exit", new Vector3(4.33f, 0.42f, 7.05f),
            new Vector3(0.16f, 0.66f, 0.72f), new Vector3(0f, -45f, 0f));
    }

    // ---------------------------------------------------------------- legacy cleanup

    /// <summary>
    /// Removes the objects left by the first hand-built pass. Without this the old walls, apron and
    /// backbox overlap the generated furniture — and the old apron sat squarely across the drain
    /// mouth, so a lost ball could never reach the DrainZone.
    /// </summary>
    static void PurgeLegacy()
    {
        string[] replacements =
        {
            "TableBase", "Playfield_Top", "Apron", "Backbox",
            "Leg_FL", "Leg_FR", "Leg_BL", "Leg_BR",
            "Wall_Left_PB", "Wall_Right_PB", "Wall_Top_PB",
            "Guide_Left_PB", "Guide_Right_PB",
        };

        foreach (var name in replacements)
        {
            var go = GameObject.Find(name);
            if (go == null) continue;
            Object.DestroyImmediate(go);
        }

        // These two host generated Krea visuals, so only their own geometry goes away.
        foreach (var name in new[] { "Ramp_Center_PB", "LaneDivider" })
        {
            var go = GameObject.Find(name);
            if (go == null) continue;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            var c = go.GetComponent<Collider>();
            if (c != null) c.enabled = false;
        }
    }

    // ---------------------------------------------------------------- apron & drain

    static void BuildApron(Transform parent)
    {
        // Bottom of the table is -Z, so the apron sits at negative Z. (Getting this sign wrong puts
        // the apron across the top return lanes instead of over the drain.)
        const float apronZ = -(HalfZ - 0.85f);      // -6.45
        const float gap = 0.95f;                    // half-width of the drain mouth

        // The apron covers the play area only. Extending it to LaneOuterX swallows the plunger lane
        // and traps the ball inside the collider.
        const float leftOuter = -HalfX;
        const float rightOuter = HalfX;

        // Two side panels: the middle stays open so a drained ball falls through to the DrainZone.
        float lw = (-gap) - leftOuter;
        Prim(parent, "Apron_L", new Vector3(leftOuter + lw / 2f, 0.28f, apronZ), new Vector3(lw, 0.56f, 2.0f));

        float rw = rightOuter - gap;
        Prim(parent, "Apron_R", new Vector3(gap + rw / 2f, 0.28f, apronZ), new Vector3(rw, 0.56f, 2.0f));

        // Front skirt, low enough to stay below the playfield line.
        Prim(parent, "Apron_Front", new Vector3(TableCenterX, -0.30f, -HalfZ - 0.1f),
            new Vector3(HalfX * 2f + LaneClear + WallT * 2f, 0.85f, WallT));

        // Lips that steer a lost ball toward the middle gap.
        Box(parent, "Drain_LipL", new Vector3(-gap - 0.95f, 0.16f, apronZ + 0.85f), new Vector3(2.3f, 0.32f, 0.16f), new Vector3(0f, -26f, 0f));
        Box(parent, "Drain_LipR", new Vector3(gap + 0.95f, 0.16f, apronZ + 0.85f), new Vector3(2.3f, 0.32f, 0.16f), new Vector3(0f, 26f, 0f));

        // Instruction-strip placeholders either side of the drain, as on the reference apron.
        Prim(parent, "Apron_LabelL", new Vector3(leftOuter + lw / 2f, 0.60f, apronZ + 0.55f), new Vector3(2.2f, 0.02f, 0.46f));
        Prim(parent, "Apron_LabelR", new Vector3(gap + rw / 2f, 0.60f, apronZ + 0.55f), new Vector3(2.2f, 0.02f, 0.46f));
    }

    // ---------------------------------------------------------------- inlane / outlane guides

    static void BuildInlanes(Transform parent)
    {
        // Each side gets an outlane (next to the wall, feeds a kickback) and an inlane (returns the
        // ball to the flipper). The divider between them is the signature "7" shape.
        //
        // All of this belongs at the BOTTOM of the table, in the band between the flipper line
        // (z = -4.8) and the apron (z = -5.45). Building it at "+HalfZ - x" puts the whole assembly at
        // the top of the table, where it blocks the lane exit and wedges the ball.
        for (int s = -1; s <= 1; s += 2)
        {
            // Divider between inlane and outlane, angled inward like the reference.
            Box(parent, $"Inlane_Divider_{Side(s)}", new Vector3(s * 2.92f, 0.22f, -4.88f),
                new Vector3(0.14f, 0.44f, 1.30f), new Vector3(0f, s * 22f, 0f));

            // Outer lip hugging the wall, forming the outlane channel.
            Box(parent, $"Outlane_Lip_{Side(s)}", new Vector3(s * 3.28f, 0.18f, -4.92f),
                new Vector3(0.12f, 0.36f, 1.05f), new Vector3(0f, s * 3f, 0f));

            // Kickback post at the bottom of the outlane: a saved ball bounces back into play.
            Prim(parent, $"Kickback_{Side(s)}", new Vector3(s * 3.24f, 0.24f, -5.34f),
                new Vector3(0.40f, 0.24f, 0.40f), null, PrimitiveType.Cylinder);

            // Rubber post above the flipper, clear of the bat's swing arc.
            Prim(parent, $"Post_{Side(s)}", new Vector3(s * 3.02f, 0.28f, -3.55f),
                new Vector3(0.34f, 0.28f, 0.34f), null, PrimitiveType.Cylinder);
        }
    }

    // ---------------------------------------------------------------- top orbit

    static void BuildOrbit(Transform parent)
    {
        // Outer guide: a shallow arc across the top that defines the orbit lane. It stops short of
        // the right wall so a ball leaving the plunger lane can feed into the orbit.
        const int segments = 13;
        const float outerRight = HalfX - 1.15f;
        const float innerRight = HalfX - 2.05f;

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            // The left end stops short of the wall too, giving the ball a way down into the play area.
            float x = Mathf.Lerp(-HalfX + 0.85f, outerRight, t);
            float z = HalfZ - 0.62f - Mathf.Sin(t * Mathf.PI) * 0.85f;
            float yaw = Mathf.Lerp(28f, -28f, t);
            Box(parent, $"Orbit_Outer_{i:00}", new Vector3(x, 0.34f, z), new Vector3(0.72f, 0.62f, 0.14f), new Vector3(0f, yaw, 0f));
        }

        // Inner guide: the "skill shot" alley ceiling, tighter and lower.
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            float x = Mathf.Lerp(-HalfX + 1.45f, innerRight, t);
            float z = HalfZ - 1.95f - Mathf.Sin(t * Mathf.PI) * 0.55f;
            float yaw = Mathf.Lerp(22f, -22f, t);
            Box(parent, $"Orbit_Inner_{i:00}", new Vector3(x, 0.30f, z), new Vector3(0.62f, 0.54f, 0.13f), new Vector3(0f, yaw, 0f));
        }

        // Skill shot target dead centre of the orbit.
        Prim(parent, "Skillshot", new Vector3(TableCenterX, 0.30f, HalfZ - 0.62f),
            new Vector3(0.9f, 0.25f, 0.9f), null, PrimitiveType.Cylinder);
    }

    // ---------------------------------------------------------------- wireform ramp

    static void BuildWireformRamp(Transform parent)
    {
        // The reference's signature chrome rail: a left-hand loop that climbs from mid-table over
        // the orbit and drops back on the right.
        var pts = new[]
        {
            new Vector3(-2.95f, 0.55f, 1.20f),
            new Vector3(-2.60f, 1.35f, 2.90f),
            new Vector3(-1.70f, 2.05f, 4.35f),
            new Vector3(-0.30f, 2.45f, 5.30f),
            new Vector3( 1.30f, 2.45f, 5.35f),
            new Vector3( 2.55f, 2.00f, 4.55f),
            new Vector3( 3.05f, 1.25f, 3.05f),
            new Vector3( 3.05f, 0.60f, 1.40f),
        };

        for (int i = 0; i < pts.Length - 1; i++)
            Tube(parent, $"Wireform_{i:00}", pts[i], pts[i + 1], 0.055f);

        // Support posts so the rail does not read as floating.
        foreach (var (x, z, y) in new[] { (-2.85f, 1.2f, 0.35f), (-0.3f, 5.3f, 1.25f), (2.95f, 1.4f, 0.4f) })
            Prim(parent, $"Wireform_Post_{x:F1}_{z:F1}", new Vector3(x, y, z),
                new Vector3(0.09f, y, 0.09f), null, PrimitiveType.Cylinder);
    }

    // ---------------------------------------------------------------- playfield inserts

    static void BuildInserts(Transform parent)
    {
        // Glowing inlays, the reference's main source of colour. Grouped where the ball travels so
        // they also read as lane markers.
        var groups = new (float x, float z, float r)[][]
        {
            new[] { (0f, 1.30f, 0.40f), (-1.70f, 0.35f, 0.40f), (1.70f, 0.35f, 0.40f) },      // bumper apron
            new[] { (-2.75f, -1.05f, 0.30f), (2.75f, -1.05f, 0.30f) },                        // slingshot feed
            new[] { (-2.05f, 2.55f, 0.26f), (2.05f, 2.55f, 0.26f) },                          // target inlane
            new[] { (0f, 4.55f, 0.30f) },                                                     // orbit mouth
            new[] { (0f, -2.65f, 0.34f) },                                                    // center drain warning
        };

        int n = 0;
        for (int g = 0; g < groups.Length; g++)
        {
            for (int i = 0; i < groups[g].Length; i++)
            {
                var (x, z, r) = groups[g][i];
                Prim(parent, $"Insert_{g}_{i}", new Vector3(x, PlayY + 0.008f, z),
                    new Vector3(r * 2f, 0.006f, r * 2f), null, PrimitiveType.Cylinder);
                n++;
            }
        }

        // The 5-white-arrow rollover lane the reference puts under the flipper feed.
        for (int i = 0; i < 3; i++)
            Prim(parent, $"Rollover_{i}", new Vector3(-0.8f + i * 0.8f, PlayY + 0.006f, -6.35f),
                new Vector3(0.34f, 0.012f, 0.62f));
    }

    // ---------------------------------------------------------------- glass & lighting

    static void BuildGlass(Transform parent)
    {
        // Catalog milestone item: the glass sheet over the playfield.
        Prim(parent, "Playfield_Glass", new Vector3(TableCenterX, WallH + 0.05f, -0.4f),
            new Vector3(HalfX * 2f + LaneClear + WallT * 2f - 0.15f, 0.04f, HalfZ * 2f + WallT * 2f - 1.4f));
    }

    static void BuildLighting(Transform parent)
    {
        // Coloured floods over the playfield: the reference's glow comes from lit inserts plus
        // overhead light, which URP gets from point lights at low intensity.
        var spots = new (float x, float z, Color c, float intensity)[]
        {
            (-1.60f,  1.35f, new Color(1.00f, 0.45f, 0.45f), 1.0f),
            ( 1.60f,  1.35f, new Color(0.50f, 0.80f, 1.00f), 1.0f),
            ( 0.00f,  3.30f, new Color(1.00f, 0.82f, 0.40f), 0.9f),
            ( 0.00f, -2.60f, new Color(0.55f, 1.00f, 0.70f), 0.9f),
            ( 0.00f, -5.20f, new Color(0.80f, 0.60f, 1.00f), 0.8f),
        };

        for (int i = 0; i < spots.Length; i++)
        {
            var (x, z, c, intensity) = spots[i];
            var go = FindOrCreate($"Light_{i}", parent);
            go.transform.localPosition = new Vector3(x, 2.4f, z);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var l = go.GetComponent<Light>();
            if (l == null) l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = 5.5f;
            l.shadows = LightShadows.None;
            Built.Add(go.name);
        }
    }

    // ---------------------------------------------------------------- backbox

    static void BuildBackbox(Transform parent)
    {
        const float backZ = HalfZ + 1.35f;

        Prim(parent, "Backbox_Body", new Vector3(TableCenterX, 2.45f, backZ),
            new Vector3(HalfX * 2f + LaneClear + WallT * 2f, 4.6f, 1.1f));

        Prim(parent, "Backbox_Glass", new Vector3(TableCenterX, 2.60f, backZ - 0.6f),
            new Vector3(HalfX * 2f + WallT * 2f, 3.7f, 0.04f));

        // School identity: a display ledge behind the top wall carrying the campus model, so the IUT
        // reads as the machine's signage. It must sit clear of the play area or it fouls the ball.
        Prim(parent, "Backbox_Ledge", new Vector3(TableCenterX, 0.40f, backZ - 0.50f),
            new Vector3(HalfX * 2f + LaneClear, 0.34f, 0.95f));

        Prim(parent, "Backbox_Identity", new Vector3(TableCenterX, 2.60f, backZ - 0.68f),
            new Vector3(5.2f, 3.2f, 0.03f));

        var campusGo = FindOrCreate("Backbox_IUT", parent);
        var campus = InstantiateCampus(campusGo);
        campus.localPosition = new Vector3(TableCenterX, 0.60f, backZ - 0.50f);
        campus.localRotation = Quaternion.identity;
        campus.localScale = Vector3.one * 0.10f;
        Built.Add(campusGo.name);

        // Marquee bar over the top of the backbox.
        Box(parent, "Backbox_Marquee", new Vector3(TableCenterX, 5.0f, backZ - 0.2f),
            new Vector3(HalfX * 2f + WallT * 2f, 0.5f, 0.9f));
    }

    // ---------------------------------------------------------------- HUD

    static void LayoutHud()
    {
        // The HUD was authored as centre-anchored boxes with hand-picked offsets, which drift off
        // screen as soon as the aspect changes. Anchor everything to the screen corners instead.
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogWarning("[BuildPinballTable] Canvas not found; HUD left as is"); return; }

        var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
        }

        // name, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta, fontSize, align
        //
        // The boxes stretch over half the screen each instead of using fixed pixel widths. With the
        // old 420px boxes the score and ball counters overlapped in the middle as soon as the game
        // view was wider than it was tall (637x471 is the default here).
        var layout = new (string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, float font, TextAlignmentOptions align)[]
        {
            ("ScoreText",     new Vector2(0f, 1f),   new Vector2(0.5f, 1f), new Vector2(0f, 1f),   new Vector2(26f, -22f),  new Vector2(-46f, 62f), 36f, TextAlignmentOptions.MidlineLeft),
            ("HighScoreText", new Vector2(0f, 1f),   new Vector2(0.5f, 1f), new Vector2(0f, 1f),   new Vector2(26f, -86f),  new Vector2(-46f, 46f), 26f, TextAlignmentOptions.MidlineLeft),
            ("BallsText",     new Vector2(0.5f, 1f), new Vector2(1f, 1f),   new Vector2(1f, 1f),   new Vector2(-26f, -22f), new Vector2(-46f, 62f), 36f, TextAlignmentOptions.MidlineRight),
            ("MissionText",   new Vector2(0.5f, 1f), new Vector2(1f, 1f),   new Vector2(1f, 1f),   new Vector2(-26f, -86f), new Vector2(-46f, 46f), 20f, TextAlignmentOptions.MidlineRight),
            ("MessageText",   new Vector2(0f, 0f),   new Vector2(1f, 0f),   new Vector2(0.5f, 0f), new Vector2(0f, 34f),    new Vector2(-60f, 64f), 34f, TextAlignmentOptions.Center),
        };

        foreach (var item in layout)
        {
            var go = GameObject.Find(item.name);
            if (go == null) { Debug.LogWarning($"[BuildPinballTable] HUD text '{item.name}' not found"); continue; }

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = item.aMin;
                rt.anchorMax = item.aMax;
                rt.pivot = item.pivot;
                rt.anchoredPosition = item.pos;
                rt.sizeDelta = item.size;
            }

            var text = go.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.alignment = item.align;
                text.fontSize = item.font;
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
            EditorUtility.SetDirty(go);
        }
    }

    // ---------------------------------------------------------------- gameplay hosts

    static void PlaceGameplayParts()
    {
        // Bumpers: the reference's thumper cluster, above the slingshots.
        Move("Bumper_01", new Vector3(-0.05f, 0.62f, 0.55f), Vector3.one);
        Move("Bumper_02", new Vector3(-1.70f, 0.62f, 1.35f), Vector3.one);
        Move("Bumper_03", new Vector3(1.60f, 0.62f, 1.35f), Vector3.one);
        foreach (var b in new[] { "Bumper_01", "Bumper_02", "Bumper_03" })
            NormaliseChild(b, 0.42f, new Vector3(0f, -0.14f, 0f));

        // Subject bank: five drop targets in a row under the orbit.
        string[] bank = { "Target_Programmation", "Target_Reseau", "Target_Web", "Target_BDD", "Target_Projet" };
        for (int i = 0; i < bank.Length; i++)
        {
            float x = -2.20f + i * 1.10f;
            Move(bank[i], new Vector3(x, 0.50f, 3.15f), Vector3.one);
            // Normalise the Krea children: they were scaled independently before the bank existed.
            NormaliseChild(bank[i], 0.62f, new Vector3(0f, -0.12f, 0.06f));
        }

        Move("Slingshot_Left", new Vector3(-2.90f, 0.50f, -2.50f), Vector3.one);
        Move("Slingshot_Right", new Vector3(2.90f, 0.50f, -2.50f), Vector3.one);
        foreach (var s in new[] { "Slingshot_Left", "Slingshot_Right" })
            NormaliseChild(s, 0.45f, new Vector3(0f, -0.16f, 0f));

        Move("Flipper_Left_Pivot", new Vector3(-FlipperX, 0.60f, FlipperZ), Vector3.one);
        Move("Flipper_Right_Pivot", new Vector3(FlipperX, 0.60f, FlipperZ), Vector3.one);

        // Plunger lane, right side, outside the playfield proper.
        Move("Plunger", new Vector3(LaneCentreX, 0.46f, -6.30f), Vector3.one);

        // Decorative Krea parts whose legacy hosts were stripped of geometry: keep the visual,
        // place it sensibly on the rebuilt table.
        var rampHost = GameObject.Find("Ramp_Center_PB");
        if (rampHost != null)
        {
            rampHost.transform.localPosition = new Vector3(TableCenterX, 0.26f, 2.70f);
            rampHost.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        }

        var laneHost = GameObject.Find("LaneDivider");
        if (laneHost != null)
        {
            laneHost.transform.localPosition = new Vector3(LaneInnerX, 0.50f, -3.60f);
            laneHost.transform.localRotation = Quaternion.identity;
        }

        var spawn = FindOrCreate("BallSpawnPoint", null);
        // On the raised lane floor, not on the playfield: the ball must start clear of the slab.
        spawn.transform.localPosition = new Vector3(LaneCentreX, LaneFloorTop + BallRadius + 0.01f, -5.55f);
        spawn.transform.localRotation = Quaternion.identity;

        var drain = GameObject.Find("DrainZone");
        if (drain != null)
        {
            drain.transform.localPosition = new Vector3(0f, -0.55f, -(HalfZ - 0.35f));
            drain.transform.localScale = new Vector3(HalfX * 1.6f, 2f, 1f);
        }

        // Camera: portrait framing like the reference, centred on the whole assembly.
        var cam = Object.FindAnyObjectByType<Camera>();
        if (cam != null)
        {
            var t = cam.transform;
            t.position = new Vector3(TableCenterX, CamLift, CamBack);
            t.rotation = Quaternion.Euler(CamPitch, 0f, 0f);
            cam.fieldOfView = CamFov;
        }
    }

    static void Move(string name, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.Find(name);
        if (go == null) { Debug.LogWarning($"[BuildPinballTable] missing host '{name}'"); return; }
        go.transform.localPosition = pos;
        if (scale != Vector3.one) go.transform.localScale = scale;
    }

    static void NormaliseChild(string hostName, float uniformScale, Vector3 localOffset)
    {
        var host = GameObject.Find(hostName);
        if (host == null) return;
        foreach (Transform c in host.transform)
        {
            if (!c.name.StartsWith("Krea")) continue;
            c.localPosition = localOffset;
            c.localRotation = Quaternion.identity;
            c.localScale = Vector3.one * uniformScale;
        }
    }

    // ---------------------------------------------------------------- materials

    static void MaterialiseAll()
    {
        // ASSET_CATALOG wiring: wood for the cabinet, metal for chrome, ice for glass-like pieces,
        // and Snow015 for the snow *accents* (it is snow-over-grass, so it only reads as snow in
        // narrow strips — not as a full playfield surface).
        var mat = new Dictionary<string, Material>
        {
            ["field"] = LoadOrCreateSolid("PlayfieldBlue_Mat", new Color(0.10f, 0.24f, 0.34f), 0.05f, 0.55f),
            ["art"] = LoadOrCreateSolid("PlayfieldTeal_Mat", new Color(0.13f, 0.32f, 0.42f), 0.05f, 0.60f),
            ["center"] = LoadOrCreateSolid("PlayfieldDeep_Mat", new Color(0.07f, 0.18f, 0.30f), 0.05f, 0.65f),
            ["snow"] = LoadOrCreate("Snow_Mat", "Assets/Textures/CC0/Snow015/Snow015_1K-JPG_Color.jpg",
                                    "Assets/Textures/CC0/Snow015/Snow015_1K-JPG_NormalGL.jpg",
                                    new Color(0.72f, 0.82f, 1.00f), 0.02f, 0.35f, new Vector2(4f, 6f)),
            ["ice"] = LoadOrCreate("Ice_Mat", "Assets/Textures/CC0/Ice003/Ice003_1K-JPG_Color.jpg",
                                   "Assets/Textures/CC0/Ice003/Ice003_1K-JPG_NormalGL.jpg",
                                   new Color(0.62f, 0.82f, 1.00f), 0.05f, 0.92f, new Vector2(2f, 2f)),
            ["wood"] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TableWood_Mat.mat"),
            ["metal"] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Metal_Mat.mat"),
            ["glass"] = LoadOrCreateGlass("PlayfieldGlass_Mat", new Color(0.70f, 0.86f, 0.96f, 0.06f)),
        };

        foreach (var name in Built)
        {
            var go = GameObject.Find(name);
            if (go == null) continue;
            var r = go.GetComponent<Renderer>();
            if (r == null) continue;

            Material m;
            if (name.StartsWith("Playfield_Glass")) m = mat["glass"];
            else if (name.StartsWith("Snow_")) m = mat["snow"];
            else if (name.StartsWith("Playfield_Surface")) m = mat["field"];
            else if (name.StartsWith("Playfield_Art")) m = mat["art"];
            else if (name.StartsWith("Playfield_Center")) m = mat["center"];
            else if (name.StartsWith("Backbox_Identity") || name.StartsWith("Backbox_Glass")) m = mat["ice"];
            else if (name.StartsWith("Cab_") || name.StartsWith("Apron") || name.StartsWith("Backbox")) m = mat["wood"];
            else if (name.StartsWith("Insert_") || name.StartsWith("Rollover")) m = mat["ice"];
            else m = mat["metal"];

            r.sharedMaterial = m;
        }

        // Glowing inserts get their own emissive colours so the table reads as lit.
        for (int g = 0; g < 5; g++)
        {
            var emissive = LoadOrCreateEmissive($"Insert_{g}_Mat", InsertColour(g));
            for (int i = 0; i < 8; i++)
            {
                var go = GameObject.Find($"Insert_{g}_{i}");
                var r = go?.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = emissive;
            }
        }
        var rollover = LoadOrCreateEmissive("Rollover_Mat", new Color(1f, 0.86f, 0.35f));
        for (int i = 0; i < 3; i++)
        {
            var go = GameObject.Find($"Rollover_{i}");
            var r = go?.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = rollover;
        }
    }

    static Color InsertColour(int g) => g switch
    {
        0 => new Color(1.00f, 0.30f, 0.30f),
        1 => new Color(0.35f, 0.75f, 1.00f),
        2 => new Color(1.00f, 0.62f, 0.20f),
        3 => new Color(0.55f, 1.00f, 0.55f),
        _ => new Color(0.75f, 0.55f, 1.00f),
    };

    // ---------------------------------------------------------------- helpers

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Vector3? euler = null)
        => Prim(parent, name, pos, size, euler, PrimitiveType.Cube);

    /// <summary>Creates or reuses a named primitive with a mesh, renderer and collider.</summary>
    static GameObject Prim(Transform parent, string name, Vector3 pos, Vector3 size,
                           Vector3? euler = null, PrimitiveType kind = PrimitiveType.Cube)
    {
        var go = FindOrCreate(name, parent);
        go.transform.localPosition = pos;
        go.transform.localRotation = euler.HasValue ? Quaternion.Euler(euler.Value) : Quaternion.identity;
        go.transform.localScale = size;
        EnsureMesh(go, kind);
        Built.Add(name);
        return go;
    }

    /// <summary>Creates a tube segment running from <paramref name="a"/> to <paramref name="b"/>.</summary>
    static GameObject Tube(Transform parent, string name, Vector3 a, Vector3 b, float radius)
    {
        var go = FindOrCreate(name, parent);
        var dir = b - a;
        go.transform.localPosition = (a + b) / 2f;
        // Unity's cylinder primitive is 2 units tall along Y, so align Y with the segment.
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        go.transform.localScale = new Vector3(radius * 2f, dir.magnitude / 2f, radius * 2f);
        EnsureMesh(go, PrimitiveType.Cylinder);
        Built.Add(name);
        return go;
    }

    static void EnsureMesh(GameObject go, PrimitiveType kind)
    {
        var mf = go.GetComponent<MeshFilter>();
        if (mf == null) mf = go.AddComponent<MeshFilter>();
        if (mf.sharedMesh == null)
        {
            var temp = GameObject.CreatePrimitive(kind);
            mf.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
        }
        if (go.GetComponent<MeshRenderer>() == null) go.AddComponent<MeshRenderer>();
        if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
    }

    /// <summary>Attaches the school campus model to the Backbox_IUT holder as a fresh instance.</summary>
    static Transform InstantiateCampus(GameObject holder)
    {
        // Drop any previous instance so the rebuild is idempotent.
        for (int i = holder.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(holder.transform.GetChild(i).gameObject);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/sprites/IUT.obj");
        if (prefab == null)
        {
            Debug.LogWarning("[BuildPinballTable] Assets/sprites/IUT.obj not found; backglass identity left empty");
            return holder.transform;
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
        inst.name = "IUT_Campus";
        return inst.transform;
    }

    static GameObject FindOrCreate(string name, Transform parent)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            if (parent != null && existing.transform.parent != parent)
                existing.transform.SetParent(parent, false);
            return existing;
        }
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static void ClearGenerated(Transform container)
    {
        // Only wipe the objects this script owns; gameplay hosts and Krea parts live elsewhere.
        var doomed = new List<GameObject>();
        foreach (Transform c in container) doomed.Add(c.gameObject);
        foreach (var go in doomed) Object.DestroyImmediate(go);
    }

    static string Side(int s) => s < 0 ? "L" : "R";

    static Material LoadOrCreateSolid(string matName, Color colour, float metallic, float smoothness)
    {
        string path = $"Assets/Materials/{matName}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        var m = existing ?? new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = matName };

        m.SetColor("_BaseColor", colour);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);

        if (existing == null) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static Material LoadOrCreateGlass(string matName, Color tint)
    {
        string path = $"Assets/Materials/{matName}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        var m = existing ?? new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = matName };

        m.SetFloat("_Surface", 1f);   // transparent
        m.SetFloat("_Blend", 0f);     // alpha blend
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Metallic", 0.0f);
        m.SetFloat("_Smoothness", 0.5f);

        if (existing == null) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static Material LoadOrCreate(string matName, string albedo, string normal, Color tint, float metallic, float smooth, Vector2 tiling)
    {
        string path = $"Assets/Materials/{matName}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) { Configure(existing, albedo, normal, tint, metallic, smooth, tiling); return existing; }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(shader) { name = matName };
        Configure(m, albedo, normal, tint, metallic, smooth, tiling);
        AssetDatabase.CreateAsset(m, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static void Configure(Material m, string albedo, string normal, Color tint, float metallic, float smooth, Vector2 tiling)
    {
        var a = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
        if (a != null) { m.SetTexture("_BaseMap", a); m.SetTextureScale("_BaseMap", tiling); }
        if (n != null)
        {
            EnsureNormalMap(n);
            m.SetTexture("_BumpMap", n);
            m.SetTextureScale("_BumpMap", tiling);
            m.EnableKeyword("_NORMALMAP");
        }
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
    }

    static void EnsureNormalMap(Texture2D tex)
    {
        var path = AssetDatabase.GetAssetPath(tex);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        if (imp.textureType != TextureImporterType.NormalMap)
        {
            imp.textureType = TextureImporterType.NormalMap;
            imp.SaveAndReimport();
        }
    }

    static Material LoadOrCreateEmissive(string matName, Color c)
    {
        string path = $"Assets/Materials/{matName}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = matName };
        m.SetColor("_BaseColor", c);
        m.SetColor("_EmissionColor", c * 2.6f);
        m.EnableKeyword("_EMISSION");
        m.SetFloat("_Smoothness", 0.8f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        AssetDatabase.CreateAsset(m, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }
}
