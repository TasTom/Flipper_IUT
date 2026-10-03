using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Transition de scène : voile, animation, chargement.
///
/// Vit hors des scènes (<see cref="Object.DontDestroyOnLoad"/>) et se construit son propre
/// <see cref="Canvas"/> : la scène de départ n'a rien à préparer, et celle d'arrivée n'a rien à
/// recevoir. Un seul survivant à la fois — un second appel pendant une transition est ignoré,
/// sans quoi deux chargements concurrents se marcheraient dessus.
///
/// <para>L'animation s'appuie sur <c>Time.unscaledDeltaTime</c>, pas sur le temps de jeu : la
/// partie est mise en pause avant l'appel, et une animation en temps mis à l'échelle resterait
/// figée à la première image — le joueur verrait un écran noir sans fin.</para>
/// </summary>
[DisallowMultipleComponent]
public class SceneTransition : MonoBehaviour
{
    /// <summary>Profondeur d'affichage : au-dessus du HUD, qui vit à 100 par défaut.</summary>
    private const int OverlaySortingOrder = 32000;

    private static SceneTransition _instance;

    [Header("Durées (secondes, temps réel)")]
    [Tooltip("Montée du voile et apparition du titre.")]
    [SerializeField] private float fadeInDuration = 0.8f;

    [Tooltip("Temps de tenue du carton, avant le chargement.")]
    [SerializeField] private float holdDuration = 1.1f;

    [Tooltip("Descente du voile dans la scène d'arrivée.")]
    [SerializeField] private float fadeOutDuration = 0.6f;

    [Header("Habillage")]
    [Tooltip("Fond du voile : acier sombre de l'atelier.")]
    [SerializeField] private Color backgroundColor = new Color(0.043f, 0.055f, 0.071f, 1f);

    [Tooltip("Couleur du bandeau qui balaie l'écran : orange de métal en fusion.")]
    [SerializeField] private Color accentColor = new Color(1f, 0.451f, 0.086f, 1f);

    [Tooltip("Éclat de la seconde station : blanc de neige vosgienne.")]
    [SerializeField] private Color secondaryColor = new Color(0.82f, 0.89f, 0.96f, 1f);

    /// <summary>Vrai tant qu'une transition est en cours.</summary>
    public static bool IsPlaying { get; private set; }

    /// <summary>
    /// Tenue du carton pour la prochaine transition. Négatif : on garde la valeur réglée dans
    /// l'Inspector.
    ///
    /// <para>C'est une donnée statique, et non un appel sur <see cref="Instance"/>, pour que le
    /// réglage du jeu n'oblige pas à créer l'habillage à l'avance : la transition ne doit exister
    /// qu'au moment où elle sert.</para>
    /// </summary>
    public static float NextHoldDuration { get; set; } = -1f;

    /// <summary>
    /// Instance courante, créée à la demande. Les scènes n'ont pas à en poser une : le premier
    /// appelant suffit.
    /// </summary>
    public static SceneTransition Instance {
        get {
            if (_instance == null)
            {
                var go = new GameObject(nameof(SceneTransition));
                _instance = go.AddComponent<SceneTransition>();
            }

            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[SceneTransition] Un second exemplaire existe sur '{name}' : il est ignoré.", this);
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            IsPlaying = false;
        }
    }

    /// <summary>
    /// Lance la transition puis charge <paramref name="sceneName"/>.
    ///
    /// <para>Refusé si une transition est déjà en cours, si la scène est vide, ou si elle n'est
    /// pas dans les Build Settings — <see cref="SceneManager.LoadScene(string)"/> lèverait alors
    /// une exception au milieu de l'animation, laissant le joueur sur un écran de chargement
    /// éternel. Mieux vaut ne rien faire et le dire.</para>
    /// </summary>
    public static bool Play(string sceneName, string title = null, string subtitle = null)
    {
        if (IsPlaying)
        {
            Debug.LogWarning("[SceneTransition] Une transition est déjà en cours : demande ignorée.", null);
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[SceneTransition] Aucune scène cible : transition annulée.", null);
            return false;
        }

        if (!IsSceneInBuild(sceneName))
        {
            Debug.LogError(
                $"[SceneTransition] La scène '{sceneName}' n'est pas dans les Build Settings " +
                "(File > Build Profiles). Transition annulée : la charger lèverait une exception " +
                "au milieu de l'animation.", null);
            return false;
        }

        Instance.StartCoroutine(Instance.Run(sceneName, title, subtitle));
        return true;
    }

    /// <summary>
    /// La scène est-elle dans les Build Settings ?
    ///
    /// <para>Passe par <see cref="SceneManager.sceneCountInBuildSettings"/> et
    /// <see cref="SceneUtility.GetScenePathByBuildIndex"/> plutôt que par
    /// <c>Application.CanStreamedLevelBeLoaded</c> : mesuré dans l'éditeur, cette dernière
    /// répond <c>false</c> pour <b>toutes</b> les scènes, y compris celles présentes au build
    /// depuis toujours. S'y fier ferait refuser chaque transition, en silence et sans recours.</para>
    /// </summary>
    private static bool IsSceneInBuild(string sceneName)
    {
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(i);
            if (!string.IsNullOrEmpty(path) &&
                System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator Run(string sceneName, string title, string subtitle)
    {
        IsPlaying = true;

        var overlay = BuildOverlay(title, subtitle, out var veil, out var bar, out var stationA,
                                   out var stationB, out var stationC);

        // Largeur du canevas, relevée une fois : le bandeau doit traverser tout l'écran, et un
        // canevas en ScreenSpaceOverlay n'a pas encore de dimensions au premier passage.
        var overlayRect = veil != null ? veil.rectTransform.parent as RectTransform : null;
        float travelWidth = overlayRect != null && overlayRect.rect.width > 0f
            ? overlayRect.rect.width
            : Screen.width;

        // Le temps de jeu reste figé pendant toute la transition : l'animation n'utilise que du
        // temps réel, et la partie doit rester immobile derrière le voile. La restaurer ici
        // ferait rouler la bille jusqu'au drain pendant que le joueur ne voit plus la table.

        // 1. Le voile monte et le titre apparaît.
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInDuration);
            float eased = t * t * (3f - 2f * t);

            SetAlpha(veil, eased);
            SetAlpha(bar, eased);
            SetAlpha(stationA, Mathf.Clamp01((eased - 0.15f) / 0.35f));
            SetAlpha(stationB, Mathf.Clamp01((eased - 0.35f) / 0.35f));
            SetAlpha(stationC, Mathf.Clamp01((eased - 0.55f) / 0.35f));

            // Le bandeau traverse l'écran : c'est le trait d'union visuel entre les trois pôles.
            if (bar != null)
            {
                var rt = bar.rectTransform;
                rt.anchoredPosition = new Vector2(Mathf.Lerp(-travelWidth, travelWidth, eased), rt.anchoredPosition.y);
            }

            yield return null;
        }

        // 2. Tenue du carton.
        float hold = NextHoldDuration >= 0f ? NextHoldDuration : holdDuration;
        NextHoldDuration = -1f;
        yield return new WaitForSecondsRealtime(hold);

        // 3. Chargement. L'objet survit : c'est lui qui fait descendre le voile ensuite.
        var load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (load == null)
        {
            Debug.LogError($"[SceneTransition] Chargement de '{sceneName}' refusé par Unity. " +
                           "Le voile est retiré et la partie reprend.", this);
            Time.timeScale = 1f;
            IsPlaying = false;
            Destroy(overlay.gameObject);
            yield break;
        }

        while (!load.isDone)
        {
            yield return null;
        }

        // La nouvelle scène démarre en temps réel : ni la pause de l'ancienne, ni un reste de
        // temps mis à l'échelle ne doivent la suivre.
        Time.timeScale = 1f;

        // 4. Le voile descend sur la scène d'arrivée.
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            float remaining = 1f - t;
            SetAlpha(veil, remaining);
            SetAlpha(bar, remaining);
            SetAlpha(stationA, remaining);
            SetAlpha(stationB, remaining);
            SetAlpha(stationC, remaining);
            yield return null;
        }

        Destroy(overlay.gameObject);
        IsPlaying = false;
    }

    /// <summary>
    /// Construit l'habillage de la transition.
    ///
    /// Trois stations s'allument l'une après l'autre : l'industrie, la montagne, l'IUT. C'est le
    /// thème annoncé, et cela occupe l'œil pendant le chargement sans masquer la barre de
    /// progression — il n'y en a pas, et une fausse serait pire que pas de barre du tout.
    /// </summary>
    private Canvas BuildOverlay(string title, string subtitle, out Image veil, out Image bar,
                               out Image stationA, out Image stationB, out Image stationC)
    {
        var canvasGo = new GameObject("TransitionOverlay", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlaySortingOrder;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var rect = canvasGo.GetComponent<RectTransform>();

        veil = NewImage("Veil", rect, backgroundColor);
        Stretch(veil.rectTransform);

        bar = NewImage("MeltBar", rect, accentColor);
        var barRect = bar.rectTransform;
        barRect.anchorMin = new Vector2(0f, 0.5f);
        barRect.anchorMax = new Vector2(0f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.sizeDelta = new Vector2(520f, 6f);
        barRect.anchoredPosition = Vector2.zero;

        stationA = NewStation(rect, new Vector2(0f, 92f), accentColor, "INDUSTRIES");
        stationB = NewStation(rect, new Vector2(0f, 0f), secondaryColor, "MONTAGNE");
        stationC = NewStation(rect, new Vector2(0f, -92f), accentColor, "IUT");

        AddText(rect, title, new Vector2(0f, 232f), 62f, FontStyles.Bold, 1.6f);
        AddText(rect, subtitle, new Vector2(0f, -232f), 30f, FontStyles.Normal, 2.4f);

        return canvas;
    }

    private static Image NewStation(RectTransform parent, Vector2 position, Color color, string label)
    {
        var image = NewImage("Station_" + label, parent, color);
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(360f, 3f);
        rect.anchoredPosition = position;

        AddText(parent, label, position + new Vector2(0f, 34f), 34f, FontStyles.Bold, 1.8f);
        return image;
    }

    private static Image NewImage(string name, RectTransform parent, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        go.transform.SetParent(parent, false);
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Ajoute un libellé. Passe par la police par défaut de TextMeshPro ; si le projet n'en a
    /// pas, le texte est simplement absent — l'animation, elle, reste.
    /// </summary>
    private static void AddText(RectTransform parent, string content, Vector2 position, float size,
                                FontStyles style, float characterSpacing)
    {
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        var font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            Debug.LogWarning("[SceneTransition] Aucune police TextMeshPro par défaut : les textes " +
                             "de la transition sont omis.");
            return;
        }

        var go = new GameObject("Label", typeof(TextMeshProUGUI));
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.characterSpacing = characterSpacing;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1400f, size * 2f);
        rect.anchoredPosition = position;

        go.transform.SetParent(parent, false);
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
        {
            return;
        }

        var color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
