using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Habillage anim├® de Bumper3. Le collider et les contacts restent ceux de VPE.</summary>
public sealed class IndustriesAutomateFx : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    private static readonly Color Red = new Color(1f, .065f, .012f);
    private readonly List<Material> materials = new List<Material>();
    private readonly Transform[] embers = new Transform[10];
    private Renderer[] bumperRenderers;
    private bool[] wasHidden;
    private Transform model, head;
    private Material signal;
    private Vector3 headRest;
    private Quaternion headRotation;
    private float clock, impactAge;
    private bool visible, defeated;

    public static bool TryCreate(Transform bumper, GameObject asset, out IndustriesAutomateFx effect)
    {
        effect = null;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return false;
        var original = bumper.GetComponentsInChildren<Renderer>(true);
        var root = new GameObject("Industries_AutomateFX");
        root.SetActive(false);
        root.transform.SetParent(bumper, false);
        effect = root.AddComponent<IndustriesAutomateFx>();
        effect.Build(asset, shader, original);
        return true;
    }

    private void Build(GameObject asset, Shader shader, Renderer[] original)
    {
        bumperRenderers = original;
        wasHidden = new bool[original.Length];
        model = Instantiate(asset, transform).transform;
        model.name = "Automate";
        model.localRotation = Quaternion.identity;
        model.localScale *= 1.11f;
        foreach (var child in model.GetComponentsInChildren<Transform>(true))
            if (child.name == "Head") { head = child; break; }
        if (head != null)
        {
            headRest = head.localPosition;
            headRotation = head.localRotation;
        }
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;

        signal = Surface(shader, Red, .12f, .32f);
        signal.EnableKeyword("_EMISSION");
        var palette = new Dictionary<string, Material>
        {
            ["Iron"] = Surface(shader, new Color(.08f, .13f, .13f), .50f, .59f),
            ["Steel"] = Surface(shader, new Color(.40f, .52f, .55f), .66f, .64f),
            ["Enamel"] = Surface(shader, new Color(.30f, .39f, .18f), .24f, .55f),
            ["Brass"] = Surface(shader, new Color(.70f, .37f, .12f), .61f, .63f),
            ["Rubber"] = Surface(shader, new Color(.024f, .035f, .037f), .06f, .40f),
            ["Hazard"] = Surface(shader, new Color(.97f, .61f, .075f), .16f, .45f),
            ["Signal"] = signal
        };
        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            string key = renderer.name.Substring(renderer.name.LastIndexOf('_') + 1);
            if (palette.TryGetValue(key, out var material)) renderer.sharedMaterial = material;
        }

        var flame = Surface(shader, new Color(1f, .33f, .025f), 0f, .3f);
        flame.EnableKeyword("_EMISSION");
        flame.SetColor(Emission, new Color(2.3f, .8f, .09f));
        for (int i = 0; i < embers.Length; i++)
        {
            var spark = GameObject.CreatePrimitive(i < 5 ? PrimitiveType.Cube : PrimitiveType.Sphere);
            spark.name = i < 5 ? "├ëtincelle" : "Flamme";
            spark.transform.SetParent(transform, false);
            var collider = spark.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            var renderer = spark.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = flame;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            spark.SetActive(false);
            embers[i] = spark.transform;
        }
    }

    public void Activate()
    {
        if (!visible)
        {
            for (int i = 0; i < bumperRenderers.Length; i++)
            {
                var renderer = bumperRenderers[i];
                if (renderer == null) continue;
                wasHidden[i] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
            }
        }
        visible = true;
        defeated = false;
        clock = 0f;
        impactAge = float.PositiveInfinity;
        model.localPosition = Vector3.down * .017f;
        model.localRotation = Quaternion.identity;
        if (head != null) { head.localPosition = headRest; head.localRotation = headRotation; }
        foreach (var ember in embers) ember.gameObject.SetActive(false);
        gameObject.SetActive(true);
    }

    public void Impact(bool isFinal)
    {
        impactAge = 0f;
        defeated = isFinal;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f) return;
        clock += Time.deltaTime;
        impactAge += Time.deltaTime;
        if (defeated && impactAge >= 1.3f) { RestoreBumper(); return; }
        float recoil = Mathf.Clamp01(1f - impactAge * 5f);
        float rise = Mathf.SmoothStep(-.017f, 0f, clock * 3.6f);
        model.localPosition = Vector3.up * (rise - recoil * .0035f - (defeated ? .015f * impactAge : 0f));
        model.localRotation = Quaternion.Euler(recoil * 12f, 0f, defeated ? impactAge * 18f : 0f);
        if (head != null)
            head.localRotation = Quaternion.AngleAxis(Mathf.Sin(clock * 3.7f) * 10f, Vector3.up) * headRotation;
        signal.SetColor(Emission, defeated ? Red * .4f : Red * (2.3f + Mathf.Sin(clock * 7f) * .8f) + Color.white * recoil * 2f);
        AnimateImpact();
    }

    private void AnimateImpact()
    {
        for (int i = 0; i < embers.Length; i++)
        {
            bool flame = i >= 5;
            float duration = flame ? .68f : .34f;
            float age = impactAge - i * .015f;
            var ember = embers[i];
            bool show = age >= 0f && age < duration && (defeated || !flame);
            ember.gameObject.SetActive(show);
            if (!show) continue;
            float angle = i * 2.4f;
            float radius = .012f + age * (flame ? .018f : .085f);
            ember.localPosition = new Vector3(Mathf.Cos(angle) * radius,
                .041f + age * (.12f - .13f * age), Mathf.Sin(angle) * radius);
            float size = flame ? Mathf.Sin(age / duration * Mathf.PI) * .006f : .0015f;
            ember.localScale = new Vector3(size, size * (flame ? 2.8f : 3.4f), size);
            ember.localRotation = Quaternion.Euler(19f, 0f, i * 38f);
        }
    }

    public void RestoreBumper()
    {
        if (!visible) return;
        visible = false;
        for (int i = 0; i < bumperRenderers.Length; i++)
            if (bumperRenderers[i] != null) bumperRenderers[i].forceRenderingOff = wasHidden[i];
        gameObject.SetActive(false);
    }

    private Material Surface(Shader shader, Color color, float metallic, float smoothness)
    {
        var material = new Material(shader) { hideFlags = HideFlags.DontSave };
        material.SetColor(BaseColor, color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        materials.Add(material);
        return material;
    }

    private void OnDisable() => RestoreBumper();

    private void OnDestroy()
    {
        foreach (var material in materials) Destroy(material);
    }
}
