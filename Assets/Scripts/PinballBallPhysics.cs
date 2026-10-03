using UnityEngine;

/// <summary>Uses the same contact profile for the scene ball and subsequently spawned balls.</summary>
[DisallowMultipleComponent]
public sealed class PinballBallPhysics : MonoBehaviour
{
    [SerializeField] private PinballPhysicsConfig config;

    private void Awake()
    {
        var body = GetComponent<Rigidbody>();
        var sphere = GetComponent<SphereCollider>();
        if (config == null || body == null || sphere == null || config.ballMaterial == null)
        {
            Debug.LogWarning("[PinballBallPhysics] Profil, Rigidbody ou SphereCollider manquant ; réglages existants conservés.", this);
            return;
        }

        body.mass = Mathf.Max(0.001f, config.ballMass);
        body.linearDamping = Mathf.Max(0f, config.linearDamping);
        body.angularDamping = Mathf.Max(0f, config.angularDamping);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        // BallManager can park the scene ball before this Awake executes.
        if (!body.isKinematic) body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        sphere.sharedMaterial = config.ballMaterial;
        sphere.contactOffset = Mathf.Max(0.001f, config.contactOffset);
    }
}
