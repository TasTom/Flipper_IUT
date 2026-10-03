using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Score de la partie et record local (GDD §Score).
///
/// Seul point d'écriture du score : les cibles, bumpers et missions passent tous par ici.
/// Le GDD interdit d'écrire le score ailleurs, sinon un multiplicateur ou un bonus de fin de
/// bille devient impossible à appliquer de façon cohérente.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    /// <summary>Clé historique : conservée pour ne pas perdre le record déjà enregistré.</summary>
    private const string HighScoreKey = "VosgesTilt_HighScore";

    [Header("Départ")]
    [SerializeField] private int startingScore;

    [Header("Multiplicateur")]
    [Tooltip("État courant. Une nouvelle partie repart à ×1.")]
    [SerializeField] private int multiplier = 1;

    [Tooltip("Règles de score avancées. Sans configuration, le barème historique des composants est conservé.")]
    [SerializeField] private ScoreConfig config;

    private readonly ComboTracker combo = new ComboTracker();
    private readonly HashSet<UnityEngine.Object> validatedCourses = new HashSet<UnityEngine.Object>();
    private bool ballActive;
    private int rampsThisBall;
    private int maximumComboThisBall;

    public struct BallBonus
    {
        public int Courses, Ramps, MaximumCombo, Points;
        public bool Forfeited;
    }

    public bool HasAdvancedRules => config != null;
    public int ComboLevel => Mathf.Max(0, combo.Actions - 1);
    public int MaximumComboThisBall => maximumComboThisBall;
    public int RampsThisBall => rampsThisBall;
    public int ValidatedCoursesThisBall => validatedCourses.Count;
    public BallBonus LastBallBonus { get; private set; }
    public event Action<int> MultiplierChanged;
    public event Action<int, int> ComboChanged;
    public event Action<BallBonus> BallBonusAwarded;

    /// <summary>Score courant de la partie.</summary>
    public int Score { get; private set; }

    /// <summary>Meilleur score enregistré sur cette machine.</summary>
    public int HighScore { get; private set; }

    /// <summary>Multiplicateur courant (1 par défaut).</summary>
    public int Multiplier => multiplier;

    /// <summary>Émis à chaque variation du score, avec la valeur courante.</summary>
    public event Action<int> ScoreChanged;

    /// <summary>Émis quand le record local est battu et enregistré.</summary>
    public event Action<int> HighScoreChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[ScoreManager] Un second gestionnaire existe sur '{name}' : il est ignoré.", this);
            enabled = false;
            return;
        }

        Instance = this;
        Score = startingScore;
        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        if (config == null)
            Debug.Log("[ScoreManager] ScoreConfig absent : score simple actif, combos et bonus de bille désactivés.", this);
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
        // Après les Awake : le HUD a eu le temps de s'abonner.
        ScoreChanged?.Invoke(Score);
        HighScoreChanged?.Invoke(HighScore);
        MultiplierChanged?.Invoke(multiplier);
    }

    /// <summary>Ajoute des points, pondérés par le multiplicateur courant.</summary>
    /// <param name="points">Points de base, avant multiplicateur.</param>
    public void Add(int points)
    {
        if (points == 0 || !CanAward())
        {
            return;
        }

        AddTotal((long)points * multiplier);
    }

    /// <summary>Remet le score à zéro pour une nouvelle partie. Le record est conservé.</summary>
    public void ResetScore()
    {
        Score = startingScore;
        multiplier = 1;
        ballActive = false;
        LastBallBonus = default;
        validatedCourses.Clear();
        rampsThisBall = maximumComboThisBall = 0;
        ClearCombo();
        ScoreChanged?.Invoke(Score);
        MultiplierChanged?.Invoke(multiplier);
    }

    /// <summary>Reprend une partie sur une autre table sans attribuer de nouveaux points.</summary>
    public void RestoreSession(int score, int restoredMultiplier)
    {
        ResetScore();
        Score = Mathf.Max(0, score);
        SetMultiplier(restoredMultiplier);
        ScoreChanged?.Invoke(Score);
    }

    /// <summary>Fixe le multiplicateur (GDD §Score, étape 2).</summary>
    public void SetMultiplier(int value)
    {
        int next = config != null ? Mathf.Clamp(value, 1, Mathf.Max(1, config.maximumMultiplier))
                                  : Mathf.Max(1, value);
        if (next == multiplier) return;
        multiplier = next;
        MultiplierChanged?.Invoke(multiplier);
    }

    public void RaiseMultiplierTo(int minimum) => SetMultiplier(Mathf.Max(multiplier, minimum));

    private bool CanAward()
    {
        if (TiltController.Instance != null && TiltController.Instance.IsTilted) return false;
        if (config != null && !ballActive) return false;
        if (GameManager.Instance == null) return true;
        var state = GameManager.Instance.State;
        return state == GameManager.GameState.Playing || state == GameManager.GameState.ReadyToLaunch;
    }

    private void AddTotal(long points)
    {
        Score = (int)Math.Max(0L, Math.Min(int.MaxValue, Score + points));
        ScoreChanged?.Invoke(Score);
    }

    /// <summary>Gros bonus lisible, sans multiplication supplémentaire.</summary>
    public void AddBonus(int points)
    {
        if (points > 0 && CanAward()) AddTotal(points);
    }

    /// <summary>Un élément a réellement marqué : source physique, barème et chaîne de combo.</summary>
    public void RecordHit(ScoreElement element, UnityEngine.Object source, int legacyPoints,
                          bool allowRepeat = false, bool courseValidated = false)
    {
        if (!CanAward()) return;
        Add(config != null ? config.Points(element) : legacyPoints);
        if (config == null) return;
        if (courseValidated && source != null) validatedCourses.Add(source);
        RegisterCombo(source, allowRepeat);
        if (element == ScoreElement.VosgesRamp || element == ScoreElement.IutRamp)
        {
            rampsThisBall++;
            if (rampsThisBall % Mathf.Max(1, config.rampsForIncrease) == 0)
                RaiseMultiplierTo(multiplier + 1);
        }
    }

    private void RegisterCombo(UnityEngine.Object source, bool repeat)
    {
        int previous = combo.Actions;
        bool advanced = combo.Register(source, repeat, Time.time, Mathf.Max(.1f, config.comboWindow));
        if (!advanced)
        {
            if (previous > 0 && combo.Actions == 0) ComboChanged?.Invoke(0, 0);
            return;
        }
        int level = ComboLevel;
        maximumComboThisBall = Mathf.Max(maximumComboThisBall, level);
        int bonus = config.ComboBonus(level);
        int awarded = (int)Math.Min(int.MaxValue, (long)bonus * multiplier);
        if (bonus > 0) Add(bonus);
        ComboChanged?.Invoke(level, awarded);
        if (bonus > 0 && AudioManager.Instance != null)
            AudioManager.Instance.Play(config.comboSound, config.comboVolume,
                                       config.comboPitch + (level - 1) * config.comboPitchStep);
        if (combo.Actions == config.comboActionsForIncrease) RaiseMultiplierTo(multiplier + 1);
    }

    private void Update()
    {
        if (config != null && combo.Expire(Time.time, Mathf.Max(.1f, config.comboWindow)))
            ComboChanged?.Invoke(0, 0);
    }

    private void ClearCombo()
    {
        combo.Clear();
        ComboChanged?.Invoke(0, 0);
    }

    public void BeginBall()
    {
        ballActive = true;
        rampsThisBall = maximumComboThisBall = 0;
        validatedCourses.Clear();
        LastBallBonus = default;
        ClearCombo();
    }

    /// <summary>Une seule clôture par bille logique ; les pertes intermédiaires de multiball ne clôturent pas.</summary>
    public void FinishBall(bool tilted)
    {
        if (!ballActive) return;
        ballActive = false;
        ClearCombo();
        if (config == null) return;
        long total = (long)validatedCourses.Count * Mathf.Max(0, config.validatedCourseBonus)
                   + (long)rampsThisBall * Mathf.Max(0, config.successfulRampBonus)
                   + (long)maximumComboThisBall * Mathf.Max(0, config.maximumComboBonus);
        bool forfeited = tilted && config.forfeitBonusOnTilt;
        LastBallBonus = new BallBonus
        {
            Courses = validatedCourses.Count, Ramps = rampsThisBall, MaximumCombo = maximumComboThisBall,
            Points = forfeited ? 0 : (int)Math.Min(int.MaxValue, total), Forfeited = forfeited
        };
        // Le versement précède GameOver et le record, et ne repasse pas par Add/multiplicateur.
        if (LastBallBonus.Points > 0) AddTotal(LastBallBonus.Points);
        if (multiplier >= Mathf.Max(2, config.decayFromMultiplier)) SetMultiplier(multiplier - 1);
        BallBonusAwarded?.Invoke(LastBallBonus);
    }

    public void RecordTargetSeries()
    {
        if (config != null && CanAward()) RaiseMultiplierTo(config.seriesMultiplier);
    }

    public void RecordMissionCompleted(bool targetSeries, int completed, int legacyMultiplier)
    {
        if (config == null) { if (legacyMultiplier > 1) SetMultiplier(legacyMultiplier); return; }
        if (!CanAward()) return;
        RaiseMultiplierTo(legacyMultiplier);
        if (targetSeries) RaiseMultiplierTo(config.seriesMultiplier);
        if (completed >= Mathf.Max(1, config.missionsForMultiplier)) RaiseMultiplierTo(config.missionMultiplier);
    }

    public void RecordBossActivated()
    {
        if (config != null && CanAward()) RaiseMultiplierTo(config.bossMultiplier);
    }

    public void RecordMultiballStarted()
    {
        if (config == null || !CanAward()) return;
        AddBonus(config.multiballActivationBonus);
        RaiseMultiplierTo(config.multiballMultiplier);
    }

    /// <summary>
    /// Compare le score au record et l'enregistre s'il est battu.
    /// Appelé en fin de partie, pas à chaque point : écrire dans les PlayerPrefs à chaque
    /// impact ferait ramer la partie.
    /// </summary>
    /// <returns>Vrai si un nouveau record a été enregistré.</returns>
    public bool CommitHighScore()
    {
        if (Score <= HighScore)
        {
            return false;
        }

        HighScore = Score;
        PlayerPrefs.SetInt(HighScoreKey, HighScore);
        PlayerPrefs.Save();
        HighScoreChanged?.Invoke(HighScore);
        return true;
    }
}
