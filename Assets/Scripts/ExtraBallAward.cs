using UnityEngine;

/// <summary>
/// Attribue des billes supplémentaires (GDD §Modes avancés).
///
/// <para>Le GDD prévoit une bille supplémentaire à certains jalons de score. Ce composant
/// surveille le score via <see cref="ScoreManager"/> et appelle
/// <see cref="GameManager.AwardExtraBall()"/> à chaque palier franchi. Les paliers sont un
/// champ, pas une constante : l'équilibrage se règle dans l'Inspector.</para>
///
/// <para>Un même palier ne se déclenche qu'une fois par partie — <see cref="GameManager"/>
/// remet les jalons à zéro au début de chaque partie via <see cref="ResetThresholds"/>.</para>
/// </summary>
[DefaultExecutionOrder(50)]   // après ScoreManager
public class ExtraBallAward : MonoBehaviour
{
    [Header("Paliers")]
    [Tooltip("Score à atteindre pour la première bille supplémentaire. GDD §Barème : " +
             "calé sur une demi-mission complète (30 000).")]
    [SerializeField] private int firstThreshold = 30000;

    [Tooltip("Écart entre deux paliers successifs. 0 = un seul palier.")]
    [SerializeField] private int thresholdStep = 30000;

    [Tooltip("Nombre maximum de billes supplémentaires par partie. Plafonné à " +
             "MaxExtraBallsPerGame : une seule bille bonus, quel que soit le réglage importé.")]
    [SerializeField] private int maxExtraBalls = MaxExtraBallsPerGame;

    /// <summary>
    /// Une seule bille supplémentaire par partie.
    ///
    /// <para>Le réglage reste un champ — les paliers de score, eux, sont de l'équilibrage — mais
    /// le nombre, non : mesuré en jeu, les trois paliers à 30 000 / 60 000 / 90 000 points
    /// portaient le compteur à <b>six</b> billes en moins d'une minute, et le joueur lisait ce
    /// compteur comme un bug. Le plafond est donc une constante, et non une valeur d'Inspector :
    /// une scène qui porte encore 3 ne peut plus le dépasser.</para>
    /// </summary>
    public const int MaxExtraBallsPerGame = 1;

    [Header("Message")]
    [Tooltip("Durée d'affichage du message, en secondes.")]
    [SerializeField] private float messageDuration = 2f;

    private int nextThreshold;
    private int awarded;
    private int lastScore;

    /// <summary>
    /// Billes bonus réellement autorisées : le réglage, ramené sous le plafond
    /// <see cref="MaxExtraBallsPerGame"/>. La valeur d'Inspector reste lisible — elle dit ce que
    /// la configuration demande — mais elle ne peut pas faire dépasser le plafond.
    /// </summary>
    private int Budget => Mathf.Clamp(maxExtraBalls <= 0 ? MaxExtraBallsPerGame : maxExtraBalls,
                                      1, MaxExtraBallsPerGame);

    private void OnEnable()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ScoreChanged += OnScoreChanged;
        }

        ResetThresholds();
        lastScore = 0;
    }

    private void OnDisable()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ScoreChanged -= OnScoreChanged;
        }
    }

    /// <summary>Remet les jalons à zéro. Appelé au début d'une partie.</summary>
    public void ResetThresholds()
    {
        nextThreshold = firstThreshold;
        awarded = 0;
    }

    /// <summary>
    /// Fixe les paliers (GDD §Difficultés). Appelé par <see cref="DifficultyManager"/> ;
    /// les valeurs viennent d'un <see cref="DifficultyConfig"/>, jamais en dur.
    /// </summary>
    public void ConfigurerPaliers(int premierPalier, int pas, int maxBillets)
    {
        firstThreshold = premierPalier;
        thresholdStep = pas;
        maxExtraBalls = maxBillets;
        ResetThresholds();
    }

    private void OnScoreChanged(int score)
    {
        // Un score qui baisse = nouvelle partie (ResetScore) : remettre les jalons à zéro,
        // sinon les paliers déjà franchis ne se redonneraient jamais.
        if (score < lastScore)
        {
            ResetThresholds();
        }

        lastScore = score;

        if (nextThreshold <= 0)
        {
            return;
        }

        bool awardedAny = false;
        // Un gros bonus peut franchir plusieurs paliers en une seule fois.
        while (nextThreshold > 0 && score >= nextThreshold && awarded < Budget)
        {
            awarded++;
            long next = (long)nextThreshold + thresholdStep;
            nextThreshold = thresholdStep > 0 && next <= int.MaxValue ? (int)next : 0;
            if (GameManager.Instance != null) GameManager.Instance.AwardExtraBall();
            awardedAny = true;
        }

        if (awardedAny && GameManager.Instance != null)
        {
            GameManager.Instance.ShowMessage("BILLE SUPPLÉMENTAIRE", messageDuration);
        }
    }
}
