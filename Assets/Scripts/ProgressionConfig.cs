using UnityEngine;

/// <summary>
/// Réglages du passage vers la seconde table (GDD §Architecture : toute valeur d'équilibrage vit
/// dans un <c>*Config</c>, jamais en dur).
///
/// <para>Le seuil est une donnée, pas une constante : c'est le genre de valeur qu'on ajuste vingt
/// fois en jouant, et la changer ne doit demander ni recompilation ni retouche de scène.</para>
/// </summary>
[CreateAssetMenu(menuName = "Vosges Mania/Progression", fileName = "ProgressionConfig")]
public class ProgressionConfig : ScriptableObject
{
    [Header("Déclenchement")]
    [Tooltip("Score à atteindre pour basculer sur la seconde table.")]
    [Min(1)] public int scoreThreshold = 100000;

    public string sourceSceneName = "Neutral";
    [Min(0.01f)] public float fadeInDuration = .8f;
    [Min(0.01f)] public float fadeOutDuration = .6f;
    public float cabinetRotation = -90f;

    [Tooltip("Nom exact de la scène cible, tel qu'il figure dans les Build Settings.")]
    public string targetSceneName = "Industries";

    [Tooltip("Met la partie en pause pendant la transition. Sans elle, la bille continue sa " +
             "course derrière le voile et le joueur la retrouve déjà drainée.")]
    public bool pauseBeforeTransition = true;

    [Header("Carton de transition")]
    public string title = "ATELIER DES VOSGES";

    [Tooltip("Sous-titre affiché sous le titre.")]
    public string subtitle = "Ligne 2 — industries, montagne et IUT";

    [Tooltip("Secondes de tenue du carton avant le chargement.")]
    [Min(0f)] public float holdDuration = 1.1f;
}
