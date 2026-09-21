using UnityEngine;

/// <summary>
/// Applique un niveau de difficulté (GDD §Difficultés) aux systèmes de gameplay.
///
/// <para>Trois <see cref="DifficultyConfig"/> sont posés dans l'Inspector — Novice,
/// Standard, Expert — et le choix courant est poussé aux systèmes qui en dépendent :
/// <see cref="TiltController"/> (tolérance aux secousses), <see cref="BallManager"/>
/// (vitesse plafond) et <see cref="ExtraBallAward"/> (paliers de bille supplémentaire).
/// Aucun de ces scripts ne connaît la difficulté : c'est ce gestionnaire qui fait le
/// lien, ce qui garde une responsabilité par fichier.</para>
///
/// <para>L'application se fait au démarrage et à chaque changement de difficulté.
/// <see cref="Apply"/> est idempotent : relancé, il repousse les mêmes valeurs.</para>
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    /// <summary>Niveaux de difficulté du GDD.</summary>
    public enum Difficulte
    {
        Novice,
        Standard,
        Expert
    }

    public static DifficultyManager Instance { get; private set; }

    [Header("Configurations")]
    [Tooltip("Configuration du niveau Novice.")]
    [SerializeField] private DifficultyConfig novice;

    [Tooltip("Configuration du niveau Standard (défaut).")]
    [SerializeField] private DifficultyConfig standard;

    [Tooltip("Configuration du niveau Expert.")]
    [SerializeField] private DifficultyConfig expert;

    [Header("Sélection")]
    [Tooltip("Difficulté de départ. Changeable en cours de partie via SetDifficulte.")]
    [SerializeField] private Difficulte difficulte = Difficulte.Standard;

    /// <summary>Configuration active, après application.</summary>
    public DifficultyConfig Active { get; private set; }

    /// <summary>Niveau courant.</summary>
    public Difficulte CurrentDifficulte => difficulte;

    /// <summary>Émis à chaque application, avec la configuration retenue.</summary>
    public event System.Action<DifficultyConfig> DifficultyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[DifficultyManager] Un second gestionnaire existe sur '{name}' : il est ignoré.", this);
            enabled = false;
            return;
        }

        Instance = this;
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
        Apply();
    }

    /// <summary>Change de difficulté et l'applique immédiatement.</summary>
    public void SetDifficulte(Difficulte valeur)
    {
        difficulte = valeur;
        Apply();
    }

    /// <summary>
    /// Pousse les valeurs de la configuration active aux systèmes de gameplay.
    /// Idempotent.
    /// </summary>
    public void Apply()
    {
        Active = Selectionner();

        if (Active == null)
        {
            Debug.LogWarning("[DifficultyManager] Aucune configuration pour le niveau " +
                             $"'{difficulte}' : les valeurs courantes sont conservées.", this);
            return;
        }

        // Tilt : la tolérance aux secousses.
        if (TiltController.Instance != null)
        {
            TiltController.Instance.ConfigurerSecousses(
                Active.secoussesAvantAvertissement, Active.secoussesAvantTilt);
        }

        // Bille : la vitesse plafond.
        if (BallManager.Instance != null)
        {
            BallManager.Instance.SetMaxSpeed(Active.maxBallSpeed);
        }

        // Bille supplémentaire : les paliers.
        var extra = FindFirstObjectByType<ExtraBallAward>();
        if (extra != null)
        {
            extra.ConfigurerPaliers(Active.firstExtraBallThreshold,
                                    Active.extraBallThresholdStep,
                                    Active.maxExtraBalls);
        }

        DifficultyChanged?.Invoke(Active);
    }

    private DifficultyConfig Selectionner()
    {
        switch (difficulte)
        {
            case Difficulte.Novice: return novice != null ? novice : standard;
            case Difficulte.Expert: return expert != null ? expert : standard;
            default: return standard;
        }
    }
}
