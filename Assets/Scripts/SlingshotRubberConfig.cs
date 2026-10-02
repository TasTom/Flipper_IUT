using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Présentation du caoutchouc du slingshot — GDD §Slingshots.</summary>
[CreateAssetMenu(menuName = "Pinball/Slingshot Rubber Config")]
public class SlingshotRubberConfig : ScriptableObject
{
    [Min(0.01f)] public float duration = 0.18f;
    [Min(0f)] public float peakDeflection = 0.10f;
    [FormerlySerializedAs("impactSpread")]
    [Tooltip("Demi-largeur de l'arrondi du poussoir central, en unités de table.")]
    [Min(0.001f)] public float kickerHalfWidth = 0.055f;
    [Min(0f)] public float pinnedRadius = 0.125f;
    [Min(0.001f)] public float pinFadeDistance = 0.10f;
    [Min(0.001f)] public float faceHalfWidth = 0.16f;
    [Min(0.001f)] public float retriggerBlendDuration = 0.035f;

    [Tooltip("Poussée centrale du mécanisme, retour amorti. Temps normalisé.")]
    public AnimationCurve motion = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.06f, 0.30f),
        new Keyframe(0.22f, 1f), new Keyframe(0.50f, 0.12f),
        new Keyframe(0.67f, -0.09f), new Keyframe(0.84f, 0.025f),
        new Keyframe(1f, 0f));
}
