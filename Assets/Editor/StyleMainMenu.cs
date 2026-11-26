using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Habillage « Crépuscule » du menu principal. Le premier menu génère les polices TMP et les
/// formes arrondies sans écraser l'existant. Le second <b>réapplique</b> la mise en page du
/// menu (positions et couleurs comprises), annulable par Ctrl+Z, sans enregistrer la scène.
/// </summary>
public static class StyleMainMenu
{
    private const string FontFolder = "Assets/Resources/UI/Fonts";
    private const string SourceFolder = "Assets/Art/UI/Fonts";
    private const int SpriteSize = 96, SpriteRadius = 32, RingWidth = 6;
    private const float Width = 448f;

    [MenuItem("Flipper/UI/Générer les polices et les formes")]
    public static void BuildAssets()
    {
        Directory.CreateDirectory(FontFolder);
        AssetDatabase.Refresh();
        BuildFont("BigShouldersDisplay-ExtraBold", UiTheme.DisplayFont);
        BuildFont("BigShouldersDisplay-Medium", UiTheme.NumberFont);
        BuildFont("Barlow-Regular", UiTheme.BodyFont);
        BuildFont("Barlow-Medium", UiTheme.StrongFont);
        BuildSprite(UiTheme.RoundedSprite, 0);
        BuildSprite(UiTheme.RingSprite, RingWidth);
        AssetDatabase.SaveAssets();
    }

    private static void BuildFont(string source, string resource)
    {
        string path = "Assets/Resources/" + resource + ".asset";
        if (File.Exists(path)) return;
        var font = AssetDatabase.LoadAssetAtPath<Font>($"{SourceFolder}/{source}.ttf");
        if (font == null) throw new FileNotFoundException("Police introuvable (git lfs pull ?) : " + source);

        var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic);
        asset.name = Path.GetFileName(resource);
        AssetDatabase.CreateAsset(asset, path);
        asset.atlasTexture.name = asset.name + " Atlas";
        asset.material.name = asset.name + " Material";
        AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
        AssetDatabase.AddObjectToAsset(asset.material, asset);

        var characters = new System.Text.StringBuilder();
        for (char c = ' '; c <= 'ÿ'; c++)
            if (!char.IsControl(c)) characters.Append(c);
        characters.Append("Œœ’‘“”–—…• →←●");
        asset.TryAddCharacters(characters.ToString(), out _);
        // Statique : un atlas dynamique se réécrit en jeu et salit le dépôt à chaque partie.
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        var fallback = TMP_Settings.defaultFontAsset;
        if (fallback != null)
            asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };
        EditorUtility.SetDirty(asset);
    }

    private static void BuildSprite(string resource, int ring)
    {
        string path = "Assets/Resources/" + resource + ".png";
        if (File.Exists(path)) return;
        var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
        for (int y = 0; y < SpriteSize; y++)
            for (int x = 0; x < SpriteSize; x++)
            {
                // Distance signée au bord du rectangle arrondi (négative à l'intérieur).
                float half = SpriteSize / 2f;
                var q = new Vector2(Mathf.Abs(x + .5f - half), Mathf.Abs(y + .5f - half))
                        - Vector2.one * (half - SpriteRadius);
                float distance = Vector2.Max(q, Vector2.zero).magnitude
                                 + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - SpriteRadius;
                float alpha = Mathf.Clamp01(.5f - distance);
                if (ring > 0) alpha *= Mathf.Clamp01(.5f + distance + ring);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = Vector4.one * SpriteRadius;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    [MenuItem("Flipper/UI/Habiller le menu principal")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var panel = Find("Canvas/MenuPrincipal");
        if (scene.name != "MainMenu" || panel == null)
        {
            Debug.LogWarning("[StyleMainMenu] Ouvre d'abord la scène MainMenu.");
            return;
        }
        BuildAssets();
        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Habiller le menu principal");

        Place(panel, 0, 0, 560, 880);
        Round(panel.GetComponent<Image>(), new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .93f), 28);

        var title = Text(panel, "TitleText", "VOSGES\n<color=#EEBC6F>MANIA</color>", UiTheme.DisplayFont,
            136, UiTheme.Cream);
        Place(title.rectTransform, 0, 262, Width, 270);
        title.lineSpacing = -24;

        var rule = Child(panel, "Filet");
        Place(rule, -Width / 2 + 36, 116, 72, 6);
        Round(Ensure<Image>(rule), UiTheme.Gold, 3);

        StyleButton(panel, "Btn_Play", "JOUER", 32, 92, true);
        StyleButton(panel, "Btn_Scores", "Classement", -74, 70, false);
        StyleButton(panel, "Btn_Controls", "Options", -156, 70, false);
        StyleButton(panel, "Btn_Quit", "Quitter", -238, 70, false);

        var caption = Text(panel, "RecordLabel", "RECORD LOCAL", UiTheme.StrongFont, 20, UiTheme.Muted);
        Place(caption.rectTransform, 0, -324, Width, 28);
        caption.characterSpacing = 8;
        var record = Text(panel, "HighScoreText", "0", UiTheme.NumberFont, 62, UiTheme.Cream);
        Place(record.rectTransform, 0, -374, Width, 72);

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void StyleButton(RectTransform panel, string name, string label, float y, float height,
        bool primary)
    {
        var rect = panel.Find(name) as RectTransform;
        if (rect == null)
        {
            Debug.LogWarning($"[StyleMainMenu] Bouton '{name}' absent.");
            return;
        }
        Place(rect, 0, y, Width, height);
        var image = rect.GetComponent<Image>();
        Round(image, Color.white, 18);
        var button = rect.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = primary ? UiTheme.Gold : UiTheme.Surface;
        colors.highlightedColor = colors.selectedColor =
            primary ? (Color)new Color32(247, 206, 138, 255) : UiTheme.SurfaceFocus;
        colors.pressedColor = Color.Lerp(colors.normalColor, Color.black, .2f);
        colors.fadeDuration = .08f;
        button.colors = colors;

        var ink = primary ? UiTheme.Ink : UiTheme.Cream;
        var text = Text(rect, "Text (TMP)", label, primary ? UiTheme.DisplayFont : UiTheme.StrongFont,
            primary ? 52 : 30, ink);
        Stretch(text.rectTransform, 30);
        if (primary)
        {
            var arrow = Text(rect, "Fleche", "→", UiTheme.StrongFont, 40, ink);
            Stretch(arrow.rectTransform, 30);
            arrow.alignment = TextAlignmentOptions.MidlineRight;
        }

        var ringRect = Child(rect, "Selection");
        Stretch(ringRect, -7);
        ringRect.offsetMin = new Vector2(-7, -7);
        ringRect.offsetMax = new Vector2(7, 7);
        var ring = Ensure<Image>(ringRect);
        Round(ring, primary ? UiTheme.Cream : UiTheme.Gold, 24);
        ring.sprite = UiTheme.Sprite(UiTheme.RingSprite);
        ring.raycastTarget = false;
        ring.enabled = false;
        var focus = Ensure<UiFocusRing>(rect);
        var serialized = new SerializedObject(focus);
        serialized.FindProperty("ring").objectReferenceValue = ring;
        serialized.ApplyModifiedProperties();
    }

    private static TMP_Text Text(RectTransform parent, string name, string content, string font, float size,
        Color color)
    {
        var text = Ensure<TextMeshProUGUI>(Child(parent, name));
        text.font = UiTheme.Font(font);
        text.fontSharedMaterial = text.font.material;
        text.text = content;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.fontStyle = FontStyles.Normal;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.margin = Vector4.zero;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    internal static RectTransform Child(RectTransform parent, string name)
    {
        var child = parent.Find(name) as RectTransform;
        if (child != null) return child;
        var created = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(created, name);
        created.layer = parent.gameObject.layer;
        created.transform.SetParent(parent, false);
        return (RectTransform)created.transform;
    }

    internal static T Ensure<T>(Component host) where T : Component =>
        host.TryGetComponent<T>(out var found) ? found : Undo.AddComponent<T>(host.gameObject);

    private static void Round(Image image, Color color, float radius)
    {
        image.sprite = UiTheme.Sprite(UiTheme.RoundedSprite);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = SpriteRadius / radius;
        image.color = color;
    }

    internal static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, y);
    }

    private static void Stretch(RectTransform rect, float horizontalPadding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(horizontalPadding, 0);
        rect.offsetMax = new Vector2(-horizontalPadding, 0);
    }

    private static RectTransform Find(string path)
    {
        var parts = path.Split('/');
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == parts[0])
                return root.transform.Find(parts[1]) as RectTransform;
        return null;
    }
}
