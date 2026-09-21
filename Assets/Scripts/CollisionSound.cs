using UnityEngine;

/// <summary>
/// Joue un effet sonore quand la bille touche cet objet (GDD §Audio).
///
/// <para>Composant réutilisable : on le pose sur un bumper, un slingshot, une cible, le
/// spinner… sans modifier leur script. C'est lui qui fait le lien entre les collisions du
/// gameplay et <see cref="AudioManager.Play(string)"/>.</para>
///
/// <para>Silencieux par défaut : si <see cref="AudioManager"/> est absent ou si le nom
/// n'est pas au registre, rien ne se passe. Le gameplay ne dépend jamais de l'audio.</para>
/// </summary>
public class CollisionSound : MonoBehaviour
{
    [Header("Son")]
    [Tooltip("Nom du clip au registre de AudioManager. Convention : 'bumper', 'slingshot', " +
             "'target', 'flipper', 'drain', 'spinner', 'gate', 'bonus'.")]
    [SerializeField] private string soundName = "impact";

    [Tooltip("Écouter les déclencheurs (OnTriggerEnter) au lieu des collisions solides.")]
    [SerializeField] private bool onTrigger;

    [Header("Anti-rebond")]
    [Tooltip("Délai minimal entre deux lectures, en secondes. Un même contact peut générer " +
             "plusieurs événements si la bille roule sur l'objet.")]
    [SerializeField] private float cooldown = 0.08f;

    private float lastPlayedAt = float.NegativeInfinity;

    private void OnCollisionEnter(Collision collision)
    {
        if (onTrigger)
        {
            return;
        }

        Fire(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!onTrigger)
        {
            return;
        }

        Fire(other.gameObject);
    }

    private void Fire(GameObject other)
    {
        if (!other.CompareTag("Ball"))
        {
            return;
        }

        if (Time.time - lastPlayedAt < cooldown)
        {
            return;
        }

        lastPlayedAt = Time.time;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(soundName);
        }
    }
}
