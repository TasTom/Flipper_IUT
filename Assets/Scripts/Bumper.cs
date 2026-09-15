using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bumper actif (GDD §Bumpers) : repousse la bille dans l'axe du centre vers le point de
/// contact, marque des points et rend un retour visuel.
///
/// Remplace <see cref="ScoreTarget"/> sur les bumpers : ce dernier ne gère qu'un score et une
/// poussée, sans temporisation ni cadence. S'il est encore présent sur le même objet, il est
/// désactivé au démarrage plutôt que de marquer les points en double.
/// </summary>
/// <remarks>
/// Pas de <c>[RequireComponent(typeof(Collider))]</c> : la scène neutre doit pouvoir exister
/// avant les assets, avec le script lié mais sans collider. Le composant n'utilise le sien que
/// par <see cref="OnCollisionEnter"/>, il ne le déréférence jamais — sans collider il est
/// simplement muet, ce qui est l'état attendu d'un hôte vide.
/// </remarks>
public class Bumper : MonoBehaviour
{
    [Header("Effet")]
    [Tooltip("Impulsion de répulsion, en unités/s.")]
    [SerializeField] private float bounceForce = 125f;   // echelle : 7,5 x 16,67

    [Tooltip("Part de la poussée dirigée vers le haut de la table (GDD : direction.y 0.2 à 0.25).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float upwardBias = 0.22f;

    [Tooltip("Délai minimum entre deux répulsions, en secondes.")]
    [SerializeField] private float cooldown = 0.1f;

    [Header("Score")]
    [SerializeField] private int scoreValue = 150;

    [Tooltip("Nombre de touches avant que le bumper ne s'éteigne (0 : illimité). " +
             "Sert aux objectifs de type « atteindre les trois bumpers » du GDD §Multiball.")]
    [SerializeField] private int hitsBeforeExtinguish;

    [Header("Retour visuel")]
    [SerializeField] private Renderer[] flashRenderers;
    [SerializeField] private Color flashColor = new Color(1f, 0.4f, 0.2f);
    [SerializeField] private float flashDuration = 0.1f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly List<MaterialPropertyBlock> blocks = new List<MaterialPropertyBlock>();
    private readonly List<Color> baseColors = new List<Color>();
    private float lastBounceTime = float.NegativeInfinity;
    private float flashEndTime;
    private int hitCount;

    /// <summary>Nombre de fois où ce bumper a été touché depuis le début de la partie.</summary>
    public int HitCount => hitCount;

    /// <summary>Vrai quand le bumper a atteint son quota de touches.</summary>
    public bool IsExtinguished => hitsBeforeExtinguish > 0 && hitCount >= hitsBeforeExtinguish;

    private void Awake()
    {
        DisableLegacyScoreTarget();

        if (flashRenderers == null || flashRenderers.Length == 0)
        {
            flashRenderers = GetComponentsInChildren<Renderer>(true);
        }

        CacheColors();
    }

    private void Update()
    {
        if (flashEndTime > 0f && Time.time >= flashEndTime)
        {
            RestoreColors();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Ball"))
        {
            return;
        }

        Rigidbody ball = collision.rigidbody != null ? collision.rigidbody : collision.collider.attachedRigidbody;

        if (ball == null || IsExtinguished || Time.time - lastBounceTime < cooldown)
        {
            return;
        }

        lastBounceTime = Time.time;
        hitCount++;

        // Répulsion radiale : la bille repart à l'opposé du centre du bumper, quelle que soit
        // la face touchée. Utiliser la normale du contact seule ferait « coller » la bille sur
        // les flancs du chapeau.
        Vector3 direction = ball.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        direction = direction.normalized;
        direction.y = upwardBias;

        ball.AddForce(direction.normalized * bounceForce, ForceMode.Impulse);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.Add(scoreValue);
        }

        Flash();
    }

    /// <summary>Remet le compteur de touches à zéro pour une nouvelle partie.</summary>
    public void ResetHits()
    {
        hitCount = 0;
        RestoreColors();
    }

    /// <summary>
    /// <see cref="ScoreTarget"/> assurait le rôle de bumper avant ce composant. Le laisser actif
    /// ferait marquer deux fois les mêmes points et appliquerait deux impulsions.
    /// </summary>
    private void DisableLegacyScoreTarget()
    {
        ScoreTarget legacy = GetComponent<ScoreTarget>();

        if (legacy == null)
        {
            return;
        }

        legacy.enabled = false;
        Debug.Log($"[Bumper] '{name}' : ScoreTarget désactivé, Bumper prend le relais " +
                  "(sinon les points seraient comptés deux fois).", this);
    }

    private void CacheColors()
    {
        blocks.Clear();
        baseColors.Clear();

        foreach (Renderer target in flashRenderers)
        {
            if (target == null)
            {
                continue;
            }

            blocks.Add(new MaterialPropertyBlock());
            baseColors.Add(ReadBaseColor(target));
        }
    }

    private static Color ReadBaseColor(Renderer target)
    {
        Material material = target.sharedMaterial;

        if (material == null)
        {
            return Color.white;
        }

        if (material.HasProperty(BaseColorId))
        {
            return material.GetColor(BaseColorId);
        }

        return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
    }

    private void Flash()
    {
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            Renderer target = flashRenderers[i];

            if (target == null)
            {
                continue;
            }

            MaterialPropertyBlock block = blocks[i];
            target.GetPropertyBlock(block);

            if (target.sharedMaterial != null && target.sharedMaterial.HasProperty(EmissionColorId))
            {
                block.SetColor(EmissionColorId, flashColor);
            }

            block.SetColor(BaseColorId, flashColor);
            target.SetPropertyBlock(block);
        }

        flashEndTime = Time.time + flashDuration;
    }

    private void RestoreColors()
    {
        flashEndTime = 0f;

        // Un bumper éteint est assombri, jamais caché : masquer le GameObject couperait
        // aussi son collider et la bille le traverserait.
        float dim = IsExtinguished ? 0.35f : 1f;

        for (int i = 0; i < flashRenderers.Length; i++)
        {
            Renderer target = flashRenderers[i];

            if (target == null)
            {
                continue;
            }

            MaterialPropertyBlock block = blocks[i];
            target.GetPropertyBlock(block);

            if (target.sharedMaterial != null && target.sharedMaterial.HasProperty(EmissionColorId))
            {
                block.SetColor(EmissionColorId, Color.black);
            }

            // Color * float touche aussi l'alpha : on le restaure pour ne pas rendre
            // le bumper translucide en plus de l'assombrir.
            Color dimmed = baseColors[i] * dim;
            dimmed.a = baseColors[i].a;
            block.SetColor(BaseColorId, dimmed);
            target.SetPropertyBlock(block);
        }
    }
}

