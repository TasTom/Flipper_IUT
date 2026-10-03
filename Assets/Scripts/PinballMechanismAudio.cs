using UnityEngine;

/// <summary>GDD §Effets sonores. Optional listener; never changes a mechanism or its score.</summary>
[DisallowMultipleComponent]
public class PinballMechanismAudio : MonoBehaviour
{
    private Flipper flipper;
    private Plunger plunger;
    private SubjectTarget target;
    private RampGate ramp;
    private LoopGate loop;

    private void Awake()
    {
        flipper = GetComponent<Flipper>();
        plunger = GetComponent<Plunger>();
        target = GetComponent<SubjectTarget>();
        ramp = GetComponent<RampGate>();
        loop = GetComponent<LoopGate>();
        if (flipper == null && plunger == null && target == null && ramp == null && loop == null)
        {
            Debug.LogWarning($"[PinballMechanismAudio] '{name}' n'a aucun mécanisme compatible.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (flipper != null) flipper.PressedChanged += FlipperChanged;
        if (plunger != null) plunger.Released += Launch;
        if (target != null) target.Validated += ValidateTarget;
        if (ramp != null) ramp.Completed += CompleteRamp;
        if (loop != null) loop.Completed += CompleteLoop;
    }
    private void OnDisable()
    {
        if (flipper != null) flipper.PressedChanged -= FlipperChanged;
        if (plunger != null) plunger.Released -= Launch;
        if (target != null) target.Validated -= ValidateTarget;
        if (ramp != null) ramp.Completed -= CompleteRamp;
        if (loop != null) loop.Completed -= CompleteLoop;
    }
    private static void Play(PinballSound sound)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Play(sound);
    }
    private void FlipperChanged(bool pressed) => Play(pressed ? PinballSound.FlipperUp : PinballSound.FlipperDown);
    private void Launch() => Play(PinballSound.Launch);
    private void ValidateTarget() => Play(PinballSound.TargetValidated);
    private void CompleteRamp(RampGate gate, Collider ball) => Play(PinballSound.RampCompleted);
    private void CompleteLoop(LoopGate gate, Collider ball, int count) => Play(PinballSound.LoopCompleted);
}
