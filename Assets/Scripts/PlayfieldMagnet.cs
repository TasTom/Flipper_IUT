using UnityEngine;

/// <summary>Under-playfield attraction; no vertical force, snap or kinematic capture.</summary>
public class PlayfieldMagnet : MonoBehaviour
{
    [SerializeField] private PinballMechanismConfig config;
    [SerializeField] private Transform tableFrame;
    [SerializeField] private PinballLightGroup lamps;
    private readonly Collider[] overlaps = new Collider[8];
    private readonly Rigidbody[] seen = new Rigidbody[8];
    private float current, until;
    public float Current => current;
    public void Pulse() { if (config != null) until = Time.time + config.magneticPulse; }
    public void Stop() { until = 0; current = 0; }
    private void Awake()
    {
        if (tableFrame == null) { var root = GameObject.Find("PinballTable"); if (root != null) tableFrame = root.transform; }
        if (config == null || tableFrame == null) { Debug.LogWarning("[PlayfieldMagnet] Config ou repère de table manquant.", this); enabled = false; }
    }
    private void FixedUpdate()
    {
        float command = Time.time < until ? 1f : 0f;
        float tau = command > current ? config.coilRise : config.coilFall;
        current = Mathf.Lerp(current, command, 1f - Mathf.Exp(-Time.fixedDeltaTime / Mathf.Max(.001f, tau)));
        if (lamps != null) lamps.SetLevel(Mathf.Max(config.lampIdle, current));
        if (current < .001f) return;
        int count = Physics.OverlapSphereNonAlloc(transform.position, config.magneticRadius, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        int bodies = 0;
        for (int i = 0; i < count; i++)
        {
            var collider = overlaps[i]; if (!collider.CompareTag("Ball")) continue;
            var body = collider.attachedRigidbody; if (body == null || body.isKinematic) continue;
            bool duplicate = false; for (int j = 0; j < bodies; j++) if (seen[j] == body) duplicate = true;
            if (duplicate) continue; seen[bodies++] = body;
            Vector3 local = tableFrame.InverseTransformVector(body.position - transform.position);
            if (local.y < 0 || local.y > config.magneticHeight) continue; // Exclude elevated ramp traffic.
            var radial = new Vector3(local.x, 0, local.z); float distance = radial.magnitude;
            if (distance >= config.magneticRadius) continue;
            float falloff = Mathf.Pow(1f - distance / config.magneticRadius, 2);
            Vector3 velocity = Vector3.ProjectOnPlane(body.linearVelocity, tableFrame.up);
            Vector3 pull = -tableFrame.TransformDirection(radial) * config.magneticAcceleration;
            body.AddForce((pull - velocity * config.magneticDamping) * falloff * current * current * body.mass * Time.fixedDeltaTime, ForceMode.Impulse);
        }
    }
    private void OnDisable() => Stop();
}
