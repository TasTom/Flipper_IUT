using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Affichage du FRONTON : l'écran 3D du caisson, face au joueur (GDD §Interface).
///
/// <para><b>Pourquoi un second affichage et pas une réutilisation du HUD.</b>
/// <see cref="HudController"/> est un Canvas en <i>écran</i> — il se superpose au rendu et reste
/// collé aux bords de la fenêtre. Le fronton, lui, est un objet <b>du monde</b> : il vit dans la
/// scène, à la place que la machine lui donne. Les deux montrent la même partie, mais par deux
/// techniques sans rapport. Le GDD §Architecture demande une responsabilité par fichier ; les
/// confondre obligerait chacun à connaître l'existence de l'autre.</para>
///
/// <para><b>Aucun singleton, à la différence de <see cref="HudController"/>.</b> Rien n'empêche
/// deux frontons de coexister (un par joueur, un écran de contrôle), et ce composant ne prend
/// aucune décision : il se contente d'écouter. Ses abonnements sont posés en
/// <see cref="OnEnable"/> et retirés en <see cref="OnDisable"/>, donc un objet désactivé cesse
/// proprement de suivre la partie.</para>
///
/// <para><b>Il ne lit jamais l'état au hasard.</b> Les gestionnaires peuvent être créés avant ou
/// après lui selon l'ordre d'éveil d'Unity : les champs vides sont donc résolus en
/// <see cref="Awake"/>, les abonnements en <see cref="OnEnable"/>, et l'état initial repris en
/// <see cref="Start"/> — quand tout le monde a fini de s'éveiller.</para>
/// </summary>
public class BackglassDisplay : MonoBehaviour
{
    [Header("Textes (TMP)")]
    [Tooltip("Le score de la partie en cours — le grand nombre au centre.")]
    [SerializeField] private TMP_Text scoreText;

    [Tooltip("Le record sauvegardé.")]
    [SerializeField] private TMP_Text highScoreText;

    [Tooltip("Billes restantes.")]
    [SerializeField] private TMP_Text ballsText;

    [Tooltip("Progression des matières.")]
    [SerializeField] private TMP_Text missionsText;

    [Tooltip("Message temporaire — Nuit de l'Info, perte de bille…")]
    [SerializeField] private TMP_Text messageText;

    [Header("Découverte automatique")]
    [Tooltip("Remplit les champs vides en cherchant les enfants portant ces noms exacts.")]
    [SerializeField] private bool autoFindTexts = true;

    [Header("Messages")]
    [SerializeField] private float messageDuration = 3f;

    private Coroutine messageRoutine;

    private void Awake()
    {
        if (autoFindTexts)
        {
            Resolve("ScoreValue", ref scoreText);
            Resolve("HighScoreValue", ref highScoreText);
            Resolve("BallsValue", ref ballsText);
            Resolve("MissionsValue", ref missionsText);
            Resolve("MessageValue", ref messageText);
        }

        if (scoreText == null && highScoreText == null && ballsText == null
            && missionsText == null && messageText == null)
        {
            Debug.LogWarning($"[BackglassDisplay] '{name}' n'a aucun texte à alimenter : " +
                             "l'écran restera vide. Vérifier les noms des enfants.", this);

            enabled = false;
        }
    }

    /// <summary>
    /// Abonne le fronton aux gestionnaires. Appelé depuis <see cref="OnEnable"/> ET depuis
    /// <see cref="Start"/>, et volontairement idempotent : les désabonnements d'abord, les
    /// abonnements ensuite, donc le rejouer ne double aucun gestionnaire.
    ///
    /// <para>⚠ Pourquoi deux fois — mesuré, et c'est le piège de ce composant. Le canvas du
    /// fronton est enfant de <c>PinballTable</c>, qui est <b>plus haut que <c>Managers/</c></b>
    /// dans la scène. Unity exécute les <c>Awake</c> dans l'ordre de la hiérarchie :
    /// <c>BackglassDisplay.OnEnable</c> passe donc <i>avant</i> l'<c>Awake</c> de
    /// <c>ScoreManager</c>, <c>GameManager</c> et <c>MissionManager</c>. À cet instant les trois
    /// <c>Instance</c> valent <c>null</c>, chaque <c>if</c> est sauté, et le fronton reste inerte
    /// <b>sans le moindre avertissement</b> — son <c>Start</c> lisait ensuite un
    /// <c>BallsRemaining</c> encore à 0 (« — ») et un score encore à 0, pendant que les
    /// gestionnaires affichaient 3 billes et 1 235 000 points.</para>
    ///
    /// <para>Au <c>Start</c>, tous les <c>Awake</c> sont passés : les <c>Instance</c> existent.
    /// Et comme le <c>Start</c> du fronton précède celui de <c>GameManager</c> (même ordre),
    /// l'abonnement est en place <i>avant</i> que <c>StartGame</c> ne diffuse ses premiers
    /// événements — on ne rate donc rien.</para>
    /// </summary>
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
            GameManager.Instance.BallsChanged += OnBallsChanged;
            GameManager.Instance.MessageChanged += OnGameMessage;
        }

        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.ProgressChanged += OnProgressChanged;
            MissionManager.Instance.Announced += OnAnnounced;
        }
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
            GameManager.Instance.BallsChanged -= OnBallsChanged;
            GameManager.Instance.MessageChanged -= OnGameMessage;
        }

        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.ProgressChanged -= OnProgressChanged;
            MissionManager.Instance.Announced -= OnAnnounced;
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        // Deuxième appel, le bon : tous les `Awake` sont passés, les `Instance` existent (voir
        // le commentaire de `Subscribe`). On en profite pour réafficher l'état courant — sinon
        // l'écran resterait vide jusqu'au premier point marqué, ce qui est long sur un fronton.
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

        if (MissionManager.Instance != null)
        {
            OnProgressChanged(MissionManager.Instance.CompletedCount,
                              MissionManager.Instance.SubjectCount);
        }
    }

    // --- les abonnés ------------------------------------------------------------------------

    private void OnScoreChanged(int score)
    {
        if (scoreText != null) { scoreText.text = score.ToString("N0"); }
    }

    private void OnHighScoreChanged(int highScore)
    {
        if (highScoreText != null) { highScoreText.text = highScore.ToString("N0"); }
    }

    private void OnBallsChanged(int balls)
    {
        if (ballsText == null) { return; }

        // Les billes se lisent d'un coup d'œil : des pastilles pleines plutôt qu'un chiffre.
        // C'est plus lisible à trois mètres, la distance réelle du fronton.
        var texte = new System.Text.StringBuilder();

        for (int i = 0; i < balls; i++) { texte.Append('●'); }

        ballsText.text = balls > 0 ? texte.ToString() : "—";
    }

    private void OnProgressChanged(int completed, int total)
    {
        if (missionsText != null) { missionsText.text = completed + " / " + total; }
    }

    /// <summary>Message du jeu, avec sa duree. `GameManager.MessageChanged` est un
    /// `Action<string, float>` : la duree vient de l'emetteur, on ne la choisit pas ici.
    ///
    /// ⚠ Dans `GameManager`, `duration = 0` veut dire PERMANENT, pas « duree non fournie ».
    /// `APPUIE SUR ENTRÉE`, `PAUSE — ÉCHAP POUR REPRENDRE` et `GAME OVER` sont tous diffuses
    /// avec `0f` et doivent rester a l ecran. Les confondre avec une duree absente ferait
    /// disparaitre un GAME OVER au bout de `messageDuration` secondes — le joueur ne saurait
    /// plus qu'il doit appuyer sur Entree.</summary>
    private void OnGameMessage(string value, float duration)
    {
        ShowMessage(value, duration > 0f ? duration : 0f);
    }

    /// <summary>Annonce de mission — `NUIT DE L'INFO` par exemple. Pas de duree fournie :
    /// on applique celle du fronton.</summary>
    private void OnAnnounced(string value)
    {
        ShowMessage(value, messageDuration);
    }

    /// <summary>Affiche un message sur le fronton pendant <paramref name="duration"/> secondes.
    /// Un appel pendant qu'un message est affiché remplace le précédent et relance le compte.</summary>
    public void ShowMessage(string value, float duration)
    {
        if (messageText == null) { return; }

        messageText.text = value;

        if (messageRoutine != null)
        {
            StopCoroutine(messageRoutine);
            messageRoutine = null;
        }

        if (duration > 0f && isActiveAndEnabled)
        {
            messageRoutine = StartCoroutine(ClearAfter(duration));
        }
    }

    private IEnumerator ClearAfter(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (messageText != null) { messageText.text = string.Empty; }

        messageRoutine = null;
    }

    /// <summary>Cherche un descendant par son nom et remplit le champ s'il est vide.
    /// <c>GetComponentsInChildren</c> plutôt que <c>GameObject.Find</c> : on reste dans le
    /// sous-arbre du fronton, sans parcourir toute la scène.</summary>
    private void Resolve(string nom, ref TMP_Text champ)
    {
        if (champ != null) { return; }

        foreach (var candidat in GetComponentsInChildren<TMP_Text>(true))
        {
            if (candidat.gameObject.name != nom) { continue; }

            champ = candidat;
            return;
        }
    }
}
