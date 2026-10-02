using UnityEngine;

/// <summary>
/// Déforme seulement le maillage visible entre les deux poteaux de la face active.
/// Le collider et l'impulsion restent sous la responsabilité de Slingshot (GDD §Slingshots).
/// </summary>
[DisallowMultipleComponent]
public class SlingshotRubberAnimation : MonoBehaviour
{
    [SerializeField] private Slingshot source;
    [SerializeField] private SlingshotRubberConfig config;
    [SerializeField] private MeshFilter rubber;
    [SerializeField] private Transform playfield;
    [SerializeField] private Transform startPost;
    [SerializeField] private Transform endPost;
    [SerializeField] private Transform backPost;

    private Mesh originalMesh;
    private Mesh animatedMesh;
    private Vector3[] restVertices;
    private Vector3[] restNormals;
    private Vector3[] frameVertices;
    private Vector3[] vertices;
    private Vector3[] residual;
    private Vector3[] animatedNormals;
    private Vector3[] residualNormals;
    private Vector3[] frameNormals;
    private Vector3[] gradients;
    private float[] weights;
    private Bounds restBounds;
    private Vector3 start;
    private Vector3 end;
    private Vector3 outward;
    private Vector3 localDisplacement;
    private Vector3 frameDisplacement;
    private Matrix4x4 frameToMesh;
    private Matrix4x4 frameNormalToMesh;
    private Vector3 edge;
    private float edgeSquared;
    private float edgeLength;
    private float spreadSquared;
    private float pinFade;
    private float faceWidth;
    private float elapsed;
    private bool animating;
    private bool ready;

    private void Awake()
    {
        if (source == null) source = GetComponentInParent<Slingshot>();
        if (rubber == null) rubber = GetComponent<MeshFilter>();
        if (playfield == null)
        {
            GameObject table = GameObject.Find("PinballTable");
            if (table != null) playfield = table.transform;
        }
        if (source != null)
        {
            Transform assembly = source.transform.Find("MechanicalAssembly");
            if (assembly != null)
            {
                if (startPost == null) startPost = assembly.Find("SlingPost_1");
                if (endPost == null) endPost = assembly.Find("SlingPost_2");
                if (backPost == null) backPost = assembly.Find("SlingPost_3");
            }
        }
        if (source == null || config == null || rubber == null || playfield == null ||
            startPost == null || endPost == null || backPost == null ||
            rubber.sharedMesh == null || !rubber.sharedMesh.isReadable)
        {
            Debug.LogWarning($"[SlingshotRubberAnimation] '{name}' : config, bande lisible, " +
                "slingshot, plateau et trois poteaux nécessaires. Animation désactivée.", this);
            enabled = false;
            return;
        }

        start = playfield.InverseTransformPoint(startPost.position);
        end = playfield.InverseTransformPoint(endPost.position);
        start.y = end.y = 0f;
        edge = end - start;
        edgeSquared = edge.sqrMagnitude;
        edgeLength = edge.magnitude;
        if (edge.sqrMagnitude < 0.000001f)
        {
            Debug.LogWarning($"[SlingshotRubberAnimation] '{name}' : poteaux confondus.", this);
            enabled = false;
            return;
        }
        outward = new Vector3(edge.z, 0f, -edge.x).normalized;
        Vector3 back = playfield.InverseTransformPoint(backPost.position);
        back.y = 0f;
        if (Vector3.Dot(outward, (start + end) * 0.5f - back) < 0f) outward = -outward;

        originalMesh = rubber.sharedMesh;
        animatedMesh = Instantiate(originalMesh);
        animatedMesh.name = originalMesh.name + "_RuntimeRubber";
        animatedMesh.hideFlags = HideFlags.DontSave;
        animatedMesh.MarkDynamic();
        restVertices = originalMesh.vertices;
        restNormals = originalMesh.normals;
        restBounds = originalMesh.bounds;
        vertices = (Vector3[])restVertices.Clone();
        frameVertices = new Vector3[vertices.Length];
        residual = new Vector3[vertices.Length];
        animatedNormals = (Vector3[])restNormals.Clone();
        residualNormals = new Vector3[vertices.Length];
        frameNormals = new Vector3[vertices.Length];
        gradients = new Vector3[vertices.Length];
        weights = new float[vertices.Length];
        Matrix4x4 meshToFrame = playfield.worldToLocalMatrix * rubber.transform.localToWorldMatrix;
        frameToMesh = meshToFrame.inverse;
        frameNormalToMesh = meshToFrame.transpose;
        Matrix4x4 meshNormalToFrame = frameToMesh.transpose;
        for (int i = 0; i < vertices.Length; i++)
        {
            frameVertices[i] = meshToFrame.MultiplyPoint3x4(restVertices[i]);
            frameNormals[i] = meshNormalToFrame.MultiplyVector(restNormals[i]).normalized;
        }
        rubber.sharedMesh = animatedMesh;
        ready = true;
    }

    private void OnEnable()
    {
        if (ready) source.Kicked += OnKicked;
    }

    private void OnDisable()
    {
        if (source != null) source.Kicked -= OnKicked;
        if (ready) Restore();
    }

    private void OnDestroy()
    {
        if (rubber != null && rubber.sharedMesh == animatedMesh) rubber.sharedMesh = originalMesh;
        if (animatedMesh == null) return;
#if UNITY_EDITOR
        // A Play exit can still report isPlaying=true while rejecting deferred destruction.
        DestroyImmediate(animatedMesh);
#else
        Destroy(animatedMesh);
#endif
    }

    private void OnKicked(Vector3 worldPoint, float impactSpeed)
    {
        if (!ready || !isActiveAndEnabled || config == null) return;
        Vector3 point = playfield.InverseTransformPoint(worldPoint);
        point.y = 0f;
        float hitPosition = Mathf.Clamp01(Vector3.Dot(point - start, edge) / edgeSquared);
        float spread = Mathf.Max(0.001f, config.impactSpread);
        spreadSquared = spread * spread;
        pinFade = Mathf.Max(0.001f, config.pinFadeDistance);
        faceWidth = Mathf.Max(0.001f, config.faceHalfWidth);
        const float derivativeStep = 0.001f;

        for (int i = 0; i < vertices.Length; i++)
        {
            residual[i] = vertices[i] - restVertices[i];
            residualNormals[i] = animatedNormals[i] - restNormals[i];
            Vector3 v = frameVertices[i];
            v.y = 0f;
            weights[i] = Weight(v, hitPosition);
            gradients[i] = new Vector3(
                Weight(v + Vector3.right * derivativeStep, hitPosition) - Weight(v - Vector3.right * derivativeStep, hitPosition),
                0f,
                Weight(v + Vector3.forward * derivativeStep, hitPosition) - Weight(v - Vector3.forward * derivativeStep, hitPosition)) / (2f * derivativeStep);
        }

        float strength = Mathf.Lerp(config.minimumStrength, 1f,
            Mathf.Clamp01(impactSpeed / Mathf.Max(0.01f, config.fullStrengthSpeed)));
        frameDisplacement = outward * config.peakDeflection * strength;
        localDisplacement = frameToMesh.MultiplyVector(frameDisplacement);
        elapsed = 0f;
        animating = true;
    }

    private void LateUpdate() => Advance(Time.deltaTime);

    private float Weight(Vector3 point, float hitPosition)
    {
        float t = Vector3.Dot(point - start, edge) / edgeSquared;
        float pinDistance = Mathf.Min(Vector3.Distance(point, start), Vector3.Distance(point, end));
        float pinned = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((pinDistance - config.pinnedRadius) / pinFade));
        float faceDistance = Mathf.Abs(Vector3.Dot(point - start, outward));
        float face = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((faceDistance - faceWidth * 0.75f) / (faceWidth * 0.25f)));
        float distance = (t - hitPosition) * edgeLength;
        float impact = Mathf.Exp(-0.5f * distance * distance / spreadSquared);
        float envelope = t > 0f && t < 1f ? Mathf.Sin(t * Mathf.PI) : 0f;
        return pinned * face * impact * envelope;
    }

    private void Advance(float deltaTime)
    {
        if (!ready || !animating || deltaTime <= 0f) return;
        if (config == null) { Restore(); return; }
        elapsed += deltaTime;
        float duration = Mathf.Max(0.01f, config.duration);
        if (elapsed >= duration)
        {
            Restore();
            return;
        }
        float motion = config.motion != null ? config.motion.Evaluate(elapsed / duration) : 0f;
        float remaining = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(elapsed / Mathf.Max(0.001f, config.retriggerBlendDuration)));
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = restVertices[i] + localDisplacement * (weights[i] * motion) + residual[i] * remaining;
            // Transform the imported smooth normal by the deformation's inverse Jacobian.
            // Recalculating from unevenly subdivided triangles produces visible facets.
            Vector3 displacement = frameDisplacement * motion;
            float determinant = Mathf.Max(0.1f, 1f + Vector3.Dot(gradients[i], displacement));
            Vector3 normal = frameNormals[i] - gradients[i] * (Vector3.Dot(displacement, frameNormals[i]) / determinant);
            animatedNormals[i] = (frameNormalToMesh.MultiplyVector(normal).normalized + residualNormals[i] * remaining).normalized;
        }
        animatedMesh.vertices = vertices;
        animatedMesh.normals = animatedNormals;
        animatedMesh.RecalculateBounds();
    }

    private void Restore()
    {
        animating = false;
        System.Array.Copy(restVertices, vertices, vertices.Length);
        System.Array.Copy(restNormals, animatedNormals, animatedNormals.Length);
        animatedMesh.vertices = vertices;
        animatedMesh.normals = restNormals;
        animatedMesh.bounds = restBounds;
    }
}
