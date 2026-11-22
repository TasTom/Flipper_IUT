using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Décharges décoratives entre les bornes du régulateur Énergie.</summary>
[DisallowMultipleComponent]
public sealed class IndustriesElectricArc : MonoBehaviour
{
    [SerializeField] private Transform firstTerminal;
    [SerializeField] private Transform secondTerminal;
    [SerializeField] private Material glowMaterial;
    [SerializeField, Min(.25f)] private float interval = 4.6f;
    [SerializeField, Range(.12f, .4f)] private float burstDuration = .23f;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private readonly Vector3[] arc = new Vector3[8];
    private readonly Vector3[] fork = new Vector3[3];
    private LineRenderer mainLine, branchLine;
    private float nextBurst, burstEnd, nextShape;
    private int burstNumber, shape;

    private void Awake()
    {
        if (firstTerminal == null || secondTerminal == null || glowMaterial == null)
        {
            Debug.LogWarning("[Industries] Bornes ou matériau manquants pour l’arc électrique.", this);
            enabled = false;
            return;
        }

        mainLine = CreateLine("Arc principal", arc.Length, .00085f, new Color(.60f, 1.9f, 3.2f));
        branchLine = CreateLine("Ramification", fork.Length, .00044f, new Color(.22f, .92f, 2.3f));
    }

    private void OnEnable()
    {
        nextBurst = Time.time + 1.5f;
        burstEnd = float.NegativeInfinity;
    }

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f) return;
        float now = Time.time;
        if (now >= nextBurst)
        {
            burstEnd = now + burstDuration;
            nextBurst = burstEnd + interval + (burstNumber % 3) * .37f;
            nextShape = now;
            shape = burstNumber++ * 7;
            mainLine.enabled = true;
        }
        if (now >= burstEnd)
        {
            Hide();
            return;
        }
        if (now < nextShape) return;
        nextShape = now + .037f;
        Draw(++shape);
    }

    private void Draw(int frame)
    {
        Vector3 from = transform.InverseTransformPoint(firstTerminal.position);
        Vector3 to = transform.InverseTransformPoint(secondTerminal.position);
        Vector3 sideways = Vector3.Cross(Vector3.up, to - from).normalized;
        arc[0] = from;
        arc[^1] = to;
        for (int i = 1; i < arc.Length - 1; i++)
        {
            float t = (float)i / (arc.Length - 1);
            float envelope = Mathf.Sin(Mathf.PI * t);
            float noise = 2f * Mathf.PerlinNoise(i * 1.14f, frame * .53f) - 1f;
            arc[i] = Vector3.Lerp(from, to, t)
                + envelope * (Vector3.up * (.0022f + noise * .00135f) + sideways * (noise * .0033f));
        }
        mainLine.SetPositions(arc);
        branchLine.enabled = frame % 3 != 0;
        if (!branchLine.enabled) return;
        fork[0] = arc[3];
        fork[1] = arc[4] - sideways * .002f + Vector3.up * .00065f;
        fork[2] = Vector3.Lerp(arc[5], to, .38f) - sideways * .0027f;
        branchLine.SetPositions(fork);
    }

    private LineRenderer CreateLine(string objectName, int count, float width, Color color)
    {
        var host = new GameObject(objectName);
        host.transform.SetParent(transform, false);
        var line = host.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = count;
        line.widthMultiplier = width * transform.lossyScale.x;
        line.sharedMaterial = glowMaterial;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColor, color);
        line.SetPropertyBlock(block);
        line.enabled = false;
        return line;
    }

    private void OnDisable() => Hide();

    private void Hide()
    {
        if (mainLine != null) mainLine.enabled = false;
        if (branchLine != null) branchLine.enabled = false;
    }
}
