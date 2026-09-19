using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// État de la partie (GDD §Architecture) : cycle de jeu, billes restantes, fin de partie.
///
/// Ce composant ne connaît ni le score (<see cref="ScoreManager"/>), ni la physique des billes
/// (<see cref="BallManager"/>), ni l'affichage (<see cref="HudController"/>). Le GDD interdit
/// un script central unique : chacun de ces rôles vit dans son propre fichier et communique
/// par événements.
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>Étapes du cycle d'une partie.</summary>
    public enum GameState
    {
        /// <summary>Avant la première partie : la table est inerte.</summary>
        Attract,

        /// <summary>Bille en jeu, lanceur disponible.</summary>
        ReadyToLaunch,

        /// <summary>Bille lancée, partie en cours.</summary>
        Playing,

        /// <summary>Bille perdue, attente de la bille suivante.</summary>
        BallDrained,

        /// <summary>Plus de billes : écran de fin.</summary>
        GameOver,

        /// <summary>Partie suspendue (Échap).</summary>
        Paused,
    }

    public static GameManager Instance { get; private set; }

    [Header("Partie")]
    [SerializeField] private int startingBalls = 3;

    [Tooltip("Lance une partie automatiquement au démarrage de la scène.")]
    [SerializeField] private bool autoStartOnPlay = true;

    [Tooltip("Délai avant la bille suivante, en secondes.")]
    [SerializeField] private float respawnDelay = 1.2f;

    [Header("Bille")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform ballSpawnPoint;

    [Header("Touches de repli")]
    [Tooltip("Utilisées seulement si la scène n'a pas d'InputRouter.")]
    [SerializeField] private KeyCode restartKey = KeyCode.Return;
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

    /// <summary>Émis à chaque changement d'état.</summary>
    public event Action<GameState> StateChanged;

    /// <summary>Émis avec le nombre de billes restantes.</summary>
    public event Action<int> BallsChanged;

    /// <summary>Émis avec un message et sa durée d'affichage (0 : permanent).</summary>
    public event Action<string, float> MessageChanged;

    /// <summary>État courant de la partie.</summary>
    public GameState State { get; private set; } = GameState.Attract;

    /// <summary>Billes restantes, bille en jeu comprise.</summary>
    public int BallsRemaining { get; private set; }

    /// <summary>Point d'apparition des billes, utilisé aussi par l'anti-blocage.</summary>
    public Transform BallSpawnPoint => ballSpawnPoint;

    private Coroutine respawnRoutine;
    private float timeScaleBeforePause = 1f;
    private GameState stateBeforePause = GameState.Playing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[GameManager] Un second gestionnaire existe sur '{name}' : il est ignoré.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (BallManager.Instance != null)
        {
            BallManager.Instance.BallDrained += OnBallDrained;
        }
    }

    private void OnDisable()
    {
        if (BallManager.Instance != null)
        {
            BallManager.Instance.BallDrained -= OnBallDrained;
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
        // BallManager a un DefaultExecutionOrder plus bas : son Awake/OnEnable a déjà eu lieu,
        // mais on se réabonne ici par sécurité si l'ordre change.
        if (BallManager.Instance != null)
        {
            BallManager.Instance.BallDrained -= OnBallDrained;
            BallManager.Instance.BallDrained += OnBallDrained;
        }

        if (autoStartOnPlay)
        {
            if (DifficultyManager.Instance != null)
            {
                DifficultyManager.Instance.BeginSelection();
            }
            else
            {
                StartGame();
            }
        }
        else
        {
            SetState(GameState.Attract);
            Broadcast("APPUIE SUR ENTRÉE", 0f);
        }
    }

    private void Update()
    {
        if (ValidatePressed() && (State == GameState.GameOver || State == GameState.Attract))
        {
            if (DifficultyManager.Instance != null)
            {
                DifficultyManager.Instance.BeginSelection();
            }
            else
            {
                StartGame();
            }

            return;
        }

        if (PausePressed())
        {
            TogglePause();
        }
    }

    /// <summary>Démarre une nouvelle partie : score à zéro, billes rechargées.</summary>
    public void StartGame()
    {
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }

        if (Time.timeScale == 0f)
        {
            TogglePause();
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetScore();
        }

        if (BallManager.Instance != null)
        {
            BallManager.Instance.ClearAll();
        }

        BallsRemaining = startingBalls;
        BallsChanged?.Invoke(BallsRemaining);

        SpawnBall();
        Broadcast("BONNE PARTIE !", 2f);
    }

    public void BeginDifficultySelection()
    {
        SetState(GameState.Attract);
        Broadcast("CHOISISSEZ LA DIFFICULTE : 1 FACILE | 2 NORMAL | 3 DIFFICILE", 0f);
    }

    /// <summary>
    /// Point d'entrée historique du score, conservé pour <see cref="MissionManager"/>,
    /// <see cref="MultiballManager"/> et les cibles. Le score vit dans <see cref="ScoreManager"/>.
    /// </summary>
    public void AddScore(int points)
    {
        if (State == GameState.GameOver || ScoreManager.Instance == null)
        {
            return;
        }

        ScoreManager.Instance.Add(points);
    }

    /// <summary>Ajoute un nombre exact de points sans appliquer le multiplicateur.</summary>
    public void AddExactScore(int points)
    {
        if (State == GameState.GameOver || ScoreManager.Instance == null)
        {
            return;
        }

        ScoreManager.Instance.AddExact(points);
    }

    /// <summary>Appelé par le lanceur quand la bille part réellement.</summary>
    public void NotifyBallLaunched()
    {
        if (State == GameState.ReadyToLaunch)
        {
            SetState(GameState.Playing);
            Broadcast(string.Empty, 0.1f);
        }
    }

    /// <summary>
    /// Appelé par <see cref="BallManager"/> quand il ramène dans le couloir une bille qu'il
    /// n'a pas réussi à débloquer.
    ///
    /// Sans ce retour d'état, la partie resterait en <see cref="GameState.Playing"/> alors que
    /// la bille est immobile dans le couloir — un état pourtant normal à cet endroit. L'anti-
    /// blocage de <see cref="BallManager"/> la relancerait alors indéfiniment, toutes les
    /// <c>stuckDelay</c> secondes, et la partie n'avancerait plus jamais.
    /// </summary>
    public void NotifyBallReturnedToLane()
    {
        // Seule une partie en cours peut revenir en attente de lancement. Dans les autres
        // états, une bille immobile relève d'autre chose : on ne touche pas au cycle de jeu.
        if (State != GameState.Playing)
        {
            return;
        }

        SetState(GameState.ReadyToLaunch);
        Broadcast("BILLE RÉCUPÉRÉE", 2f);
    }

    /// <summary>
    /// Signale la perte d'une bille. Appelé par <see cref="DrainZone"/> quand la scène n'a pas
    /// de <see cref="BallManager"/> ; sinon c'est l'événement de ce dernier qui déclenche.
    /// </summary>
    public void LoseBall()
    {
        HandleBallLoss();
    }

    private void OnBallDrained(Rigidbody ball)
    {
        HandleBallLoss();
    }

    private void HandleBallLoss()
    {
        if (State == GameState.GameOver || State == GameState.Attract)
        {
            return;
        }

        // En multiball, la bille n'est réellement perdue que lorsque la dernière est tombée.
        if (BallManager.Instance != null && BallManager.Instance.LiveBallCount > 0)
        {
            return;
        }

        BallsRemaining--;
        BallsChanged?.Invoke(BallsRemaining);

        if (BallsRemaining > 0)
        {
            SetState(GameState.BallDrained);
            Broadcast("BILLE PERDUE", respawnDelay);

            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
            }

            respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }
        else
        {
            EndGame();
        }
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        respawnRoutine = null;

        if (State != GameState.GameOver)
        {
            SpawnBall();
        }
    }

    private void EndGame()
    {
        SetState(GameState.GameOver);

        bool isRecord = ScoreManager.Instance != null && ScoreManager.Instance.CommitHighScore();

        Broadcast(isRecord ? "NOUVEAU RECORD ! — ENTRÉE POUR REJOUER"
                           : "GAME OVER — ENTRÉE POUR REJOUER", 0f);
    }

    private void SpawnBall()
    {
        if (ballSpawnPoint == null)
        {
            Debug.LogError("[GameManager] Aucun point d'apparition de bille : la partie ne peut pas " +
                           "démarrer. Assigne 'Ball Spawn Point' dans l'Inspector.", this);
            return;
        }

        if (ballPrefab == null)
        {
            Debug.LogError("[GameManager] Aucun prefab de bille assigné : la partie ne peut pas démarrer.", this);
            return;
        }

        if (BallManager.Instance != null)
        {
            BallManager.Instance.SpawnBall(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);
        }
        else
        {
            // Repli sans BallManager : la bille existe mais échappe au plafond de vitesse
            // et à l'anti-blocage. On le signale plutôt que de le taire.
            Debug.LogWarning("[GameManager] Aucun BallManager dans la scène : la bille est créée " +
                             "sans suivi (ni plafond de vitesse, ni anti-blocage).", this);
            Instantiate(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);
        }

        SetState(GameState.ReadyToLaunch);
    }

    /// <summary>Suspend ou reprend la partie (Échap, GDD §Contrôles).</summary>
    public void TogglePause()
    {
        if (State == GameState.Paused)
        {
            Time.timeScale = timeScaleBeforePause;

            // On restaure l'état d'avant la pause, et non `Playing` en dur : mis en pause
            // pendant l'attente du lancement, on doit y revenir. Sinon la partie se
            // retrouverait en `Playing` avec une bille immobile dans le couloir, et
            // l'anti-blocage la relancerait sans fin.
            SetState(stateBeforePause);
            Broadcast(string.Empty, 0.1f);
        }
        else if (State != GameState.GameOver && State != GameState.Attract)
        {
            stateBeforePause = State;
            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            SetState(GameState.Paused);
            Broadcast("PAUSE — ÉCHAP POUR REPRENDRE", 0f);
        }
    }

    private void SetState(GameState next)
    {
        if (State == next)
        {
            return;
        }

        State = next;
        StateChanged?.Invoke(State);
    }

    private void Broadcast(string message, float duration)
    {
        MessageChanged?.Invoke(message, duration);
    }

    private bool ValidatePressed()
    {
        if (InputRouter.Instance != null)
        {
            return InputRouter.Instance.ValidatePressed;
        }

        return Input.GetKeyDown(restartKey);
    }

    private bool PausePressed()
    {
        if (InputRouter.Instance != null)
        {
            return InputRouter.Instance.PausePressed;
        }

        return Input.GetKeyDown(pauseKey);
    }

    private void OnApplicationQuit()
    {
        // Une pause laissée active fausserait la session suivante dans l'éditeur.
        Time.timeScale = 1f;
    }
}
