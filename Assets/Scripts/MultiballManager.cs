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
    [SerializeField] private BallLock ballLock;
    private float ballRadius;
    private bool fromPhysicalLock;

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
                Vector3 scale = sphere.transform.lossyScale;
                ballRadius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
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
        int needed = BallManager.Instance != null ? Mathf.Max(0, Mathf.Min(3, extraBalls + 1) - BallManager.Instance.LiveBallCount) : extraBalls;
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
        if (released > 0 && (!alreadyMultiball || fromPhysicalLock) && ScoreManager.Instance != null)
            ScoreManager.Instance.RecordMultiballStarted();
        fromPhysicalLock = false;
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
