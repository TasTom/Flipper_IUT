using UnityEngine;

/// <summary>
/// Joue un effet sonore quand la bille touche cet objet (GDD §Audio).
///
/// <para>Composant réutilisable : on le pose sur un bumper, un slingshot, une cible, un
/// spinner… sans modifier leur script. C'est lui qui fait le lien entre les collisions du
/// gameplay et <see cref="AudioManager.Play(string, float, float)"/>.</para>
///
/// <para><b>Réponse à l'impact.</b> Un bumper effleuré à 15 u/s et un bumper frappé à 80 u/s
/// ne rendent pas le même son. Ce composant lit donc la vitesse relative du contact et
/// choisit un clip dans <see cref="variants"/> — du plus faible au plus fort — puis dose le
/// volume. Sans cette partie tous les chocs de la table sonneraient identique, ce qui est le
/// défaut le plus audible d'un flipball.</para>
///
/// <para>Les variantes se nomment comme le registre d'<see cref="AudioManager"/> : un nom
/// absent est écarté silencieusement, et s'il ne reste rien de jouable on retombe sur
/// <see cref="soundName"/>. Silencieux par défaut : le gameplay ne dépend jamais de l'audio.</para>
/// </summary>
public class CollisionSound : MonoBehaviour
{
    [Header("Son")]
    [Tooltip("Nom du clip au registre de AudioManager. Convention : 'bumper', 'slingshot', " +
             "'target', 'flipper', 'drain', 'spinner', 'gate', 'bonus'.")]
    [SerializeField] private string soundName = "impact";

    [Tooltip("Écouter les déclencheurs (OnTriggerEnter) au lieu des collisions solides. " +
             "Un déclencheur n'a pas de vitesse de contact : les variantes sont alors ignorées.")]
    [SerializeField] private bool onTrigger;

    [Header("Réponse à l'impact")]
    [Tooltip("Clips du plus faible au plus fort : le premier pour un simple effleurement, " +
             "le dernier pour un coup franc. Vide : seul soundName est joué.")]
    [SerializeField] private string[] variants;

    [Tooltip("En dessous de cette vitesse d'impact (u/s), aucun son : une bille qui roule " +
             "n'a pas claqué le bumper, elle l'a effleuré.")]
    [SerializeField] private float silentBelowSpeed = 6f;

    [Tooltip("Vitesse d'impact (u/s) qui joue la première variante.")]
    [SerializeField] private float weakestSpeed = 18f;

    [Tooltip("Vitesse d'impact (u/s) qui joue la dernière variante. Au-delà, elle est jouée " +
             "en permanence. Doit dépasser weakestSpeed.")]
    [SerializeField] private float strongestSpeed = 55f;

    [Header("Anti-rebond")]
    [Tooltip("Délai minimal entre deux lectures, en secondes. Un même contact peut générer " +
             "plusieurs événements si la bille roule sur l'objet.")]
    [SerializeField] private float cooldown = 0.08f;

    [Tooltip("Écart de hauteur aléatoire, en demi-tons. Deux bumper voisins ne doivent pas " +
             "sonner rigoureusement pareils, comme trois solénoïdes réels.")]
    [SerializeField] [Range(0f, 0.5f)] private float pitchJitter = 0.12f;

    private float lastPlayedAt = float.NegativeInfinity;

    private void OnCollisionEnter(Collision collision)
    {
        if (onTrigger)
        {
            return;
        }

        // Only the normal component is an impact; sliding along a surface is not a strike.
        float speed = collision.contactCount > 0
            ? Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal))
            : collision.relativeVelocity.magnitude;
        Fire(collision.gameObject, speed);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!onTrigger)
        {
            return;
        }

        Fire(other.gameObject, 0f);
    }

    private void Fire(GameObject other, float impactSpeed)
    {
        if (!other.CompareTag("Ball"))
        {
            return;
        }

        if (Time.time - lastPlayedAt < cooldown)
        {
            return;
        }

        var audio = AudioManager.Instance;

        if (audio == null)
        {
            return;
        }
        if (!onTrigger && impactSpeed <= silentBelowSpeed) return;

        string clip = soundName;
        float volume = 1f;

        // Un déclencheur passe ici avec impactSpeed = 0 : il n'a pas de vitesse de contact,
        // donc pas d'intensité à graduer. Une porte qui s'ouvre sonne pareil à chaque fois.
        if (!onTrigger && impactSpeed > silentBelowSpeed)
        {
            if (!PickVariant(impactSpeed, audio, ref clip))
            {
                return;
            }

            // Le volume suit l'intensité, mais en racine : à mi-plage de vitesse le son
            // doit rester franc, pas à moitié éteint.
            float t = ImpactRatio(impactSpeed);
            volume = Mathf.Lerp(0.45f, 1f, Mathf.Sqrt(t));
        }

        lastPlayedAt = Time.time;

        float jitter = Mathf.Pow(2f, Random.Range(-pitchJitter, pitchJitter) / 12f);
        audio.Play(clip, volume, jitter);
    }

    /// <summary>Position de la vitesse d'impact dans la plage utile, bornée à [0 ; 1].</summary>
    private float ImpactRatio(float impactSpeed)
    {
        return Mathf.InverseLerp(weakestSpeed, Mathf.Max(weakestSpeed + 0.01f, strongestSpeed), impactSpeed);
    }

    /// <summary>
    /// Choisit la variante correspondant à la vitesse du choc, et l'écrit dans
    /// <paramref name="clip"/>. Rend faux si rien n'est jouable.
    /// </summary>
    private bool PickVariant(float impactSpeed, AudioManager audio, ref string clip)
    {
        var names = variants;

        if (names == null || names.Length == 0)
        {
            return audio.HasClip(clip);
        }

        // Seules les variantes réellement présentes au registre comptent : un clip manquant
        // ne doit pas faire choisir une variante plus faible que celle du choc.
        int jouables = 0;
        for (int i = 0; i < names.Length; i++)
        {
            if (audio.HasClip(names[i]))
            {
                jouables++;
            }
        }

        if (jouables == 0)
        {
            return audio.HasClip(clip);
        }

        int wanted = Mathf.Min(jouables - 1, Mathf.RoundToInt(ImpactRatio(impactSpeed) * (jouables - 1)));

        // `wanted` est un rang parmi les jouables : il faut le traduire en index réel.
        for (int i = 0, vus = 0; i < names.Length; i++)
        {
            if (!audio.HasClip(names[i]))
            {
                continue;
            }

            if (vus++ == wanted)
            {
                clip = names[i];
                return true;
            }
        }

        return false;
    }
}
