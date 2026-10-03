using System;
using UnityEngine;
using UnityEngine.Audio;

public enum PinballSound
{
    FlipperUp, FlipperDown, Launch, TargetValidated, RampCompleted,
    LoopCompleted, MultiplierRaised, ExtraBall, GameOver, HighScore
}

/// <summary>GDD §Direction sonore et §Architecture Unity : gains, voix et sons configurables.</summary>
[CreateAssetMenu(menuName = "Vosges Mania/Audio", fileName = "AudioConfig")]
public class AudioConfig : ScriptableObject
{
    [Range(0f, 1f)] public float musicVolume = .45f;
    [Range(0f, 1f)] public float effectsVolume = .8f;
    [Range(1, 32)] public int voices = 12;
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup mechanicalGroup;
    public AudioMixerGroup rewardGroup;
    public Cue[] cues = Array.Empty<Cue>();

    [Serializable]
    public class Cue
    {
        public PinballSound sound;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = .65f;
        [Range(0f, 2f)] public float pitchSemitones = .15f;
        [Min(0f)] public float cooldown = .03f;
        public bool mechanical;
    }
}
