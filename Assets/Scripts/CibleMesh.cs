using System.Collections.Generic;
using UnityEngine;

public class CibleMesh : MonoBehaviour
{
    [Header("Effet")]
    [SerializeField] private int scoreValue = 150;
    [SerializeField] private float bounceForce = 90f;

    [Range(0f, 0.25f)]
    [SerializeField] private float upwardBias = 0.05f;

    [SerializeField] private float cooldown = 0.1f;

    [Header("Mission")]
    [Tooltip("Identifiant de matière. Vide : cette cible ne valide pas de mission.")]
    [SerializeField] private string subjectId;

    [Header("Retour visuel")]
    [SerializeField] private Renderer[] flashRenderers;
    [SerializeField] private Color flashColor = new Color(1f, 0.85f, 0.25f);
    [SerializeField] private float flashDuration = 0.12f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly List<MaterialPropertyBlock> blocks = new List<MaterialPropertyBlock>();
    private readonly List<Color> baseColors = new List<Color>();
    private float lastHitTime = float.NegativeInfinity;
    private float flashEndTime;

    private void Awake()
    {
        DisableLegacyTargets();

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

        if (ball == null || Time.time - lastHitTime < cooldown)
        {
            return;
        }

        lastHitTime = Time.time;

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

        if (!string.IsNullOrWhiteSpace(subjectId) && MissionManager.Instance != null)
        {
            MissionManager.Instance.CompleteSubject(subjectId);
        }

        Flash();
    }

    private void DisableLegacyTargets()
    {
        ScoreTarget legacyScoreTarget = GetComponentInParent<ScoreTarget>();
        if (legacyScoreTarget != null && legacyScoreTarget != this)
        {
            legacyScoreTarget.enabled = false;
        }

        SubjectTarget legacySubjectTarget = GetComponentInParent<SubjectTarget>();
        if (legacySubjectTarget != null)
        {
            legacySubjectTarget.enabled = false;
        }
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

    private void Flash()
    {
        flashEndTime = Time.time + flashDuration;

        int index = 0;
        foreach (Renderer target in flashRenderers)
        {
            if (target == null)
            {
                continue;
            }

            MaterialPropertyBlock block = blocks[index++];
            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, flashColor);
            block.SetColor(EmissionColorId, flashColor);
            target.SetPropertyBlock(block);
        }
    }

    private void RestoreColors()
    {
        int index = 0;
        foreach (Renderer target in flashRenderers)
        {
            if (target == null)
            {
                continue;
            }

            MaterialPropertyBlock block = blocks[index++];
            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, baseColors[index - 1]);
            block.SetColor(EmissionColorId, Color.black);
            target.SetPropertyBlock(block);
        }

        flashEndTime = 0f;
    }

    private static Color ReadBaseColor(Renderer target)
    {
        Material material = target.sharedMaterial;
        return material != null && material.HasProperty(BaseColorId)
            ? material.GetColor(BaseColorId)
            : Color.white;
    }
}