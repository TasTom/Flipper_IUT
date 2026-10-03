using UnityEngine;

/// <summary>GDD Rampes, effets lumineux et multiball; scoop/aimant ajoutés à la demande du joueur.</summary>
[CreateAssetMenu(menuName = "Pinball/Mechanism Config")]
public class PinballMechanismConfig : ScriptableObject
{
    [Header("Aimant sous le plateau (unités de table, secondes)")]
    [Min(.1f)] public float magneticRadius = .62f;
    [Min(.01f)] public float magneticHeight = .42f;
    [Min(0)] public float magneticAcceleration = 110f;
    [Min(0)] public float magneticDamping = 8f;
    [Min(.01f)] public float coilRise = .045f;
    [Min(.01f)] public float coilFall = .08f;
    [Min(.1f)] public float magneticPulse = 1.4f;
    [Header("Scoop et ball lock")]
    [Min(.05f)] public float scoopDwell = .55f;
    [Min(.05f)] public float ejectCooldown = .8f;
    [Min(1)] public float ejectSpeed = 18f;
    [Range(1, 2)] public int lockCapacity = 2;
    [Min(.15f)] public float releaseSpacing = .45f;
    [Min(0)] public int scoopPoints = 1000;
    [Min(0)] public int lockPoints = 3000;
    [Header("Groupes de lampes (émission, sans nouvelles lumières temps réel)")]
    [Range(0, 1)] public float lampIdle = .1f;
    [Min(0)] public float lampEmission = 2.4f;
    [Min(.1f)] public float lampPeriod = 1.4f;
    [Min(.1f)] public float lampFlashDuration = .65f;
}
