using UnityEngine;

public sealed class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    [Header("Mode")]
    [SerializeField] private DifficultyMode startingMode = DifficultyMode.Normal;
    [SerializeField] private DifficultyConfig easyConfig;
    [SerializeField] private DifficultyConfig normalConfig;
    [SerializeField] private DifficultyConfig hardConfig;

    public DifficultyMode CurrentMode { get; private set; }
    public DifficultyConfig CurrentConfig { get; private set; }
    public bool IsSelecting { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ApplyMode(startingMode);
    }

    private void Start()
    {
        ApplyMode(startingMode);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetDifficulty(DifficultyMode mode)
    {
        ApplyMode(mode);
    }

    public void BeginSelection()
    {
        IsSelecting = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.BeginDifficultySelection();
        }
    }

    public float TiltTolerance => CurrentConfig != null ? CurrentConfig.tiltTolerance : 0.5f;

    private void Update()
    {
        if (!IsSelecting)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectDifficulty(DifficultyMode.Easy);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectDifficulty(DifficultyMode.Normal);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectDifficulty(DifficultyMode.Hard);
        }
    }

    private void SelectDifficulty(DifficultyMode mode)
    {
        IsSelecting = false;
        ApplyMode(mode);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame();
        }
    }

    private void ApplyMode(DifficultyMode mode)
    {
        CurrentMode = mode;
        CurrentConfig = ResolveConfig(mode);

        if (CurrentConfig == null)
        {
            CurrentConfig = DifficultyConfig.CreateDefault(mode);
        }

        if (BallManager.Instance != null)
        {
            BallManager.Instance.ApplyDifficulty(CurrentConfig.ballMaxSpeed);
        }

        Bumper[] bumpers = FindObjectsByType<Bumper>(FindObjectsInactive.Include);
        foreach (Bumper bumper in bumpers)
        {
            bumper.ApplyDifficulty(CurrentConfig.bumperBounceForce);
        }

        Flipper[] flippers = FindObjectsByType<Flipper>(FindObjectsInactive.Include);
        foreach (Flipper flipper in flippers)
        {
            flipper.ApplyDifficulty(CurrentConfig.flipperBatScale);
        }

        DrainZone[] drains = FindObjectsByType<DrainZone>(FindObjectsInactive.Include);
        foreach (DrainZone drain in drains)
        {
            drain.ApplyDifficulty(CurrentConfig.drainWidthMultiplier,
                                  CurrentConfig.lateralRescueEnabled,
                                  CurrentConfig.lateralRescueForce);
        }
    }

    private DifficultyConfig ResolveConfig(DifficultyMode mode)
    {
        switch (mode)
        {
            case DifficultyMode.Easy:
                return easyConfig;
            case DifficultyMode.Hard:
                return hardConfig;
            default:
                return normalConfig;
        }
    }
}
