using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Two physical balls stored under the scoop; replacement doesn't consume a player ball.</summary>
public class BallLock : MonoBehaviour
{
    [SerializeField] private PinballMechanismConfig config;
    [SerializeField] private Transform[] storage;
    [SerializeField] private PinballScoop scoop;
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform replacement;
    [SerializeField] private MultiballManager multiball;
    private readonly List<Rigidbody> locked = new List<Rigidbody>(2);
    private BallManager balls;
    private GameManager game;
    private bool lit, releasing;
    public int LockedCount => locked.Count;
    public bool IsLit => lit;
    public bool IsReleasing => releasing;
    public event Action Changed;
    private void Awake()
    {
        balls = BallManager.Instance; game = GameManager.Instance;
        if (game == null) game = FindAnyObjectByType<GameManager>();
        if (multiball == null) multiball = FindAnyObjectByType<MultiballManager>();
        if (config == null || game == null || balls == null || scoop == null || replacement == null || ballPrefab == null || storage == null || storage.Length < config.lockCapacity || Array.Exists(storage, slot => slot == null))
        { Debug.LogWarning("[BallLock] Configuration, scoop, réserve ou sortie manquante.", this); enabled = false; }
    }
    private void OnEnable() { if (balls != null) balls.BallsCleared += ResetLock; if (game != null) game.StateChanged += State; }
    private void OnDisable() { if (balls != null) balls.BallsCleared -= ResetLock; if (game != null) game.StateChanged -= State; StopAllCoroutines(); releasing = false; foreach (var ball in locked) if (ball != null && scoop != null) scoop.Eject(ball); locked.Clear(); }
    private void State(GameManager.GameState state) { if (state == GameManager.GameState.GameOver && balls != null) balls.ClearAll(); }
    public void Arm() { if (enabled && !releasing && balls.LiveBallCount <= 1) { lit = true; Changed?.Invoke(); game?.ShowMessage("LOCK ALLUMÉ — VISE LE SCOOP", 2f); } }
    public bool TryLock(Rigidbody ball)
    {
        if (!enabled || !lit || releasing || ball == null || locked.Count >= config.lockCapacity || balls.LiveBallCount > 1) return false;
        locked.Add(ball); ball.position = storage[locked.Count - 1].position; lit = false;
        ScoreManager.Instance?.Add(config.lockPoints); Changed?.Invoke();
        if (locked.Count >= config.lockCapacity) ReleaseMultiball();
        else
        {
            balls.SpawnBall(ballPrefab, replacement.position, replacement.rotation);
            game?.NotifyBallReturnedToLane();
            game?.ShowMessage("BILLE VERROUILLÉE 1/2 — RELANCE", 2f);
        }
        return true;
    }
    public void ReleaseMultiball() { if (releasing || locked.Count == 0) return; releasing = true; lit = false; Changed?.Invoke(); StartCoroutine(Release()); }
    private IEnumerator Release()
    {
        while (locked.Count > 0)
        {
            var ball = locked[0]; locked.RemoveAt(0); scoop.Eject(ball); Changed?.Invoke();
            yield return new WaitForSeconds(config.releaseSpacing);
        }
        releasing = false; Changed?.Invoke();
        multiball?.TriggerLockedMultiball(); game?.ShowMessage("MULTIBALL — FIN DE SEMESTRE", 3f);
    }
    private void ResetLock() { StopAllCoroutines(); locked.Clear(); lit = false; releasing = false; Changed?.Invoke(); }
}
