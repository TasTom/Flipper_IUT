using UnityEngine;

/// <summary>GDD : direction artistique, états lumineux des cibles, rampes et FX optimisés.</summary>
[CreateAssetMenu(menuName="Vosges Mania/Industries Lighting",fileName="IndustriesLightingConfig")]
public sealed class IndustriesLightingConfig : ScriptableObject
{
    public Color amber=new Color(1f,.43f,.10f);
    public Color turquoise=new Color(.08f,.76f,1f);
    public Color ivory=new Color(1f,.79f,.43f);
    [Min(0)] public float idleEmission=1.5f;
    [Min(0)] public float activeEmission=3.6f;
    [Min(0)] public float flashEmission=8f;
    [Min(.01f)] public float flashSeconds=.24f;
    [Min(0)] public float haloGain=.18f;
    [Min(0)] public float chaseSpeed=1.4f;
    [Range(0,1)] public float breathingAmount=.12f;
    [Min(0)] public float lightIntensity=.8f;
    [Min(.01f)] public float lightRange=.20f;
    [Min(0)] public float workshopKeyIntensity=1.5f;
    [Min(0)] public float workshopFillIntensity=.65f;
    [Min(0)] public float bloomIntensity=.38f;
    [Min(0)] public float bloomThreshold=1.15f;
    [Range(0,1)] public float bloomScatter=.55f;
    [Range(0,1)] public float vignetteIntensity=.13f;
}
