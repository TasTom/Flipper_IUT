using System.Collections;
using TMPro;
using UnityEngine;

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

    [Header("Découverte automatique")]
    [Tooltip("Remplit les champs vides en cherchant les enfants nommés ScoreText, " +
             "HighScoreText, BallsText et MessageText.")]
    [SerializeField] private bool autoFindTexts = true;

    [Header("Message temporaire")]
    [SerializeField] private float defaultMessageDuration = 2f;

    private Coroutine messageRoutine;

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

        WarnAboutMissingTexts();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Subscribe()
    {
        Unsubscribe();

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ScoreChanged += OnScoreChanged;
            ScoreManager.Instance.HighScoreChanged += OnHighScoreChanged;
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
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ScoreChanged -= OnScoreChanged;
            ScoreManager.Instance.HighScoreChanged -= OnHighScoreChanged;
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
        // Les gestionnaires ont fini leur Awake : on récupère l'état initial plutôt que
        // d'attendre le premier événement, qui n'arrivera qu'au premier point marqué.
        Subscribe();

        if (ScoreManager.Instance != null)
        {
            OnScoreChanged(ScoreManager.Instance.Score);
            OnHighScoreChanged(ScoreManager.Instance.HighScore);
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
