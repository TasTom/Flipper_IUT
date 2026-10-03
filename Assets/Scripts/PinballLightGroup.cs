using UnityEngine;

/// <summary>A logical lamp controls explicit emitters together, like VPE lamp groups.</summary>
public class PinballLightGroup : MonoBehaviour
{
    [SerializeField] private PinballMechanismConfig config;
    [SerializeField] private Renderer[] emitters;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private bool generalIllumination;
    [SerializeField] private float phase;
    private MaterialPropertyBlock block;
    private float level, flashUntil;
    private bool animated;
    private float applied = -1;
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    public int EmitterCount => emitters != null ? emitters.Length : 0;
    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (config == null) { Debug.LogWarning("[PinballLightGroup] Config manquante.", this); enabled = false; return; }
        level = generalIllumination ? .75f : config.lampIdle; Apply(level);
    }
    public void SetLevel(float value) { level = Mathf.Clamp01(value); if (block != null && config != null && Time.time >= flashUntil && !animated) Apply(level); }
    public void Flash() { if (config != null) flashUntil = Time.time + config.lampFlashDuration; }
    public void SetAnimated(bool value) { animated = value; if (!value && block != null && config != null) Apply(level); }
    public void ResetGroup() { flashUntil = 0; animated = false; level = generalIllumination ? .75f : (config != null ? config.lampIdle : 0); if(block != null && config != null)Apply(level); }
    private void Update()
    {
        if (Time.time < flashUntil) Apply(1);
        else if (animated) Apply(Mathf.Lerp(config.lampIdle, 1, .5f + .5f * Mathf.Sin(Time.time * Mathf.PI * 2 / config.lampPeriod + phase)));
        else Apply(level);
    }
    private void Apply(float brightness)
    {
        if (Mathf.Abs(applied - brightness) < .002f) return;
        applied = brightness;
        if (emitters == null) return;
        foreach (var emitter in emitters)
        {
            if (emitter == null) continue;
            emitter.GetPropertyBlock(block); block.SetColor(Emission, color * brightness * config.lampEmission); block.SetColor("_BaseColor", color * .32f); emitter.SetPropertyBlock(block);
        }
    }
}
