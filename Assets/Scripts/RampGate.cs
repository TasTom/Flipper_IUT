using UnityEngine;

/// <summary>Bout d'une rampe, du point de vue de la bille.</summary>
public enum RampEnd
{
    /// <summary>L'entrée : la bille s'y engage.</summary>
    Entree,

    /// <summary>La sortie : la bille en ressort, ce qui valide la rampe.</summary>
    Sortie
}

/// <summary>
/// Un bout de rampe (GDD §Rampes). Deux exemplaires s'apparient — <c>Ramp_Vosges_In</c> et
/// <c>Ramp_Vosges_Out</c> — et une rampe est réussie quand la bille entre par l'un et ressort
/// par l'autre dans la fenêtre de temps.
///
/// <para>Les deux bouts sont deux objets distincts parce que c'est ainsi qu'on les pose : on
/// place un déclencheur en bas de rampe et un en haut. Le script ne se contente pas d'un
/// <c>OnTriggerEnter</c> sur un seul objet : une bille qui entre dans l'entrée puis
/// <i>redescend</i> sans monter (rampe ratée, bille qui recule) ne doit pas marquer la rampe.
/// C'est la paire qui fait la réussite, pas le passage.</para>
///
/// <para><b>Barème (GDD §Barème)</b> : 7 500 points pour la rampe Vosges, 10 000 pour la rampe
/// IUT. La valeur est un champ, pas une constante : le script ne peut pas savoir laquelle des
/// deux rampes il équipe, et l'écrire en dur obligerait à dupliquer le composant.</para>
/// </summary>
/// <remarks>
/// Pas de <c>[RequireComponent(typeof(Collider))]</c> : la scène neutre doit pouvoir exister avec
/// l'hôte lié mais sans collider. Le composant le signale au démarrage au lieu de lever une
/// NullReferenceException — même motif que <see cref="DrainZone"/>.
/// </remarks>
public class RampGate : MonoBehaviour
{
    [Header("Rôle")]
    [Tooltip("Ce bout est-il l'entrée ou la sortie de la rampe ?")]
    [SerializeField] private RampEnd end = RampEnd.Entree;

    [Tooltip("L'autre bout de la MÊME rampe. Obligatoire : sans lui, aucune rampe ne peut " +
             "être validée. L'entrée arme, la sortie valide.")]
    [SerializeField] private RampGate partner;

    [Header("Récompense")]
    [Tooltip("Points versés quand la rampe est réussie. GDD §Barème : 7 500 (Vosges), " +
             "10 000 (IUT).")]
    [SerializeField] private int points = 7500;

    [Tooltip("Message affiché au joueur. Vide = aucun message.")]
    [SerializeField] private string displayName = "RAMPE";

    [Tooltip("Durée d'affichage du message, en secondes.")]
    [SerializeField] private float messageDuration = 1.5f;

    [Header("Fenêtre")]
    [Tooltip("Délai maximum entre l'entrée et la sortie, en secondes. Au-delà, l'entrée est " +
             "considérée périmée : la bille a traîné sur la table et sa sortie ne valide rien.")]
    [SerializeField] private float window = 8f;

    [Header("Boss")]
    [Tooltip("Réussir cette rampe corrige le bug du boss (GDD §Boss : « Réussir une rampe IUT ou " +
             "le loop @ »). À cocher sur la rampe IUT, PAS sur la Vosges.")]
    [SerializeField] private bool corrigeLeBug;

    private float armedAt = float.NegativeInfinity;
    private bool warned;

    /// <summary>Émis quand une rampe est réussie. Le nom de la rampe et la bille sont fournis.</summary>
    public event System.Action<RampGate, Collider> Completed;

    /// <summary>Ce bout est-il l'entrée ?</summary>
    public bool IsEntry => end == RampEnd.Entree;

    /// <summary>L'autre bout de la rampe.</summary>
    public RampGate Partner => partner;

    /// <summary>Nom affiché de la rampe, tel que posé sur l'hôte.</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    private void Reset()
    {
        // Un bout de rampe se traverse : en solide, il bloquerait la bille au lieu de la laisser
        // passer. C'est vrai des deux bouts.
        Collider own = GetComponent<Collider>();

        if (own != null)
        {
            own.isTrigger = true;
        }
    }

    private void Awake()
    {
        Collider own = GetComponent<Collider>();

        if (own == null)
        {
            Debug.LogWarning($"[RampGate] '{name}' n'a pas de collider : aucune bille ne " +
                             "traversera ce bout de rampe.", this);
        }
        else if (!own.isTrigger)
        {
            Debug.LogWarning($"[RampGate] Le collider de '{name}' n'est pas un déclencheur : " +
                             "la bille butera dessus au lieu de s'engager dans la rampe.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball"))
        {
            return;
        }

        if (end == RampEnd.Entree)
        {
            // On arme sans condition : c'est la SORTIE qui jugera si la rampe a été faite.
            armedAt = Time.time;
            return;
        }

        Validate(other);
    }

    /// <summary>
    /// Appelé par le bout de SORTIE. Il interroge son entrée : si elle a été armée récemment,
    /// la bille a bien parcouru la rampe.
    /// </summary>
    private void Validate(Collider ball)
    {
        if (partner == null)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning($"[RampGate] La sortie '{name}' n'a pas d'entrée appariée : " +
                                 "aucune rampe ne sera validée. Renseigner le champ 'partner'.", this);
            }

            return;
        }

        if (!partner.TryConsume(Time.time))
        {
            // Deux cas, tous deux normaux : l'entrée n'a jamais été armée (la bille est arrivée
            // par le haut de la table), ou elle l'a été il y a trop longtemps.
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(points);

            if (!string.IsNullOrEmpty(displayName))
            {
                GameManager.Instance.ShowMessage(displayName, messageDuration);
            }
        }

        Completed?.Invoke(this, ball);

        // La mission désigne la rampe par le nom de son ENTRÉE (`Ramp_IUT_In`) : c'est l'entrée
        // qui porte le nom de la rampe dans la scène, la sortie n'est qu'un point de passage.
        // On remonte donc à l'entrée depuis la sortie qui vient de valider.
        RampGate entree = IsEntry ? this : partner;

        if (MissionManager.Instance != null && entree != null)
        {
            MissionManager.Instance.NotifyRampCompleted(entree.name);
        }

        // Le boss se corrige par une rampe précise, pas par n'importe laquelle : le GDD nomme la
        // rampe IUT. Le drapeau est sur le script, donc c'est la rampe qui sait si elle corrige.
        if (corrigeLeBug && BossTarget.Instance != null)
        {
            BossTarget.Instance.NotifyCorrection();
        }
    }

    /// <summary>
    /// Consomme l'armement de l'entrée si elle est encore dans la fenêtre, et rend <c>true</c>
    /// dans ce cas. Consommer plutôt que lire est délibéré : une bille qui repasserait par la
    /// sortie sans reprendre l'entrée ne doit pas compter une seconde rampe.
    /// </summary>
    private bool TryConsume(float now)
    {
        if (now - armedAt > window)
        {
            return false;
        }

        armedAt = float.NegativeInfinity;
        return true;
    }
}
