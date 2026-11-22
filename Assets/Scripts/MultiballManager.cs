using UnityEngine;

public class MultiballManager : MonoBehaviour
{
    public static MultiballManager Instance;

    [Header("Multiball settings")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Sorties de multiball, dans l'ordre de préférence. Chaque sortie doit être libre et orientée vers le jeu.")]
    [SerializeField] private Transform[] releasePoints;
    [SerializeField] private int extraBalls = 2;
    [SerializeField] private float spawnImpulse = 5f;
    [Tooltip("Nombre maximal de billes actives pendant un multiball.")]
    [SerializeField] private int maxActiveBalls = 3;
    [SerializeField] private BallLock ballLock;

    [Header("Jackpots multiball")]
    [Tooltip("Points d'un jackpot sur rampe ou loop pendant un multiball.")]
    [SerializeField] private int jackpotPoints = 25000;
    [Tooltip("Points d'un super jackpot (tous les 3 jackpots).")]
    [SerializeField] private int superJackpotPoints = 75000;
    [SerializeField] private int jackpotsForSuper = 3;

    private float ballRadius;
    private bool fromPhysicalLock;
    private int jackpotsThisMultiball;

    public bool IsMultiballActive => BallManager.Instance != null && BallManager.Instance.LiveBallCount > 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (ballPrefab == null)
        {
            ballPrefab = Resources.Load<GameObject>("Ball");

#if UNITY_EDITOR
            if (ballPrefab == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("Ball t:Prefab");
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    GameObject candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (candidate != null && candidate.CompareTag("Ball"))
                    {
                        ballPrefab = candidate;
                        break;
                    }
                }

                if (ballPrefab == null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    ballPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
#endif
        }

        if (spawnPoint == null)
        {
            GameObject spawnObject = GameObject.Find("BallSpawnPoint");
            if (spawnObject != null)
            {
                spawnPoint = spawnObject.transform;
            }
        }

        if (ballPrefab != null)
        {
            var sphere = ballPrefab.GetComponentInChildren<SphereCollider>();
            if (sphere != null)
            {
                Vector3 scale = sphere.transform.localScale;
                ballRadius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                if (ballRadius <= 0f) ballRadius = sphere.radius > 0f ? sphere.radius : 0.225f;
            }
            else
            {
                ballRadius = 0.225f;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void TriggerMultiball()
    {
        if (ballLock != null && ballLock.IsReleasing) return;
        if (ballLock != null && ballLock.LockedCount > 0) { ballLock.ReleaseMultiball(); return; }
        if (ballPrefab == null || ballRadius <= 0f || (spawnPoint == null && (releasePoints == null || releasePoints.Length == 0)))
        {
            Debug.LogWarning("[MultiballManager] Bille sphérique ou sorties de multiball non assignées.", this);
            return;
        }

        bool alreadyMultiball = BallManager.Instance != null && BallManager.Instance.LiveBallCount > 1;
        int released = 0;
        int target = Mathf.Clamp(extraBalls + 1, 1, Mathf.Clamp(maxActiveBalls, 1, 3));
        int current = BallManager.Instance != null ? Mathf.Max(0, BallManager.Instance.LiveBallCount) : 1;
        int needed = Mathf.Max(0, target - current);
        bool physicalLockActivation = fromPhysicalLock;
        fromPhysicalLock = false;
        for (int i = 0; i < needed; i++)
        {
            Physics.SyncTransforms();
            Transform release = FindFreeRelease();
            if (release == null)
            {
                Debug.LogWarning("[MultiballManager] Aucune sortie libre : aucune bille créée dans un obstacle.", this);
                break;
            }
            // All balls must be tracked for draining, speed limiting and restarting.
            Rigidbody ballRigidbody = BallManager.Instance != null
                ? BallManager.Instance.SpawnBall(ballPrefab, release.position, release.rotation)
                : Instantiate(ballPrefab, release.position, release.rotation).GetComponent<Rigidbody>();

            if (ballRigidbody != null)
            {
                ballRigidbody.AddForce(release.forward * spawnImpulse, ForceMode.Impulse);
                released++;
            }
        }
        if ((released > 0 || physicalLockActivation) && (!alreadyMultiball || physicalLockActivation))
        {
            jackpotsThisMultiball = 0;
            if (ScoreManager.Instance != null) ScoreManager.Instance.RecordMultiballStarted();
            var saver = BallManager.Instance != null ? BallManager.Instance.GetComponent<BallSaver>() : null;
            if (saver != null) saver.ArmMultiball();
            AudioManager.Instance?.Play(PinballSound.ExtraBall);
        }
    }

    /// <summary>
    /// Récompense un tir majeur (rampe ou loop) pendant un multiball par un Jackpot ou Super Jackpot.
    /// </summary>
    public bool TryAwardJackpot()
    {
        if (!IsMultiballActive || GameManager.Instance == null || GameManager.Instance.State != GameManager.GameState.Playing)
            return false;

        jackpotsThisMultiball++;
        bool isSuper = jackpotsForSuper > 0 && jackpotsThisMultiball % jackpotsForSuper == 0;
        int pts = isSuper ? superJackpotPoints : jackpotPoints;

        if (pts > 0) GameManager.Instance.AddBonus(pts);
        GameManager.Instance.ShowMessage(
            isSuper ? $"SUPER JACKPOT !  +{pts:N0}" : $"JACKPOT !  +{pts:N0}",
            isSuper ? 2.5f : 1.8f);
        AudioManager.Instance?.Play(isSuper ? PinballSound.ExtraBall : PinballSound.MultiplierRaised);
        return true;
    }

    public void TriggerLockedMultiball() { fromPhysicalLock = true; TriggerMultiball(); }

    private Transform FindFreeRelease()
    {
        if (releasePoints != null && releasePoints.Length > 0)
        {
            foreach (var point in releasePoints)
                if (point != null && IsClear(point)) return point;
            return null;
        }
        return spawnPoint != null && IsClear(spawnPoint) ? spawnPoint : null;
    }

    private bool IsClear(Transform point)
    {
        return !Physics.CheckSphere(point.position, ballRadius + Physics.defaultContactOffset,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }
}
