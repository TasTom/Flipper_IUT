using UnityEngine;

/// <summary>
/// Lanceur à ressort (GDD §Bille et lanceur) : le joueur charge en maintenant la touche,
/// la puissance monte avec la durée d'appui, puis le relâchement propulse la bille.
///
/// <para><b>Deux sources, cumulatives</b>, comme les flippers : le <b>bouton start de la
/// borne</b> (action <c>GamePlayPF/LaunchBall</c> de <see cref="PinballControls"/>) et le
/// <b>clavier</b> via <see cref="InputRouter"/>. La borne n'a aucune liaison clavier dans son
/// asset : s'en remettre à elle seule rendrait le jeu injouable sans matériel.</para>
///
/// <para>Contrairement aux flippers, il n'y a <b>rien à configurer ici</b> : un lanceur n'a
/// qu'une action possible, <c>LaunchBall</c>. Les flippers avaient besoin d'un champ réglable
/// parce que gauche et droite sont symétriques et que le câblage d'une borne ne se déduit pas
/// du nom des actions ; un lanceur, lui, ne peut pas être « inversé ». Si le bouton start ne
/// répond pas, c'est la <i>liaison</i> de l'asset qu'il faut corriger, pas ce script.</para>
/// </summary>
public class Plunger : MonoBehaviour
{
    [Header("Charge")]
    [Tooltip("Vitesse de charge, par seconde.")]
    [SerializeField] private float pullSpeed = 3f;

    [Tooltip("Course maximale du lanceur, en unités.")]
    [SerializeField] private float maxPull = 0.8f;

    [Header("Lancement")]
    [Tooltip("Impulsion à pleine charge, en unités/s.")]
    [SerializeField] private float launchForce = 100f;   // 6 m/s réels — mesuré : la bille sort du couloir à 4,79 m/s

    [Tooltip("Demi-largeur de la zone de recherche, en travers du couloir.")]
    [SerializeField] private float catchHalfWidth = 0.5f;

    [Tooltip("Demi-hauteur de la zone de recherche, dans l'axe de la gravité de table.")]
    [SerializeField] private float catchHalfHeight = 0.5f;

    [Tooltip("Distance devant le BOUT DU BOUCHON jusqu'où la bille est cherchée.")]
    [SerializeField] private float catchOffset = 0.6f;

    [Header("Bouchon")]
    [Tooltip("Collider de la pièce qui touche réellement la bille. La zone de recherche se mesure "
           + "depuis SA géométrie, jamais depuis cet hôte — voir PushBalls. Laissé vide, il est "
           + "cherché dans le sous-arbre.")]
    [SerializeField] private Collider headCollider;

    [Tooltip("Délai avant de pouvoir relancer, en secondes.")]
    [SerializeField] private float relaunchDelay = 0.3f;

    [Header("Retour au repos")]
    [Tooltip("Vitesse de la course de retour du bouchon après le lancement, en unités/s. "
           + "Bornée au runtime : voir ReturnSpeed.")]
    [SerializeField] private float returnSpeed = 5f;

    [Header("Touche de repli")]
    [Tooltip("Utilisée seulement si la scène n'a pas d'InputRouter.")]
    [SerializeField] private KeyCode plungerKey = KeyCode.Space;

    [Header("Borne physique (Input System)")]
    [Tooltip("Lit GamePlayPF/LaunchBall sur le contrôleur (bornier xin-mo). " +
             "Le clavier reste actif en parallèle : les deux se cumulent.")]
    [SerializeField] private bool useCabinetController = true;

    [Header("Diagnostic")]
    [Tooltip("Journalise chaque appui et chaque relâchement, avec la source réelle (borne ou " +
             "clavier) et le contrôle physique. À décocher une fois le câblage vérifié.")]
    [SerializeField] private bool logInput = true;

    private Vector3 restPosition;
    private Vector3 restWorldPosition;

    /// <summary>
    /// Collider du bouchon, et son emprise AU REPOS. C'est depuis cette emprise que se mesure la
    /// zone de recherche des billes — jamais depuis la position de cet hôte.
    /// </summary>
    private Collider tete;
    private Bounds teteAuRepos;

    private float pullAmount;
    private bool launching;
    private PinballControls cabinet;          // actions de la borne, null si désactivée
    private bool heldLastFrame;               // état de l'appui à la frame précédente
    private string currentSource = "aucune";  // qui presse en ce moment
    private string lastSource = "aucune";     // qui a chargé en dernier, pour le log de relâche

    /// <summary>
    /// Avance maximale du bouchon par frame, en unités. 0,1 u = 6 mm, soit moins de la moitié
    /// du rayon de la bille (0,225). Voir <see cref="ReturnSpeed"/> pour le pourquoi.
    /// </summary>
    private const float ReturnStep = 0.1f;

    /// <summary>Charge courante, de 0 à 1.</summary>
    public float Charge => maxPull > 0f ? pullAmount / maxPull : 0f;

    /// <summary>
    /// Vitesse de la course de retour, bornée pour que le bouchon n'avance jamais de plus de
    /// <see cref="ReturnStep"/> par frame.
    ///
    /// Pourquoi une borne : au relâchement le bouchon est reculé de 0,8 u et la bille est posée
    /// contre sa face. Le remettre au repos d'un coup fait sauter le collider *dans* la bille —
    /// et PhysX résout un enfoncement profond par un désenfoncement positionnel, dans un sens qui
    /// dépend de quel côté de la boîte tombe le centre de la bille, donc parfois à contresens.
    /// Une course progressive forme toujours le contact par l'arrière : la bille ne peut qu'être
    /// poussée vers l'avant.
    ///
    /// La borne dépend de la durée de la frame, et non du pas physique : le bouchon est bougé
    /// depuis <see cref="Update"/>, donc une frame longue (hitch, chargement) ferait à elle seule
    /// l'avance qu'on cherche à interdire.
    /// </summary>
    private float ReturnSpeed
    {
        get
        {
            if (returnSpeed <= 0f) { return 0f; }

            return Mathf.Min(returnSpeed, ReturnStep / Mathf.Max(Time.deltaTime, 1e-4f));
        }
    }

    private void Awake()
    {
        restPosition = transform.localPosition;
        restWorldPosition = transform.position;

        // --- le bouchon -----------------------------------------------------------------------
        //
        // ⚠ Le bouchon n'est PAS forcément au même endroit que cet hôte, et cette différence a
        // coûté un lanceur muet. Mesuré sur `Neutral` : l'hôte était à x = 4,670 (dans l'aire de
        // jeu) alors que `Plunger_Rod/Rod`, qui porte le collider, était à x = 6,969 (dans le
        // couloir) — soit 2,299 u = 137,9 mm d'écart EN TRAVERS du couloir. Le bouchon est un
        // enfant décalé, et rien ne garantit qu'il soit à l'aplomb de son hôte.
        //
        // La zone de recherche se mesure donc depuis la GÉOMÉTRIE DU BOUCHON. C'est aussi ce qui
        // rend le script insensible à la longueur du bouchon et à l'endroit où tombe son origine.
        tete = headCollider != null ? headCollider : GetComponentInChildren<Collider>();

        if (tete == null)
        {
            Debug.LogWarning($"[Plunger] {name} : aucun collider dans le sous-arbre — le bouchon " +
                             "ne pourra pas pousser la bille. Poser le collider du bouchon sur un " +
                             "enfant de cet objet.", this);
        }
        else
        {
            teteAuRepos = tete.bounds;
        }

        // Chaque lanceur construit son propre exemplaire plutôt que de partager un statique :
        // `PinballControls` n'est qu'un emballage autour d'un JSON, et un statique survivrait
        // d'une session de Play à l'autre dans l'éditeur en gardant une action map périmée.
        if (useCabinetController)
        {
            cabinet = new PinballControls();

            Debug.Log($"[Plunger] {name} : borne='LaunchBall' ({CabinetPath()}), clavier=" +
                      (InputRouter.Instance != null ? "InputRouter" : plungerKey.ToString())
                      + (tete != null ? $", bouchon='{tete.name}'" : ", BOUCHON SANS COLLIDER"), this);
        }
    }

    /// <summary>
    /// Active l'action map de la borne. Sans cet appel les actions ne reçoivent rien : le nouvel
    /// Input System n'écoute pas une action laissée désactivée, et <c>IsPressed()</c> renverrait
    /// <c>false</c> en silence — la panne la plus difficile à diagnostiquer.
    /// </summary>
    private void OnEnable()
    {
        if (cabinet != null)
        {
            cabinet.GamePlayPF.Enable();
        }
    }

    private void OnDisable()
    {
        if (cabinet != null)
        {
            cabinet.GamePlayPF.Disable();
        }
    }

    private void OnDestroy()
    {
        if (cabinet == null)
        {
            return;
        }

        cabinet.GamePlayPF.Disable();

        // `DestroyImmediate` en toutes circonstances : `Dispose()` — qui appelle
        // `Object.Destroy` — produit « Destroy may not be called from edit mode » quand la
        // session de Play se termine. Mesuré sur `Flipper`, même motif.
        DestroyImmediate(cabinet.asset);

        cabinet = null;
    }

    private void Update()
    {
        // La source n'est interrogée qu'UNE fois par frame : `IsPressed()` au pas physique et
        // `Input.GetKey` dans `Update` ne verraient pas la même chose si on les appelait deux
        // fois, et la transition d'appui deviendrait incohérente.
        bool avant = heldLastFrame;
        heldLastFrame = PlungerHeld();

        if (logInput && heldLastFrame != avant)
        {
            if (heldLastFrame)
            {
                lastSource = currentSource;
                Debug.Log($"[Plunger] {name}  CHARGEMENT  <- {lastSource}", this);
            }
            else
            {
                Debug.Log($"[Plunger] {name}  relâché  (source : {lastSource})", this);
            }
        }

        if (launching)
        {
            // Course de retour : le bouchon revient au repos en poussant la bille devant lui,
            // au lieu de la traverser d'un coup. Voir ReturnSpeed pour la borne de vitesse.
            pullAmount = Mathf.MoveTowards(pullAmount, 0f, ReturnSpeed * Time.deltaTime);
        }
        else if (heldLastFrame)
        {
            pullAmount = Mathf.Clamp(pullAmount + pullSpeed * Time.deltaTime, 0f, maxPull);
        }
        else if (pullAmount > 0f)
        {
            Launch();
        }

        transform.localPosition = restPosition + Vector3.back * pullAmount;
    }

    private void Launch()
    {
        launching = true;

        // Une charge nulle (relâchement dans la même frame) ne doit pas lancer la bille :
        // elle partirait avec une puissance nulle et resterait dans le couloir.
        float power = maxPull > 0f ? pullAmount / maxPull : 0f;

        if (power > 0.05f)
        {
            PushBalls(power);
            NotifyLaunched();
        }

        // Le bouchon ne revient plus d'un coup : `pullAmount` décroît dans Update et `launching`
        // retombe quand la course est finie. Le délai de relance ne peut pas être plus court que
        // la course elle-même, sans quoi on rechargerait un bouchon encore en mouvement.
        float course = ReturnSpeed > 0f ? pullAmount / ReturnSpeed : 0f;

        Invoke(nameof(ResetLaunch), Mathf.Max(relaunchDelay, course));
    }

    private void PushBalls(float power)
    {
        if (tete == null)
        {
            return;
        }

        // La zone se mesure depuis l'emprise du BOUCHON, et non depuis cet hôte : les deux
        // peuvent être très éloignés l'un de l'autre (voir le commentaire de `Awake`).
        //
        // ⚠ On garde la position AU REPOS, capturee dans `Awake`. Au relachement le bouchon est
        // recule de `maxPull` et la bille peut etre restee en arriere : la zone doit couvrir
        // TOUTE la course, pas seulement la position courante.
        //
        // ⚠ La portee se mesure depuis le BOUT du bouchon, pas depuis son origine. Sur cette
        // piece l'origine du maillage tombe a une extremite : la bille se pose contre l'autre
        // bout, a 1,95 u de l'origine. Une portee comptee depuis l'origine ne l'aurait pas
        // atteinte — c'est la deuxieme moitie du meme defaut.
        var axe = transform.forward;
        var pointe = teteAuRepos.center + axe * Etendue(teteAuRepos.extents, axe);
        var reculee = pointe - axe * maxPull;
        var devant = pointe + axe * catchOffset;

        Vector3 center = (devant + reculee) * 0.5f;
        Vector3 halfExtents = new Vector3(catchHalfWidth, catchHalfHeight,
                                          (devant - reculee).magnitude * 0.5f);

        Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation);

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Ball"))
            {
                continue;
            }

            Rigidbody ball = hit.attachedRigidbody;

            if (ball == null)
            {
                continue;
            }

            // ⚠ La bille peut dormir : `Rigidbody.Sleep` la rend insensible a une impulsion
            // posee par un corps qui ne la touche pas. On la reveille avant de pousser, sans
            // quoi `AddForce` s'applique a un corps endormi et le lancer reste sans effet.
            if (ball.IsSleeping())
            {
                ball.WakeUp();
            }

            // Le plafond de vitesse de BallManager s'applique après coup : une charge maximale
            // peut donc être écrêtée, ce qui est voulu.
            ball.AddForce(transform.forward * launchForce * power, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// Demi-etendue d'une boite alignee sur le monde le long d'une direction : la somme des
    /// contributions de chaque axe. C'est le support d'une AABB dans la direction donnee.
    /// </summary>
    private static float Etendue(Vector3 extents, Vector3 direction)
    {
        return Mathf.Abs(extents.x * direction.x)
             + Mathf.Abs(extents.y * direction.y)
             + Mathf.Abs(extents.z * direction.z);
    }

    private void NotifyLaunched()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyBallLaunched();
        }
    }

    private void ResetLaunch()
    {
        // Filet de sécurité : `MoveTowards` peut s'arrêter à un cheveu du repos, et la position
        // locale est écrite à chaque frame depuis `pullAmount`.
        pullAmount = 0f;
        transform.localPosition = restPosition;
        launching = false;
    }

    /// <summary>
    /// Charge demandée par la borne ou par le clavier, et <b>se souvient de laquelle</b>.
    ///
    /// <para>La source est mémorisée dans <see cref="currentSource"/> : c'est ce qui permet au
    /// journal de dire <i>quel</i> bouton a répondu. Sans cela, un bouton start câblé sur le
    /// mauvais connecteur ne se distingue pas d'une touche Espace restée enfoncée.</para>
    /// </summary>
    private bool PlungerHeld()
    {
        if (cabinet != null && cabinet.GamePlayPF.LaunchBall.IsPressed())
        {
            currentSource = "BORNE " + CabinetPath();
            return true;
        }

        if (InputRouter.Instance != null)
        {
            if (InputRouter.Instance.PlungerHeld)
            {
                currentSource = "CLAVIER (InputRouter, lanceur)";
                return true;
            }

            currentSource = "aucune";
            return false;
        }

        if (plungerKey != KeyCode.None && Input.GetKey(plungerKey))
        {
            currentSource = "CLAVIER (" + plungerKey + ")";
            return true;
        }

        currentSource = "aucune";
        return false;
    }

    /// <summary>
    /// Chemin du contrôle physique écouté — par exemple
    /// <c>&lt;HID::xin-mo.com Xinmotek Controller&gt;/button4</c>.
    ///
    /// <para>C'est ce qui rend le diagnostic possible : le nom de l'action ne dit pas quel bouton
    /// le joueur doit presser, le chemin si. Affiché au démarrage et à chaque appui, il
    /// transforme « le bouton start ne fait rien » en une correspondance lisible.</para>
    /// </summary>
    private string CabinetPath()
    {
        if (cabinet == null)
        {
            return "aucune borne";
        }

        var action = cabinet.GamePlayPF.LaunchBall;

        if (action == null || action.bindings.Count == 0)
        {
            return "action non liée";
        }

        return action.bindings[0].path;
    }
}


