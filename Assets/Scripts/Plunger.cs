using UnityEngine;

/// <summary>
/// Lanceur à ressort (GDD §Bille et lanceur) : le joueur charge en maintenant la touche,
/// la puissance monte avec la durée d'appui, puis le relâchement propulse la bille.
///
/// Passe par <see cref="InputRouter"/> pour la touche, ce qui garde la table des contrôles
/// du GDD en un seul endroit.
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
    [SerializeField] private float launchForce = 18f;

    [Tooltip("Demi-largeur de la zone de recherche, en travers du couloir.")]
    [SerializeField] private float catchHalfWidth = 0.5f;

    [Tooltip("Distance devant la position de repos jusqu'où la bille est cherchée.")]
    [SerializeField] private float catchOffset = 0.6f;

    [Tooltip("Délai avant de pouvoir relancer, en secondes.")]
    [SerializeField] private float relaunchDelay = 0.3f;

    [Header("Retour au repos")]
    [Tooltip("Vitesse de la course de retour du bouchon après le lancement, en unités/s. "
           + "Bornée au runtime : voir ReturnSpeed.")]
    [SerializeField] private float returnSpeed = 5f;

    [Header("Touche de repli")]
    [Tooltip("Utilisée seulement si la scène n'a pas d'InputRouter.")]
    [SerializeField] private KeyCode plungerKey = KeyCode.Space;

    private Vector3 restPosition;
    private Vector3 restWorldPosition;
    private float pullAmount;
    private bool launching;

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
    }

    private void Update()
    {
        if (launching)
        {
            // Course de retour : le bouchon revient au repos en poussant la bille devant lui,
            // au lieu de la traverser d'un coup. Voir ReturnSpeed pour la borne de vitesse.
            pullAmount = Mathf.MoveTowards(pullAmount, 0f, ReturnSpeed * Time.deltaTime);
        }
        else if (PlungerHeld())
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
        // La zone de recherche couvre toute la course du lanceur, de sa position reculée
        // jusqu'à catchOffset devant sa position de repos : au relâchement, la bille a suivi
        // le lanceur vers l'arrière et ne se trouve donc pas devant sa position courante.
        Vector3 front = restWorldPosition + transform.forward * catchOffset;
        Vector3 back = restWorldPosition - transform.forward * maxPull;

        Vector3 center = (front + back) * 0.5f;
        Vector3 halfExtents = new Vector3(catchHalfWidth, 0.5f, (front - back).magnitude * 0.5f);

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

            // Le plafond de vitesse de BallManager s'applique après coup : une charge maximale
            // peut donc être écrêtée, ce qui est voulu.
            ball.AddForce(transform.forward * launchForce * power, ForceMode.Impulse);
        }
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

    private bool PlungerHeld()
    {
        if (InputRouter.Instance != null)
        {
            return InputRouter.Instance.PlungerHeld;
        }

        return plungerKey != KeyCode.None && Input.GetKey(plungerKey);
    }
}
