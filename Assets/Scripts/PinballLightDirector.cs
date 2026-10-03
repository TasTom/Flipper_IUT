using System;
using UnityEngine;

/// <summary>Event-driven lamps: qualified shots, scoop/lock, magnet and multiball.</summary>
public class PinballLightDirector : MonoBehaviour
{
    [SerializeField] private SubjectTarget[] targets;
    [SerializeField] private PinballLightGroup[] targetGroups;
    [SerializeField] private RampGate[] rampExits;
    [SerializeField] private PinballLightGroup[] rampGroups;
    [SerializeField] private LoopGate loop;
    [SerializeField] private PinballLightGroup loopGroup, lockGroup, scoopGroup;
    [SerializeField] private PinballLightGroup[] multiballGroups;
    [SerializeField] private PlayfieldMagnet magnet;
    [SerializeField] private PinballScoop scoop;
    [SerializeField] private BallLock ballLock;
    private Action[] targetHandlers;
    private BallManager balls;
    private GameManager game;
    private void Awake()
    {
        balls = BallManager.Instance; game = GameManager.Instance;
        if (game == null) game = FindAnyObjectByType<GameManager>();
        targetHandlers = new Action[targets != null ? targets.Length : 0];
        for (int i = 0; i < targetHandlers.Length; i++) { int slot = i; targetHandlers[i] = () => Target(slot); }
    }
    private void OnEnable()
    {
        for (int i = 0; i < targetHandlers.Length; i++) if (targets[i] != null) targets[i].Validated += targetHandlers[i];
        if (rampExits != null) foreach (var exit in rampExits) if (exit != null) exit.Completed += Ramp;
        if (loop != null) loop.Completed += Loop;
        if (ballLock != null) ballLock.Changed += Lock;
        if (scoop != null) { scoop.Captured += Capture; scoop.Ejected += Eject; }
        if (balls != null) { balls.BallCountChanged += Count; balls.BallsCleared += ResetLamps; }
        if (game != null) game.StateChanged += State;
    }
    private void OnDisable()
    {
        for (int i = 0; i < targetHandlers.Length; i++) if (targets[i] != null) targets[i].Validated -= targetHandlers[i];
        if (rampExits != null) foreach (var exit in rampExits) if (exit != null) exit.Completed -= Ramp;
        if (loop != null) loop.Completed -= Loop;
        if (ballLock != null) ballLock.Changed -= Lock;
        if (scoop != null) { scoop.Captured -= Capture; scoop.Ejected -= Eject; }
        if (balls != null) { balls.BallCountChanged -= Count; balls.BallsCleared -= ResetLamps; }
        if (game != null) game.StateChanged -= State;
    }
    private void Target(int slot)
    {
        if (targetGroups != null && slot < targetGroups.Length && targetGroups[slot] != null) targetGroups[slot].Flash();
        if (targets[slot] != null && targets[slot].name == "Target_Cafe_01") magnet?.Pulse();
    }
    private void Ramp(RampGate gate, Collider ball)
    {
        if (rampExits != null) for (int i = 0; i < rampExits.Length; i++) if (rampExits[i] == gate && rampGroups != null && i < rampGroups.Length && rampGroups[i] != null) rampGroups[i].Flash();
        ballLock?.Arm();
    }
    private void Loop(LoopGate gate, Collider ball, int count) => loopGroup?.Flash();
    private void Capture() { scoopGroup?.Flash(); AudioManager.Instance?.Play(PinballSound.TargetValidated); }
    private void Eject() { scoopGroup?.Flash(); AudioManager.Instance?.Play(PinballSound.Launch); }
    private void Lock() { if (ballLock != null) { lockGroup?.SetAnimated(ballLock.IsLit || ballLock.IsReleasing); lockGroup?.SetLevel(ballLock.LockedCount > 0 ? .9f : .1f); } }
    private void Count() { bool multiple = balls != null && balls.LiveBallCount > 1; if (multiballGroups != null) foreach (var group in multiballGroups) if (group != null) group.SetAnimated(multiple); }
    private void State(GameManager.GameState state) { if (state == GameManager.GameState.GameOver || state == GameManager.GameState.Attract) ResetLamps(); }
    private void ResetLamps()
    {
        magnet?.Stop(); lockGroup?.ResetGroup(); scoopGroup?.ResetGroup(); loopGroup?.ResetGroup();
        if (targetGroups != null) foreach (var group in targetGroups) if (group != null) group.ResetGroup();
        if (multiballGroups != null) foreach (var group in multiballGroups) if (group != null) group.ResetGroup();
    }
}
