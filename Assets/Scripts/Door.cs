using UnityEngine;

/// <summary>
/// Porte « IUT » (GDD §Loops et portes) : un obstacle qui s'ouvre après validation de certaines
/// cibles, et dont le franchissement donne un bonus.
///
/// <para>La porte est <b>solide tant qu'elle est fermée</b> et <b>traversante une fois
/// ouverte</b>. C'est le même collider qui fait les deux, en basculant <c>isTrigger</c> : un
/// collider solide arrête la bille, un déclencheur la laisse passer <i>et</i> la détecte. Poser
/// deux colliders séparés obligerait à les garder synchronisés, pour aucun gain.</para>
///
/// <para><b>L'ouverture est définitive.</b> Le GDD décrit une porte qui s'ouvre « après avoir
/// validé certaines cibles » et permet alors de « comprendre visuellement qu'une nouvelle
/// opportunité vient d'être débloquée ». Une porte qui se refermerait effacerait ce signal, et
/// le joueur n'aurait aucun moyen de savoir qu'il a perdu son acquis.</para>
/// </summary>
/// <remarks>
/// Pas de <c>[RequireComponent(typeof(Collider))]</c>, même motif que <see cref="DrainZone"/> :
/// la scène neutre doit pouvoir exister avec l'hôte lié mais sans collider.
/// </remarks>
public class Door : MonoBehaviour
{
    [Header("Battant")]
    [Tooltip("La pièce qui coulisse à l'ouverture. Laissée vide, le premier enfant est utilisé. " +
             "Sans battant, seule la collision change — la porte fonctionne quand même.")]
    [SerializeField] private Transform panel;

    [Tooltip("Déplacement du battant à l'ouverture, dans les axes de la porte.")]
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 0f, -1.2f);

    [Tooltip("Vitesse de coulissement du battant, en unités/s.")]
    [SerializeField] private float slideSpeed = 4f;

    [Header("État")]
    [Tooltip("La porte commence-t-elle ouverte ? Coché, elle ne bloque rien tant qu'on ne l'a " +
             "pas refermée — utile pour tester la table sans avoir à valider les cibles.")]
    [SerializeField] private bool startsOpen;

    [Header("Récompense")]
    [Tooltip("Points versés au franchissement, une fois par passage. GDD §Barème : un " +
             "franchissement de porte est un « bonus important », calé ici entre la rampe IUT " +
             "(10 000) et une série de cibles complétée (15 000).")]
    [SerializeField] private int bonusPoints = 12000;

    [Tooltip("Message affiché au franchissement. Vide = aucun message.")]
    [SerializeField] private string displayName = "PORTE IUT";

    [Tooltip("Durée d'affichage du message, en secondes.")]
    [SerializeField] private float messageDuration = 1.5f;

    private Collider barrier;
    private Vector3 panelClosedPosition;
    private Vector3 panelOpenPosition;
    private bool isOpen;

    /// <summary>Émis quand la porte s'ouvre. Ne se déclenche qu'une fois : l'ouverture est définitive.</summary>
    public event System.Action<Door> Opened;

    /// <summary>Émis à chaque franchissement, avec la bille qui est passée.</summary>
    public event System.Action<Door, Collider> Passed;

    /// <summary>La porte est-elle ouverte ?</summary>
    public bool IsOpen => isOpen;

    /// <summary>Nom affiché de la porte, tel que posé sur l'hôte.</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    private void Reset()
    {
        // Une porte est un obstacle : solide par défaut. C'est `Open` qui la rend traversante.
        Collider own = GetComponent<Collider>();

        if (own != null)
        {
            own.isTrigger = false;
        }
    }

    private void Awake()
    {
        barrier = GetComponent<Collider>();

        if (barrier == null)
        {
            Debug.LogWarning($"[Door] '{name}' n'a pas de collider : la porte ne bloquera pas la " +
                             "bille, et aucun franchissement ne sera détecté.", this);
        }

        if (panel == null && transform.childCount > 0)
        {
            panel = transform.GetChild(0);
        }

        if (panel != null)
        {
            panelClosedPosition = panel.localPosition;
            panelOpenPosition = panelClosedPosition + openOffset;
        }

        // L'état de départ est posé sans animation : une porte qui commencerait ouverte ne doit
        // pas coulisser sous les yeux du joueur au premier pas de la partie.
        SetOpen(startsOpen, instant: true);
    }

    /// <summary>Ouvre la porte. Sans effet si elle l'est déjà — l'ouverture est définitive.</summary>
    public void Open()
    {
        if (isOpen)
        {
            return;
        }

        SetOpen(true, instant: false);
        Opened?.Invoke(this);

        Collider own = barrier != null ? barrier : GetComponent<Collider>();

        if (own != null && !own.isTrigger)
        {
            Debug.LogWarning($"[Door] '{name}' est ouverte mais son collider reste solide : la " +
                             "bille ne pourra pas la franchir.", this);
        }
    }

    /// <summary>
    /// Referme la porte. Réservé au redémarrage d'une partie et aux tests : en jeu, une porte
    /// ouverte le reste (voir le commentaire de classe).
    /// </summary>
    public void Close()
    {
        SetOpen(false, instant: false);
    }

    /// <summary>Remet la porte dans son état de départ.</summary>
    public void ResetDoor()
    {
        SetOpen(startsOpen, instant: true);
    }

    private void SetOpen(bool open, bool instant)
    {
        isOpen = open;

        // Le collider bascule : solide fermée, déclencheur ouverte. C'est ce seul booléen qui
        // fait la différence entre « la bille rebondit » et « la bille passe ».
        Collider own = barrier != null ? barrier : GetComponent<Collider>();

        if (own != null)
        {
            own.isTrigger = open;
        }

        if (panel == null)
        {
            return;
        }

        if (instant)
        {
            panel.localPosition = open ? panelOpenPosition : panelClosedPosition;
        }
    }

    private void Update()
    {
        if (panel == null)
        {
            return;
        }

        // Le battant coulisse vers sa cote d'ouverture ou de fermeture. Fait dans `Update` et
        // non dans `FixedUpdate` : c'est du décor, il n'a pas à suivre le pas physique — et le
        // faire suivre obligerait à animer un kinematic, ce que le projet évite ailleurs.
        Vector3 cible = isOpen ? panelOpenPosition : panelClosedPosition;

        if ((panel.localPosition - cible).sqrMagnitude > 1e-6f)
        {
            panel.localPosition = Vector3.MoveTowards(panel.localPosition, cible,
                                                      slideSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Un collider solide (porte fermée) ne déclenche jamais ce rappel : y arriver prouve
        // déjà que la porte est ouverte. Le test est là pour le cas où un collider resterait en
        // déclencheur sans que la porte soit ouverte.
        if (!isOpen || !other.CompareTag("Ball"))
        {
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(bonusPoints);

            if (!string.IsNullOrEmpty(displayName))
            {
                GameManager.Instance.ShowMessage(displayName, messageDuration);
            }
        }

        Passed?.Invoke(this, other);
    }
}
