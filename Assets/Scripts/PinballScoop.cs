using System;
using System.Collections;
using UnityEngine;

/// <summary>A real aperture captures only balls below the playfield; solid ramp traffic is ignored.</summary>
public class PinballScoop : MonoBehaviour
{
    [SerializeField] private PinballMechanismConfig config;
    [SerializeField] private Transform seat, ejectPoint;
    [SerializeField] private BallLock ballLock;
    private BallManager balls;
    private Rigidbody captive;
    private float cooldown;
    private Coroutine routine;
    private bool multiballCapture;
    public event Action Captured;
    public event Action Ejected;
    private void Awake()
    {
        balls = BallManager.Instance;
        if (config == null || seat == null || ejectPoint == null || balls == null)
        { Debug.LogWarning("[PinballScoop] Config, assise, sortie ou BallManager manquant.", this); enabled = false; }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!enabled || captive != null || Time.time < cooldown || !other.CompareTag("Ball")) return;
        var body = other.attachedRigidbody;
        if (body == null || body.isKinematic || Vector3.Dot(body.position - transform.position, transform.up) > .08f) return;
        if (GameManager.Instance == null || GameManager.Instance.State != GameManager.GameState.Playing) return;
        multiballCapture = balls.LiveBallCount > 1;
        captive = body; balls.HoldBall(body); body.position = seat.position;
        Captured?.Invoke();
        ScoreManager.Instance?.Add(config.scoopPoints);
        routine = StartCoroutine(Process());
    }
    private void OnEnable() { if (balls != null) balls.BallsCleared += ResetCapture; }
    private void ResetCapture() { if (routine != null) StopCoroutine(routine); routine = null; captive = null; cooldown = 0; }
    private IEnumerator Process()
    {
        yield return new WaitForSeconds(config.scoopDwell);
        var ball = captive; captive = null; routine = null;
        if (ball == null) yield break;
        if (multiballCapture || ballLock == null || !ballLock.TryLock(ball)) Eject(ball);
    }
    public void Eject(Rigidbody ball)
    {
        if (ball == null || balls == null) return;
        cooldown = Time.time + config.ejectCooldown;
        balls.ReleaseHeldBall(ball, ejectPoint.position, ejectPoint.rotation);
        ball.AddForce(ejectPoint.forward * config.ejectSpeed * ball.mass, ForceMode.Impulse);
        Ejected?.Invoke();
    }
    private void OnDisable()
    {
        if (balls != null) balls.BallsCleared -= ResetCapture;
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (captive != null && balls != null && ejectPoint != null && config != null) Eject(captive);
        captive = null;
    }
}
