using UnityEngine;

public class SubjectTarget : MonoBehaviour
{
    [SerializeField] private string subjectId = "Programmation";
    [SerializeField] private int points = 150;
    [SerializeField] private float impulseForce = 6f;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ball"))
        {
            return;
        }

        GameManager.Instance.AddScore(points);
        MissionManager.Instance.CompleteSubject(subjectId);

        Rigidbody ballRigidbody = collision.gameObject.GetComponent<Rigidbody>();

        if (ballRigidbody != null)
        {
            Vector3 direction = (collision.transform.position - transform.position).normalized;
            direction.y = 0.2f;
            ballRigidbody.AddForce(direction * impulseForce, ForceMode.Impulse);
        }
    }
}