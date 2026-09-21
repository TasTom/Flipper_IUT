using UnityEngine;

/// <summary>Bout d'une voie rapide, du point de vue de la bille.</summary>
public enum LoopEnd
{
    /// <summary>L'entrée : la bille s'y engage.</summary>
    Entree,

    /// <summary>La sortie : la bille en ressort, ce qui valide un tour.</summary>
    Sortie
}

/// <summary>
/// Un bout de loop (GDD §Loops et portes). Même appariement que <see cref="RampGate"/>, mais
/// avec deux différences qui tiennent au rôle du loop dans le GDD.
///
/// <para><b>Il compte les tours.</b> La mission 5 « Réseau connecté » se valide en réussissant le
/// loop @ <b>trois fois</b> (GDD §Missions). Un seul passage ne suffit donc pas, et le décompte
/// doit survivre à la bille : c'est le <see cref="Completions"/> du <i>groupe</i>, pas d'une
/// bille.</para>
///
/// <para><b>Le compteur vit sur l'entrée.</b> Les deux bouts d'un loop sont deux objets ; le
/// compte est posé sur celui d'entrée pour qu'il n'y ait qu'une source de vérité, et il est lu
/// depuis les deux. Compter sur les deux doublerait chaque passage.</para>
///
/// <para><b>Barème (GDD §Barème)</b> : 5 000 points par passage.</para>
/// </summary>
/// <remarks>
/// Pas de <c>[RequireComponent(typeof(Collider))]</c>, même motif que <see cref="DrainZone"/> :
/// la scène neutre doit pouvoir exister avec l'hôte lié mais sans collider.
/// </remarks>
public class LoopGate : MonoBehaviour
{
    [Header("Rôle")]
    [Tooltip("Ce bout est-il l'entrée ou la sortie du loop ?")]
    [SerializeField] private LoopEnd end = LoopEnd.Entree;

    [Tooltip("L'autre bout du MÊME loop. Obligatoire : sans lui, aucun tour n'est compté.")]
    [SerializeField] private LoopGate partner;

    [Header("Récompense")]
    [Tooltip("Points par tour réussi. GDD §Barème : 5 000.")]
    [SerializeField] private int points = 5000;

    [Tooltip("Message affiché au joueur. Vide = aucun message.")]
    [SerializeField] private string displayName = "LOOP @";

    [Tooltip("Durée d'affichage du message, en secondes.")]
    [SerializeField] private float messageDuration = 1.5f;

    [Header("Fenêtre")]
    [Tooltip("Délai maximum entre l'entrée et la sortie, en secondes.")]
    [SerializeField] private float window = 8f;

    private float armedAt = float.NegativeInfinity;
    private int completions;
    private bool warned;

    /// <summary>Émis à chaque tour réussi, avec le nombre total de tours.</summary>
    public event System.Action<LoopGate, Collider, int> Completed;

    /// <summary>Nombre de tours réussis depuis le début de la partie.</summary>
    public int Completions => CompletionsOf(this);

    /// <summary>Ce bout est-il l'entrée ?</summary>
    public bool IsEntry => end == LoopEnd.Entree;

    /// <summary>L'autre bout du loop.</summary>
    public LoopGate Partner => partner;

    /// <summary>Nom affiché du loop, tel que posé sur l'hôte.</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    /// <summary>
    /// Nombre de tours du loop auquel appartient ce bout. Remonte à l'entrée, qui seule porte le
    /// compteur : interroger la sortie rend donc le même chiffre que l'entrée.
    /// </summary>
    public static int CompletionsOf(LoopGate gate)
    {
        if (gate == null)
        {
            return 0;
        }

        LoopGate entree = gate.end == LoopEnd.Entree ? gate : gate.partner;

        return entree != null ? entree.completions : 0;
    }

    /// <summary>Remet le compteur à zéro. Appelé au début d'une partie.</summary>
    public void ResetCompletions()
    {
        completions = 0;
        armedAt = float.NegativeInfinity;
    }

    private void Reset()
    {
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
            Debug.LogWarning($"[LoopGate] '{name}' n'a pas de collider : aucune bille ne " +
                             "traversera ce bout de loop.", this);
        }
        else if (!own.isTrigger)
        {
            Debug.LogWarning($"[LoopGate] Le collider de '{name}' n'est pas un déclencheur : " +
                             "la bille butera dessus au lieu de s'engager dans le loop.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball"))
        {
            return;
        }

        if (end == LoopEnd.Entree)
        {
            armedAt = Time.time;
            return;
        }

        Validate(other);
    }

    /// <summary>
    /// Appelé par le bout de SORTIE. Il interroge son entrée, comme <see cref="RampGate"/>, et
    /// incrémente le compteur de l'entrée — pas le sien, qui resterait à zéro.
    /// </summary>
    private void Validate(Collider ball)
    {
        if (partner == null)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning($"[LoopGate] La sortie '{name}' n'a pas d'entrée appariée : " +
                                 "aucun tour ne sera compté. Renseigner le champ 'partner'.", this);
            }

            return;
        }

        if (partner.end != LoopEnd.Entree)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning($"[LoopGate] '{name}' a pour partenaire '{partner.name}', qui " +
                                 "n'est pas une entrée : les deux bouts doivent être de rôle " +
                                 "opposé.", this);
            }

            return;
        }

        if (Time.time - partner.armedAt > window)
        {
            return;
        }

        partner.armedAt = float.NegativeInfinity;
        partner.completions++;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(points);

            if (!string.IsNullOrEmpty(displayName))
            {
                GameManager.Instance.ShowMessage($"{displayName}  {partner.completions}", messageDuration);
            }
        }

        Completed?.Invoke(this, ball, partner.completions);

        // La mission 5 « Réseau connecté » se valide en réussissant le loop @ trois fois
        // (GDD §Missions). Le total part du début de la partie : c'est MissionManager qui tient
        // le compteur de SA mission, pour qu'une mission lancée après deux tours ne reparte pas
        // de zéro — ni ne se termine sur le premier tour qui suit.
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.NotifyLoopCompleted(partner.completions);
        }

        // GDD §Boss, étape « Correction » : « Réussir une rampe IUT ou le loop @ ». Il n'y a qu'un
        // loop sur la table, donc pas de drapeau à poser — tout tour de loop corrige.
        if (BossTarget.Instance != null)
        {
            BossTarget.Instance.NotifyCorrection();
        }
    }
}
