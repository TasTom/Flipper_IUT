using UnityEngine;

public class MultiballManager : MonoBehaviour
{
    public static MultiballManager Instance;

    [Header("Multiball settings")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private int extraBalls = 2;
    [SerializeField] private float spawnImpulse = 5f;

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
    }

    public void TriggerMultiball()
    {
        if (ballPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("[MultiballManager] ballPrefab ou spawnPoint non assigne.");
            return;
        }

        for (int i = 0; i < extraBalls; i++)
        {
            GameObject ball = Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation);
            Rigidbody ballRigidbody = ball.GetComponent<Rigidbody>();

            if (ballRigidbody != null)
            {
                Vector3 direction = new Vector3(Random.Range(-0.5f, 0.5f), 0.25f, 1f).normalized;
                ballRigidbody.AddForce(direction * spawnImpulse, ForceMode.Impulse);
            }
        }
    }
}
