using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Charte « Crépuscule » (GDD §Interface) : source unique des couleurs, polices et formes.
/// Les assets de <c>Resources/UI</c> sont générés par le menu <c>Flipper > UI</c>.
/// </summary>
public static class UiTheme
{
    public static readonly Color Ink = new Color32(17, 32, 36, 255);
    public static readonly Color Surface = new Color32(32, 54, 59, 255);
    public static readonly Color SurfaceFocus = new Color32(48, 77, 83, 255);
    public static readonly Color Gold = new Color32(238, 188, 111, 255);
    public static readonly Color Cream = new Color32(243, 235, 221, 255);
    public static readonly Color Muted = new Color32(171, 195, 195, 255);

    /// <summary>Titres et boutons principaux.</summary>
    public const string DisplayFont = "UI/Fonts/BigShoulders ExtraBold";
    /// <summary>Chiffres et intertitres.</summary>
    public const string NumberFont = "UI/Fonts/BigShoulders Medium";
    public const string BodyFont = "UI/Fonts/Barlow Regular";
    public const string StrongFont = "UI/Fonts/Barlow Medium";

    /// <summary>Rectangle blanc arrondi, découpé en 9 : se teinte par <c>Image.color</c>.</summary>
    public const string RoundedSprite = "UI/RoundedRect";
    /// <summary>Contour arrondi de même rayon, pour l'élément sélectionné.</summary>
    public const string RingSprite = "UI/RoundedRing";

    private const float SpriteRadius = 32f;

    /// <summary>Texte à la police voulue ; garde la police par défaut si l'asset manque.</summary>
    public static void Apply(TMP_Text text, string path)
    {
        var font = Font(path);
        if (font == null) return;
        text.font = font;
        text.fontSharedMaterial = font.material;
    }

    /// <summary>Fond uni à coins arrondis de rayon <paramref name="radius"/>.</summary>
    public static void Round(Image image, Color color, float radius)
    {
        image.sprite = Sprite(RoundedSprite);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = SpriteRadius / radius;
        image.color = color;
    }

    /// <summary>Couleurs d'état et contour de sélection d'un bouton construit en jeu.</summary>
    public static void StyleButton(Button button, bool primary)
    {
        Round((Image)button.targetGraphic, Color.white, 12);
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = primary ? Gold : Surface;
        colors.highlightedColor = colors.selectedColor =
            primary ? (Color)new Color32(247, 206, 138, 255) : SurfaceFocus;
        colors.pressedColor = Color.Lerp(colors.normalColor, Color.black, .2f);
        colors.disabledColor = new Color(Surface.r, Surface.g, Surface.b, .45f);
        colors.fadeDuration = 0f; // Un fondu partirait du blanc à la création du bouton.
        button.colors = colors;

        var child = button.transform.Find("Selection");
        var rect = child != null ? (RectTransform)child
            : new GameObject("Selection", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(button.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-4, -4);
        rect.offsetMax = new Vector2(4, 4);
        if (!rect.TryGetComponent<Image>(out var ring)) ring = rect.gameObject.AddComponent<Image>();
        Round(ring, primary ? Cream : Gold, 16);
        ring.sprite = Sprite(RingSprite);
        ring.raycastTarget = false;
        if (!button.TryGetComponent<UiFocusRing>(out var focus))
            focus = button.gameObject.AddComponent<UiFocusRing>();
        focus.Bind(ring);
    }

    public static TMP_FontAsset Font(string path) => Resources.Load<TMP_FontAsset>(path);
    public static Sprite Sprite(string path) => Resources.Load<Sprite>(path);
}
