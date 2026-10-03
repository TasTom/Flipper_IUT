using UnityEngine;

/// <summary>PhysX adaptation of Industries contacts (GDD §Bille et lanceur, §Flippers principaux).
/// VPE coefficients describe another solver; these materials are calibrated for Neutral's units.</summary>
[CreateAssetMenu(menuName = "Flipper/Pinball Physics Config")]
public sealed class PinballPhysicsConfig : ScriptableObject
{
    [Min(0.001f)] public float ballMass = 2f;
    [Min(0f)] public float angularDamping = 0.02f;
    [Min(0f)] public float linearDamping;
    [Min(0.001f)] public float contactOffset = 0.003f;
    [Range(0.001f, 0.02f)] public float simulationStep = 0.002f;
    [Min(0f)] public float flipperRiseSpeed = 2600f;
    [Min(0f)] public float flipperReturnSpeed = 700f;
    public PhysicsMaterial ballMaterial;
    public PhysicsMaterial playfieldMaterial;
    public PhysicsMaterial rubberMaterial;
    public PhysicsMaterial metalMaterial;
}
