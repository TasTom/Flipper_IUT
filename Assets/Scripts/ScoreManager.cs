using System;
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
    [Tooltip("Le multiplicateur reste à 1 tant que le GDD §Score n'est pas implémenté.")]
    [SerializeField] private int multiplier = 1;

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
    }

    /// <summary>Ajoute des points, pondérés par le multiplicateur courant.</summary>
    /// <param name="points">Points de base, avant multiplicateur.</param>
    public void Add(int points)
    {
        if (points == 0)
        {
            return;
        }

        Score = Mathf.Max(0, Score + points * multiplier);
        ScoreChanged?.Invoke(Score);
    }

    /// <summary>Remet le score à zéro pour une nouvelle partie. Le record est conservé.</summary>
    public void ResetScore()
    {
        Score = startingScore;
        multiplier = 1;
        ScoreChanged?.Invoke(Score);
    }

    /// <summary>Fixe le multiplicateur (GDD §Score, étape 2).</summary>
    public void SetMultiplier(int value)
    {
        multiplier = Mathf.Max(1, value);
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
