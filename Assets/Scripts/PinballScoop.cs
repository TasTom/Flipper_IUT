using System;
using System.Collections;
using System.Collections.Generic;
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
    private readonly HashSet<Rigidbody> ejecting = new HashSet<Rigidbody>();
    public event Action Captured;
    public event Action Ejected;
    private void Awake()
    {
        balls = BallManager.Instance;
        if (config == null || seat == null || ejectPoint == null || balls == null)
        { Debug.LogWarning("[PinballScoop] Config, assise, sortie ou BallManager manquant.", this); enabled = false; }
    }
    private void OnTriggerEnter(Collider other) => TryCapture(other);

    // A ball can enter the trigger above the capture depth, then fall while still overlapping it.
    private void OnTriggerStay(Collider other) => TryCapture(other);

    private void TryCapture(Collider other)
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
    private void ResetCapture()
    {
        StopAllCoroutines();
        ejecting.Clear();
        routine = null;
        captive = null;
        cooldown = 0;
    }
    private IEnumerator Process()
    {
        yield return new WaitForSeconds(config.scoopDwell);
        var ball = captive; captive = null; routine = null;
        if (ball == null) yield break;
        if (multiballCapture || ballLock == null || !ballLock.TryLock(ball)) Eject(ball);
    }
    public void Eject(Rigidbody ball)
    {
        if (ball == null || balls == null || !ejecting.Add(ball)) return;
        cooldown = Time.time + config.ejectCooldown;
        if (isActiveAndEnabled)
            StartCoroutine(LiftAndEject(ball));
        else
            Release(ball);
    }

    private IEnumerator LiftAndEject(Rigidbody ball)
    {
        // Locked balls return to the seat below the playfield before rising through the hole.
        var start = seat.position;
        var exit = ejectPoint.position;
        var rim = start + Vector3.Project(exit - start, transform.up);
        float liftDistance = Vector3.Distance(start, rim);
        float travelDistance = liftDistance + Vector3.Distance(rim, exit);
        float speed = Mathf.Max(0.01f, config.ejectSpeed * 0.5f);
        var tick = new WaitForFixedUpdate();
        ball.position = start;

        for (float distance = 0; distance < travelDistance && ball != null;)
        {
            yield return tick;
            if (ball == null) break;
            distance = Mathf.Min(distance + speed * Time.fixedDeltaTime, travelDistance);
            ball.position = distance <= liftDistance
                ? Vector3.MoveTowards(start, rim, distance)
                : Vector3.MoveTowards(rim, exit, distance - liftDistance);
        }

        if (ball != null) Release(ball);
        else ejecting.Remove(ball);
    }

    private void Release(Rigidbody ball)
    {
        ejecting.Remove(ball);
        balls.ReleaseHeldBall(ball, ejectPoint.position, ejectPoint.rotation);
        ball.AddForce(ejectPoint.forward * config.ejectSpeed * ball.mass, ForceMode.Impulse);
        Ejected?.Invoke();
    }
    private void OnDisable()
    {
        if (balls != null) balls.BallsCleared -= ResetCapture;
        StopAllCoroutines();
        routine = null;
        if (captive != null && balls != null && ejectPoint != null && config != null) Eject(captive);
        captive = null;
        foreach (var ball in new List<Rigidbody>(ejecting))
            if (ball != null) Release(ball);
        ejecting.Clear();
    }
}
