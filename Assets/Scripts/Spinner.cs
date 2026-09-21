using UnityEngine;

/// <summary>
/// Ailette du spinner. La bille la frappe, elle tourne autour de son axe vertical
/// (HingeJoint freeSpin posé sur le même objet), et chaque impact rapporte des points.
/// </summary>
/// <remarks>
/// Collider solide, pas un déclencheur : c'est le contact physique qui fait tourner
/// l'ailette. Le comptage se fait donc en <c>OnCollisionEnter</c>, avec un court délai
/// anti-rebond — un même contact peut générer plusieurs entrées si l'ailette vibre.
/// </remarks>
public class Spinner : MonoBehaviour
{
    [Header("Récompense")]
    [Tooltip("Points par impact de bille sur l'ailette.")]
    [SerializeField] private int points = 500;

    [Header("Anti-rebond")]
    [Tooltip("Délai minimal entre deux comptages, en secondes.")]
    [SerializeField] private float cooldown = 0.15f;

    private float lastCountedAt = float.NegativeInfinity;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ball"))
        {
            return;
        }

        if (Time.time - lastCountedAt < cooldown)
        {
            return;
        }

        lastCountedAt = Time.time;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(points);
        }
    }
}
