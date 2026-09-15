using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Slingshot : renvoie la bille avec une impulsion sèche quand elle touche la bande
/// (GDD §Slingshots).
///
/// Le contact est traité en <c>OnCollisionEnter</c> et non en continu : sans temporisation,
/// une bille qui roule le long de la bande déclencherait une impulsion par pas physique et
/// finirait par traverser la table.
/// </summary>
/// <remarks>
/// Pas de <c>[RequireComponent(typeof(Collider))]</c> : la scène neutre doit pouvoir exister
/// avant les assets, avec le script lié mais sans collider. Le composant n'utilise le sien que
/// par <see cref="OnCollisionEnter"/>, il ne le déréférence jamais — sans collider il est
/// simplement muet, ce qui est l'état attendu d'un hôte vide.
/// </remarks>
public class Slingshot : MonoBehaviour
{
    [Header("Effet")]
    [Tooltip("Impulsion appliquée à la bille, en unités/s.")]
    [SerializeField] private float kickForce = 150f;   // echelle : 9 x 16,67

    [Tooltip("Part de la poussée dirigée vers le haut de la table (GDD : direction.y 0.2 à 0.25).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float upwardBias = 0.05f;

    [Tooltip("Délai minimum entre deux impulsions, en secondes.")]
    [SerializeField] private float cooldown = 0.12f;

    [Header("Score")]
    [SerializeField] private int scoreValue = 100;

    [Header("Retour visuel")]
    [Tooltip("Rendus qui s'illuminent à l'impact. Vide : tous les enfants sont utilisés.")]
    [SerializeField] private Renderer[] flashRenderers;

    [SerializeField] private Color flashColor = new Color(1f, 0.85f, 0.25f);
    [SerializeField] private float flashDuration = 0.12f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly List<MaterialPropertyBlock> blocks = new List<MaterialPropertyBlock>();
    private readonly List<Color> baseColors = new List<Color>();
    private float lastKickTime = float.NegativeInfinity;
    private float flashEndTime;

    private void Awake()
    {
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

        if (ball == null)
        {
            return;
        }

        if (Time.time - lastKickTime < cooldown)
        {
            return;
        }

        lastKickTime = Time.time;

        // La normale du contact pointe vers la bille : on pousse dans l'axe inverse pour
        // l'éloigner de la bande, avec une composante verticale pour le « pop » du slingshot.
        Vector3 direction = collision.contactCount > 0
            ? -collision.GetContact(0).normal
            : transform.up;

        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        direction.y = upwardBias;

        ball.AddForce(direction.normalized * kickForce, ForceMode.Impulse);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.Add(scoreValue);
        }

        Flash();
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
        // Le pic d'émission est ignoré si le matériau ne l'active pas : on se contente alors
        // de la couleur de base, ce qui reste lisible sans réglage supplémentaire.
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

            block.SetColor(BaseColorId, baseColors[i]);
            target.SetPropertyBlock(block);
        }
    }
}

