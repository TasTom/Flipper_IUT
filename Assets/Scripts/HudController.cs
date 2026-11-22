using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affichage de la partie (GDD §Interface).
///
/// Le HUD ne décide de rien : il s'abonne à <see cref="ScoreManager"/> et
/// <see cref="GameManager"/> et se contente de refléter leur état. Séparé d'eux parce que le
/// GDD §Architecture interdit un script central, et parce que l'affichage doit pouvoir
/// disparaître (build sans UI, test de physique) sans casser les règles du jeu.
/// </summary>
public class HudController : MonoBehaviour
{
    public static HudController Instance { get; private set; }

    [Header("Textes (TMP)")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text highScoreText;
    [SerializeField] private TMP_Text ballsText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text multiplierText;
    [SerializeField] private TMP_Text scoreFeedbackText;
    [SerializeField] private bool compactMultiplierLabel;

    [Header("Jauge de puissance du lanceur")]
    [SerializeField] private Image plungerGaugeFill;
    [SerializeField] private TMP_Text plungerGaugeLabel;
    [SerializeField] private GameObject plungerGaugeRoot;
    [SerializeField] private bool showGaugeOnlyWhenCharged;

    [Header("Découverte automatique")]
    [Tooltip("Remplit les champs vides en cherchant les enfants nommés ScoreText, " +
             "HighScoreText, BallsText et MessageText.")]
    [SerializeField] private bool autoFindTexts = true;
    [SerializeField] private bool autoFindPlungerGauge = true;

    [Header("Message temporaire")]
    [SerializeField] private float defaultMessageDuration = 2f;

    private Coroutine messageRoutine;
    private Plunger[] plungers = System.Array.Empty<Plunger>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[HudController] Un second HUD existe sur '{name}' : il est ignoré.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (autoFindTexts)
        {
            ResolveMissingTexts();
        }

        if (autoFindPlungerGauge)
        {
            ResolveMissingPlungerGauge();
        }

        RefreshPlungers();

        WarnAboutMissingTexts();
    }

    private void RefreshPlungers()
    {
        plungers = FindObjectsByType<Plunger>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if ((plungers == null || plungers.Length == 0) && Plunger.Instance != null)
        {
            plungers = new Plunger[] { Plunger.Instance };
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void LateUpdate()
    {
        if ((plungers == null || plungers.Length == 0) && Plunger.Instance != null)
        {
            Subscribe();
        }
    }

    private void Subscribe()
    {
        Unsubscribe();
        RefreshPlungers();
        for (int i = 0; i < plungers.Length; i++)
        {
            if (plungers[i] != null)
            {
                plungers[i].ChargeChanged -= OnPlungerChargeChanged;
                plungers[i].ChargeChanged += OnPlungerChargeChanged;
            }
        }
        if (Plunger.Instance != null && System.Array.IndexOf(plungers, Plunger.Instance) < 0)
        {
            Plunger.Instance.ChargeChanged -= OnPlungerChargeChanged;
            Plunger.Instance.ChargeChanged += OnPlungerChargeChanged;
        }

        if (plungers.Length > 0 && plungers[0] != null)
        {
            OnPlungerChargeChanged(plungers[0].Charge);
        }
        else if (Plunger.Instance != null)
        {
            OnPlungerChargeChanged(Plunger.Instance.Charge);
        }
        else
        {
            OnPlungerChargeChanged(0f);
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ScoreChanged += OnScoreChanged;
            ScoreManager.Instance.HighScoreChanged += OnHighScoreChanged;
            ScoreManager.Instance.MultiplierChanged += OnMultiplierChanged;
            ScoreManager.Instance.ComboChanged += OnComboChanged;
            ScoreManager.Instance.BallBonusAwarded += OnBallBonus;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.MessageChanged += ShowMessage;
            GameManager.Instance.BallsChanged += OnBallsChanged;
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        for (int i = 0; i < plungers.Length; i++)
        {
            if (plungers[i] != null)
            {
                plungers[i].ChargeChanged -= OnPlungerChargeChanged;
            }
        }
        if (Plunger.Instance != null)
        {
            Plunger.Instance.ChargeChanged -= OnPlungerChargeChanged;
        }
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ScoreChanged -= OnScoreChanged;
            ScoreManager.Instance.HighScoreChanged -= OnHighScoreChanged;
            ScoreManager.Instance.MultiplierChanged -= OnMultiplierChanged;
            ScoreManager.Instance.ComboChanged -= OnComboChanged;
            ScoreManager.Instance.BallBonusAwarded -= OnBallBonus;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.MessageChanged -= ShowMessage;
            GameManager.Instance.BallsChanged -= OnBallsChanged;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        // Reprendre les abonnements si le HUD s'est éveillé avant les gestionnaires.
        Subscribe();
        // Les gestionnaires ont fini leur Awake : on récupère l'état initial plutôt que
        // d'attendre le premier événement, qui n'arrivera qu'au premier point marqué.
        if (ScoreManager.Instance != null)
        {
            OnScoreChanged(ScoreManager.Instance.Score);
            OnHighScoreChanged(ScoreManager.Instance.HighScore);
            OnMultiplierChanged(ScoreManager.Instance.Multiplier);
            OnComboChanged(ScoreManager.Instance.ComboLevel, 0);
        }

        if (GameManager.Instance != null)
        {
            OnBallsChanged(GameManager.Instance.BallsRemaining);
        }
    }

    /// <summary>Affiche un message, puis efface si <paramref name="duration"/> est positif.</summary>
    public void ShowMessage(string value)
    {
        ShowMessage(value, defaultMessageDuration);
    }

    /// <summary>Affiche un message pendant une durée donnée.</summary>
    public void ShowMessage(string value, float duration)
    {
        if (messageText == null)
        {
            return;
        }

        messageText.text = value;

        if (messageRoutine != null)
        {
            StopCoroutine(messageRoutine);
            messageRoutine = null;
        }

        if (duration > 0f && isActiveAndEnabled)
        {
            messageRoutine = StartCoroutine(ClearMessageAfter(duration));
        }
    }

    private IEnumerator ClearMessageAfter(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (messageText != null)
        {
            messageText.text = string.Empty;
        }

        messageRoutine = null;
    }

    private void OnScoreChanged(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = $"SCORE : {score:N0}";
        }
    }

    private void OnHighScoreChanged(int highScore)
    {
        if (highScoreText != null)
        {
            highScoreText.text = $"RECORD : {highScore:N0}";
        }
    }

    private void OnBallsChanged(int balls)
    {
        if (ballsText != null)
        {
            ballsText.text = $"BILLES : {balls}";
        }
    }

    private void OnMultiplierChanged(int multiplier)
    {
        if (multiplierText != null) multiplierText.text = compactMultiplierLabel ? $"×{multiplier}" : $"MULTIPLICATEUR : ×{multiplier}";
    }

    private void OnComboChanged(int level, int awarded)
    {
        if (scoreFeedbackText == null) return;
        if (level > 0)
            scoreFeedbackText.text = (level >= 4 ? "SUPER COMBO" : $"COMBO ×{level}")
                                   + (awarded > 0 ? $"  +{awarded:N0}" : string.Empty);
        else if (ScoreManager.Instance != null && (ScoreManager.Instance.LastBallBonus.Points > 0
                 || ScoreManager.Instance.LastBallBonus.Forfeited))
            OnBallBonus(ScoreManager.Instance.LastBallBonus);
        else scoreFeedbackText.text = string.Empty;
    }

    private void OnBallBonus(ScoreManager.BallBonus bonus)
    {
        if (scoreFeedbackText != null)
            scoreFeedbackText.text = bonus.Forfeited ? "BONUS ANNULÉ — TILT" : $"BONUS BILLE  +{bonus.Points:N0}";
    }

    /// <summary>
    /// Cherche les textes par nom d'objet. Évite <c>GameObject.Find</c>, qui parcourt toute la
    /// scène, et permet au script de build de générer le HUD avec les noms attendus.
    /// </summary>
    private void ResolveMissingTexts()
    {
        TMP_Text[] candidates = GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text candidate in candidates)
        {
            string n = candidate.gameObject.name;

            if (scoreText == null && n == "ScoreText")
            {
                scoreText = candidate;
            }
            else if (highScoreText == null && n == "HighScoreText")
            {
                highScoreText = candidate;
            }
            else if (ballsText == null && n == "BallsText")
            {
                ballsText = candidate;
            }
            else if (messageText == null && n == "MessageText")
            {
                messageText = candidate;
            }
            else if (multiplierText == null && n == "MultiplierText") multiplierText = candidate;
            else if (scoreFeedbackText == null && n == "ScoreFeedbackText") scoreFeedbackText = candidate;
        }
    }

    private static Sprite whiteSprite;
    private static Sprite GetOrCreateWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            whiteSprite.name = "PlungerGauge_WhiteSprite";
        }
        return whiteSprite;
    }

    private void EnsureValidFillSprite(Image image)
    {
        if (image == null) return;
        if (image.sprite == null)
        {
            image.sprite = GetOrCreateWhiteSprite();
        }
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    private void ResolveMissingPlungerGauge()
    {
        if (plungerGaugeFill == null)
        {
            Image[] candidates = GetComponentsInChildren<Image>(true);
            foreach (Image candidate in candidates)
            {
                if (candidate.gameObject.name == "PlungerGaugeFill")
                {
                    plungerGaugeFill = candidate;
                    break;
                }
            }
        }

        if (plungerGaugeFill == null)
        {
            CreateRuntimePlungerGauge();
        }

        if (plungerGaugeFill != null)
        {
            EnsureValidFillSprite(plungerGaugeFill);
            if (plungerGaugeRoot == null && plungerGaugeFill.transform.parent != null)
            {
                plungerGaugeRoot = plungerGaugeFill.transform.parent.gameObject;
            }
        }

        if (plungerGaugeLabel == null)
        {
            TMP_Text[] textCandidates = GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text candidate in textCandidates)
            {
                if (candidate.gameObject.name == "PlungerGaugeLabel")
                {
                    plungerGaugeLabel = candidate;
                    break;
                }
            }
        }
    }

    private void CreateRuntimePlungerGauge()
    {
        Transform existingBg = transform.Find("PlungerGaugeBackground");
        GameObject backgroundGo;
        if (existingBg != null)
        {
            backgroundGo = existingBg.gameObject;
        }
        else
        {
            backgroundGo = new GameObject("PlungerGaugeBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundGo.transform.SetParent(transform, false);

            RectTransform bgRect = backgroundGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0f);
            bgRect.anchorMax = new Vector2(0.5f, 0f);
            bgRect.pivot = new Vector2(0.5f, 0f);
            bgRect.anchoredPosition = new Vector2(0f, 40f);
            bgRect.sizeDelta = new Vector2(500f, 26f);

            Image bgImage = backgroundGo.GetComponent<Image>();
            bgImage.sprite = GetOrCreateWhiteSprite();
            bgImage.color = new Color(0.08f, 0.10f, 0.14f, 0.90f);
            bgImage.raycastTarget = false;
        }

        plungerGaugeRoot = backgroundGo;

        Transform existingLabel = backgroundGo.transform.Find("PlungerGaugeLabel");
        if (existingLabel != null)
        {
            plungerGaugeLabel = existingLabel.GetComponent<TMP_Text>();
        }
        else
        {
            GameObject labelGo = new GameObject("PlungerGaugeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(backgroundGo.transform, false);

            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 4f);
            labelRect.sizeDelta = new Vector2(0f, 24f);

            TextMeshProUGUI labelText = labelGo.GetComponent<TextMeshProUGUI>();
            labelText.fontSize = 18f;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.text = "LANCEUR [ESPACE]";
            labelText.color = new Color(0.9f, 0.9f, 0.9f, 0.95f);
            labelText.raycastTarget = false;
            plungerGaugeLabel = labelText;
        }

        Transform existingFill = backgroundGo.transform.Find("PlungerGaugeFill");
        if (existingFill != null)
        {
            plungerGaugeFill = existingFill.GetComponent<Image>();
        }
        else
        {
            GameObject fillGo = new GameObject("PlungerGaugeFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(backgroundGo.transform, false);

            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            Image fillImage = fillGo.GetComponent<Image>();
            fillImage.sprite = GetOrCreateWhiteSprite();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            fillImage.color = Color.green;
            fillImage.raycastTarget = false;
            plungerGaugeFill = fillImage;
        }
    }

    private void OnPlungerChargeChanged(float charge)
    {
        if (plungerGaugeFill == null)
        {
            ResolveMissingPlungerGauge();
            if (plungerGaugeFill == null)
            {
                return;
            }
        }

        float clamped = Mathf.Clamp01(charge);
        plungerGaugeFill.fillAmount = clamped;

        // Arcade gradient: Green -> Yellow -> Orange -> Red
        Color fillColor;
        if (clamped < 0.5f)
        {
            fillColor = Color.Lerp(new Color(0.2f, 0.95f, 0.2f), new Color(1f, 0.9f, 0.1f), clamped * 2f);
        }
        else
        {
            fillColor = Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(1f, 0.15f, 0.15f), (clamped - 0.5f) * 2f);
        }
        plungerGaugeFill.color = fillColor;

        if (plungerGaugeLabel != null)
        {
            if (clamped > 0.01f)
            {
                plungerGaugeLabel.text = $"LANCEUR : {Mathf.RoundToInt(clamped * 100f)}%";
            }
            else
            {
                plungerGaugeLabel.text = "LANCEUR [ESPACE]";
            }
        }

        if (showGaugeOnlyWhenCharged && plungerGaugeRoot != null)
        {
            bool shouldBeVisible = clamped > 0.005f;
            if (plungerGaugeRoot.activeSelf != shouldBeVisible)
            {
                plungerGaugeRoot.SetActive(shouldBeVisible);
            }
        }
    }

    /// <summary>
    /// Un champ vide n'est pas une erreur : c'est le cas normal d'une scène sans HUD.
    /// On prévient une seule fois pour que le silence ne soit pas pris pour un bug.
    /// </summary>
    private void WarnAboutMissingTexts()
    {
        if (scoreText != null && highScoreText != null && ballsText != null && messageText != null)
        {
            return;
        }

        Debug.Log("[HudController] Certains textes du HUD ne sont pas assignés : " +
                  "le jeu fonctionne, mais l'information correspondante restera invisible. " +
                  "Vérifie ScoreText / HighScoreText / BallsText / MessageText sous le Canvas.", this);
    }
}
