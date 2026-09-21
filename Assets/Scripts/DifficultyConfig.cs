using UnityEngine;

/// <summary>
/// Valeurs d'équilibrage d'un niveau de difficulté (GDD §Architecture et §Difficultés).
///
/// <para>Le GDD impose que les valeurs d'équilibrage vivent dans des données configurables
/// (ScriptableObjects), jamais en dur dans les scripts. Ce fichier est le premier de la
/// série : il regroupe ce que la difficulté change sur la table — tolérance au tilt,
/// vitesse plafond de la bille, paliers de bille supplémentaire.</para>
///
/// <para>Trois exemplaires — Novice, Standard, Expert — sont posés dans
/// <c>Assets/Config/</c> et choisis par <see cref="DifficultyManager"/>.</para>
/// </summary>
[CreateAssetMenu(fileName = "Difficulte_Standard",
                 menuName = "Flipper/Difficulté")]
public class DifficultyConfig : ScriptableObject
{
    [Header("Identité")]
    [Tooltip("Nom affiché au joueur.")]
    public string difficultyName = "Standard";

    [Header("Tilt (GDD §Tilt)")]
    [Tooltip("Secousses tolérées avant l'avertissement. Plus haut = plus indulgent.")]
    [Range(1, 8)] public int secoussesAvantAvertissement = 2;

    [Tooltip("Secousses tolérées avant le tilt. Plus haut = plus indulgent.")]
    [Range(1, 10)] public int secoussesAvantTilt = 4;

    [Header("Bille (GDD §Bille)")]
    [Tooltip("Vitesse plafond de la bille, en u/s. Échelle : 1 u = 60 mm. Plus haut = plus rapide.")]
    public float maxBallSpeed = 170f;

    [Header("Bille supplémentaire (GDD §Modes avancés)")]
    [Tooltip("Score de la première bille supplémentaire. 0 = jamais.")]
    public int firstExtraBallThreshold = 30000;

    [Tooltip("Écart entre deux paliers successifs. 0 = un seul palier.")]
    public int extraBallThresholdStep = 30000;

    [Tooltip("Billes supplémentaires maximum par partie. 0 = illimité.")]
    public int maxExtraBalls = 3;
}
