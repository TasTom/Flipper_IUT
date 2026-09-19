using System.Collections;
using UnityEngine;

/// <summary>
/// Capture une bille dans un kicker, la recentre, la descend brièvement puis la relance.
/// Le collider de l'hôte doit être un déclencheur.
/// </summary>
public class Kicker : MonoBehaviour
{
    [Header("Capture")]
    [SerializeField] private Transform capturePoint;
    [SerializeField] private float captureDuration = 0.25f;
    [SerializeField] private float holeDepth = 0.18f;
    [SerializeField] private float launchDelay = 2f;
    [Tooltip("Point situe au-dessus et hors du collider du kicker. Laisse vide pour utiliser la hauteur ci-dessous.")]
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float exitHeight = 0.7f;

    [Header("Relance")]
    [SerializeField] private float launchForce = 8f;
    [Range(0f, 0.25f)]
    [SerializeField] private float horizontalSpread;
    [SerializeField] private bool showDebugLog;

    [Header("Audio")]
    [SerializeField] private AudioClip entrySound;
    [Range(0f, 1f)]
    [SerializeField] private float entryVolume = 1f;
    [SerializeField] private AudioClip exitSound;
    [Range(0f, 1f)]
    [SerializeField] private float exitVolume = 1f;

    private Rigidbody capturedBall;
    private Coroutine captureRoutine;
    private bool ejecting;

    private void Reset()
    {
        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider != null)
        {
            ownCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider == null)
        {
            Debug.LogWarning($"[Kicker] '{name}' n'a pas de collider : aucune bille ne sera capturee.", this);
        }
        else if (!ownCollider.isTrigger)
        {
            ownCollider.isTrigger = true;
            Debug.LogWarning($"[Kicker] Le collider de '{name}' a ete configure automatiquement " +
                             "comme declencheur.", this);
        }

        if (capturePoint != null && capturePoint.GetComponentInParent<Rigidbody>() != null)
        {
            Debug.LogWarning($"[Kicker] Le capturePoint de '{name}' doit etre un Transform fixe " +
                             "du kicker, pas un composant de la bille.", this);
        }

        captureDuration = Mathf.Max(0f, captureDuration);
        holeDepth = Mathf.Max(0f, holeDepth);
        launchDelay = Mathf.Max(0f, launchDelay);
        exitHeight = Mathf.Max(0.1f, exitHeight);
        launchForce = Mathf.Max(0f, launchForce);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCapture(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryCapture(other);
    }

    private void TryCapture(Collider other)
    {
        if (capturedBall != null || ejecting)
        {
            return;
        }

        Rigidbody ball = other.attachedRigidbody != null
            ? other.attachedRigidbody
            : other.GetComponentInParent<Rigidbody>();

        bool isBall = other.CompareTag("Ball") ||
                      (ball != null && ball.gameObject.CompareTag("Ball")) ||
                      other.transform.root.CompareTag("Ball");

        if (!isBall)
        {
            return;
        }

        if (ball == null)
        {
            return;
        }

        if (showDebugLog)
        {
            Debug.Log($"[Kicker] Bille detectee par '{name}' via '{other.name}'.", this);
        }

        capturedBall = ball;
        if (entrySound != null)
        {
            AudioSource.PlayClipAtPoint(entrySound, transform.position, entryVolume);
        }

        const int normalKickerScore = 1000;
        const int nuitDeLInfoKickerScore = 2000;
        int awardedScore = normalKickerScore;
        if (MissionManager.Instance != null && MissionManager.Instance.IsNuitDeLInfoActive)
        {
            awardedScore = nuitDeLInfoKickerScore;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddExactScore(awardedScore);
        }
        else
        {
            Debug.LogWarning($"[Kicker] GameManager.Instance est absent : les points de '{name}' " +
                             "ne peuvent pas etre ajoutes.", this);
        }

        captureRoutine = StartCoroutine(CaptureAndLaunch(ball));
    }

    private IEnumerator CaptureAndLaunch(Rigidbody ball)
    {
        Vector3 capturePosition = capturePoint != null ? capturePoint.position : transform.position;
        Vector3 holePosition = capturePosition - transform.up * holeDepth;
        Vector3 exitPosition = exitPoint != null
            ? exitPoint.position
            : capturePosition + transform.up * exitHeight;
        Vector3 startPosition = ball.position;
        float elapsed = 0f;

        if (!ball.isKinematic)
        {
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
        }

        ball.isKinematic = true;
        ball.useGravity = false;

        while (elapsed < captureDuration)
        {
            elapsed += Time.fixedDeltaTime;
            ball.MovePosition(Vector3.Lerp(startPosition, capturePosition,
                Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, captureDuration))));
            yield return new WaitForFixedUpdate();
        }

        elapsed = 0f;
        float sinkDuration = Mathf.Min(0.25f, launchDelay);
        while (elapsed < sinkDuration)
        {
            elapsed += Time.fixedDeltaTime;
            ball.MovePosition(Vector3.Lerp(capturePosition, holePosition,
                Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, sinkDuration))));
            yield return new WaitForFixedUpdate();
        }

        float remainingDelay = Mathf.Max(0f, launchDelay - captureDuration - sinkDuration);
        if (remainingDelay > 0f)
        {
            yield return new WaitForSeconds(remainingDelay);
        }

        // Sortir du volume avant d'appliquer l'impulsion evite que le kicker ou son mesh
        // ne retienne la bille et empeche une nouvelle capture immediate.
        ball.position = exitPosition;
        // La bille doit redevenir dynamique avant AddForce : une force est ignoree
        // tant que Rigidbody.isKinematic est vrai.
        ball.isKinematic = false;
        ball.useGravity = true;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;

        if (exitSound != null)
        {
            AudioSource.PlayClipAtPoint(exitSound, exitPosition, exitVolume);
        }

        Vector3 launchDirection = transform.up + transform.right * horizontalSpread;
        ball.AddForce(launchDirection.normalized * launchForce, ForceMode.Impulse);

        ejecting = true;
        captureRoutine = null;
        yield return new WaitForSeconds(0.2f);
        capturedBall = null;
        ejecting = false;
    }

    private void OnDisable()
    {
        if (captureRoutine != null)
        {
            StopCoroutine(captureRoutine);
            captureRoutine = null;
        }

        if (capturedBall != null)
        {
            capturedBall.isKinematic = false;
            capturedBall.useGravity = true;
            capturedBall = null;
        }
    }
}