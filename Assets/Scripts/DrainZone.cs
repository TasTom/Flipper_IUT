using UnityEngine;

/// <summary>
/// Zone de sortie (GDD §Zone de sortie) : une bille qui l'atteint est perdue.
///
/// Le composant ne détruit rien lui-même : il délègue à <see cref="BallManager"/>, qui est le
/// seul à savoir quelles billes sont en jeu. Il retombe sur <see cref="GameManager"/> si la
/// scène n'a pas encore de gestionnaire de billes.
/// </summary>
/// <remarks>
/// Pas de <c>[RequireComponent(typeof(Collider))]</c> : la scène neutre doit pouvoir exister
/// avant les assets, avec le script lié mais sans collider. Le composant, lui, déréférence
/// bien son collider — d'où les gardes dans <see cref="Reset"/> et <see cref="Awake"/>, qui
/// signalent l'absence au lieu de lever une NullReferenceException.
/// </remarks>
public class DrainZone : MonoBehaviour
{
    [Tooltip("Délai avant de compter la bille perdue, en secondes. Évite qu'une bille qui " +
             "frôle la zone en ressortant soit comptée à tort.")]
    [SerializeField] private float graceDelay;

    private Collider pendingBall;
    private Collider ownCollider;
    private Vector3 baseColliderSize;
    private bool lateralRescueEnabled;
    private float lateralRescueForce;

    private void Reset()
    {
        // Le collider doit être un déclencheur : un collider solide à cet endroit empêcherait
        // la bille de tomber et bloquerait la partie.
        Collider own = GetComponent<Collider>();

        if (own != null)
        {
            own.isTrigger = true;
        }
    }

    private void Awake()
    {
        ownCollider = GetComponent<Collider>();

        if (ownCollider is BoxCollider box)
        {
            baseColliderSize = box.size;
        }

        if (ownCollider == null)
        {
            // Cas de l'hôte encore vide. On le dit une fois, puis on se tait : la zone ne
            // comptera simplement aucune bille tant qu'un asset ne l'aura pas meublée.
            Debug.LogWarning($"[DrainZone] '{name}' n'a pas de collider : aucune bille ne sera " +
                             "comptée comme perdue tant qu'il en manque un (en déclencheur).", this);
            return;
        }

        if (!ownCollider.isTrigger)
        {
            Debug.LogWarning($"[DrainZone] Le collider de '{name}' n'est pas un déclencheur : " +
                             "la bille rebondira dessus au lieu d'être comptée comme perdue.", this);
        }
    }

    public void ApplyDifficulty(float widthMultiplier, bool enableLateralRescue, float rescueForce)
    {
        lateralRescueEnabled = enableLateralRescue;
        lateralRescueForce = Mathf.Max(0f, rescueForce);

        if (ownCollider is BoxCollider box && baseColliderSize != Vector3.zero)
        {
            Vector3 size = baseColliderSize;
            size.x *= Mathf.Max(0.1f, widthMultiplier);
            box.size = size;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody ball = other.attachedRigidbody != null
            ? other.attachedRigidbody
            : other.GetComponentInParent<Rigidbody>();

        if (!other.CompareTag("Ball") &&
            (ball == null || !ball.gameObject.CompareTag("Ball")) &&
            !other.transform.root.CompareTag("Ball"))
        {
            return;
        }

        if (lateralRescueEnabled && ball != null)
        {
            Vector3 localPosition = transform.InverseTransformPoint(ball.position);
            float halfWidth = ownCollider != null ? ownCollider.bounds.extents.x : 0f;

            if (halfWidth > 0f && Mathf.Abs(localPosition.x) > halfWidth * 0.35f)
            {
                Vector3 rescueDirection = localPosition.x > 0f ? -transform.right : transform.right;
                ball.AddForce(rescueDirection * lateralRescueForce, ForceMode.Impulse);
                return;
            }
        }

        if (graceDelay <= 0f)
        {
            CountBallLost(other);
            return;
        }

        // Le collider est conservé : le passer à CountBallLost après coup est nécessaire,
        // sinon la bille ne peut plus être identifiée ni retirée du jeu.
        pendingBall = other;
        Invoke(nameof(CountPendingBallLost), graceDelay);
    }

    private void CountPendingBallLost()
    {
        Collider ball = pendingBall;
        pendingBall = null;

        if (ball == null)
        {
            // La bille a été détruite pendant le délai : elle a déjà été comptée.
            return;
        }

        CountBallLost(ball);
    }

    private void CountBallLost(Collider ball)
    {
        if (BallManager.Instance != null)
        {
            BallManager.Instance.NotifyDrained(ball);
            return;
        }

        if (GameManager.Instance != null)
        {
            BallManager.DestroyBallObject(ball);
            GameManager.Instance.LoseBall();
            return;
        }

        Debug.LogWarning("[DrainZone] Ni BallManager ni GameManager dans la scène : " +
                         "la bille n'est pas comptée comme perdue.", this);
    }
}
