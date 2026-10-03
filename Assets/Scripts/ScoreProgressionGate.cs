using UnityEngine;

/// <summary>
/// Surveille le score et bascule sur la seconde table quand le seuil est atteint.
///
/// <para>Ce composant ne compte rien lui-même : il écoute <see cref="ScoreManager.ScoreChanged"/>.
/// Le score n'a qu'un seul point d'écriture dans le projet, et le dupliquer ici rendrait le
/// multiplicateur incohérent.</para>
///
/// <para>La porte se pose toute seule au démarrage si la scène n'en a pas : la scène de travail
/// change souvent, et oublier de la replacer après une refonte ferait silencieusement disparaître
/// la transition. Une porte posée à la main l'emporte — c'est elle qu'on règle dans l'Inspector.</para>
/// </summary>
[DisallowMultipleComponent]
public class ScoreProgressionGate : MonoBehaviour
{
    /// <summary>Config lue quand la scène n'en fournit pas.</summary>
    private const string DefaultConfigPath = "Progression/ProgressionConfig";

    [Header("Config")]
    [Tooltip("Règles de progression. Vide : la config de Resources est utilisée, sinon les " +
             "valeurs de repli ci-dessous.")]
    [SerializeField] private ProgressionConfig config;

    [Header("Repli sans config")]
    [SerializeField] private int scoreThreshold = 100000;
    [SerializeField] private string targetSceneName = "Industries";
    [SerializeField] private string title = "ATELIER DES VOSGES";
    [SerializeField] private string subtitle = "Ligne 2 — industries, montagne et IUT";
    [SerializeField] private bool pauseBeforeTransition = true;

    /// <summary>Le seuil a-t-il déjà été franchi ? Une seule bascule par scène chargée.</summary>
    private bool _fired;
    private ScoreManager _score;
    private bool _subscribed;
    private bool _thresholdPending;

    private void Awake()
    {
        if (config == null)
        {
            config = Resources.Load<ProgressionConfig>(DefaultConfigPath);
        }
    }

    private void OnEnable() => TrySubscribe();

    private void Start() => TrySubscribe();

    private void OnDisable()
    {
        if (_subscribed && _score != null)
        {
            _score.ScoreChanged -= OnScoreChanged;
        }

        _subscribed = false;
    }

    /// <summary>
    /// S'abonne au score.
    ///
    /// <para>Le gestionnaire peut ne pas exister au moment de <c>OnEnable</c> : l'ordre d'éveil
    /// n'est pas garanti, et ce composant peut être ajouté par le démarrage automatique. On
    /// réessaie donc jusqu'à ce que le score réponde, plutôt que de rester muet à jamais.</para>
    /// </summary>
    private void TrySubscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _score = ScoreManager.Instance;
        if (_score == null)
        {
            return;
        }

        _score.ScoreChanged += OnScoreChanged;
        _subscribed = true;
        OnScoreChanged(_score.Score);
    }

    private void Update()
    {
        if (!_subscribed)
        {
            TrySubscribe();
        }
    }

    private void OnScoreChanged(int score) => _thresholdPending = score >= Threshold;

    private void LateUpdate()
    {
        if (!_thresholdPending || _score == null) return;
        _thresholdPending = false;
        // Attendre la fin du contact/drain : les bonus, multiplicateurs et billes doivent
        // finir leur transaction avant la photographie de la partie.
        Check(_score.Score);
    }

    private void Check(int score)
    {
        if (_fired || score < Threshold || gameObject.scene.name != (config != null ? config.sourceSceneName : "Neutral"))
        {
            return;
        }

        if (!SceneTransition.CanLoad(TargetScene)) return;
        _fired = true;
        SyncHoldDuration();
        TableSessionTransfer.Capture(TargetScene);

        if (PauseBefore) PauseGame(); // Le voile fige également le temps, y compris après GameOver.

        if (SceneTransition.Play(TargetScene, Title, Subtitle, config))
        {
            Debug.Log($"[ScoreProgressionGate] Seuil de {Threshold} points atteint ({score}) : " +
                      $"passage à '{TargetScene}'.", this);
            return;
        }

        // La transition a été refusée : ne pas condamner la suite de la partie, le joueur
        // pourra retenter après avoir corrigé les Build Settings.
        _fired = false;
        TableSessionTransfer.Clear();
        if (GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.Paused)
            GameManager.Instance.TogglePause();
    }

    private bool PauseGame()
    {
        var manager = GameManager.Instance;
        if (manager == null)
        {
            return false;
        }

        if (manager.State == GameManager.GameState.Paused)
        {
            return true;
        }

        if (manager.State == GameManager.GameState.GameOver || manager.State == GameManager.GameState.Attract)
        {
            return false;
        }

        manager.TogglePause();
        return manager.State == GameManager.GameState.Paused;
    }

    private int Threshold => config != null ? Mathf.Max(1, config.scoreThreshold) : Mathf.Max(1, scoreThreshold);

    private string TargetScene => config != null ? config.targetSceneName : targetSceneName;

    private string Title => config != null ? config.title : title;

    private string Subtitle => config != null ? config.subtitle : subtitle;

    private bool PauseBefore => config != null ? config.pauseBeforeTransition : pauseBeforeTransition;

    /// <summary>
    /// Transmet la durée de tenue du carton à la prochaine transition.
    /// </summary>
    private void SyncHoldDuration()
    {
        if (config != null)
        {
            SceneTransition.NextHoldDuration = Mathf.Max(0f, config.holdDuration);
        }
    }

    /// <summary>
    /// Pose une porte dans la scène chargée si elle n'en a pas.
    ///
    /// <para>C'est le seul endroit du projet qui s'ajoute lui-même à une scène : la scène est le
    /// domaine de l'utilisateur, et y écrire depuis un script de jeu serait une surprise. Le
    /// compromis est de ne le faire qu'en l'absence de porte, sans jamais en remplacer une.</para>
    ///
    /// <para>La scène d'arrivée est écartée : elle porte le même nom que la cible, donc sans ce
    /// garde-fou elle s'installerait sa propre porte et renverrait le joueur vers elle-même en
    /// boucle dès le premier point marqué.</para>
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureGateExists()
    {
        if (FindAnyObjectByType<ScoreProgressionGate>() != null)
        {
            return;
        }

        var config = Resources.Load<ProgressionConfig>(DefaultConfigPath);
        if (config == null)
        {
            // Sans config, il n'y a pas de scène cible à nommer : on ne devine pas.
            Debug.Log("[ScoreProgressionGate] Aucune config de progression dans Resources/" +
                      DefaultConfigPath + " : la bascule vers la seconde table reste inactive.", null);
            return;
        }

        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene != config.sourceSceneName)
        {
            return;
        }

        var go = new GameObject(nameof(ScoreProgressionGate));
        var gate = go.AddComponent<ScoreProgressionGate>();
        gate.config = config;
        gate.SyncHoldDuration();
    }
}
