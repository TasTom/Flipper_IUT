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

    [Tooltip("Nombre maximum de billes supplémentaires par partie. 0 = illimité.")]
    [SerializeField] private int maxExtraBalls = 3;

    [Header("Message")]
    [Tooltip("Durée d'affichage du message, en secondes.")]
    [SerializeField] private float messageDuration = 2f;

    private int nextThreshold;
    private int awarded;
    private int lastScore;

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

        if (maxExtraBalls > 0 && awarded >= maxExtraBalls)
        {
            return;
        }

        if (score < nextThreshold)
        {
            return;
        }

        awarded++;
        nextThreshold += thresholdStep > 0 ? thresholdStep : int.MaxValue;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AwardExtraBall();
        }
    }
}
