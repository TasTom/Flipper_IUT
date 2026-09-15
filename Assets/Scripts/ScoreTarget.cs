using UnityEngine;

public class ScoreTarget : MonoBehaviour
{
    [SerializeField] private int points = 100;
    [SerializeField] private float impulseForce = 7f;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ball"))
        {
            return;
        }

        GameManager.Instance.AddScore(points);

        Rigidbody ballRigidbody = collision.gameObject.GetComponent<Rigidbody>();

        if (ballRigidbody != null)
        {
            Vector3 direction = (collision.transform.position - transform.position).normalized;
            direction.y = 0.25f;

            ballRigidbody.AddForce(direction * impulseForce, ForceMode.Impulse);
        }
    }
}