using UnityEngine;

public enum IndustriesSound { FlipperUp, FlipperDown, Launch, Bumper, Target, Scoop, Bonus, Drain }

/// <summary>Retour sonore de la seconde table, indépendant des règles et du moteur physique.</summary>
[RequireComponent(typeof(AudioSource))]
public sealed class IndustriesAudio : MonoBehaviour
{
    [SerializeField] private IndustriesConfig config;
    private AudioSource source;
    private void Awake()
    {
        source=GetComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0f;
    }
    public void Play(IndustriesSound sound)
    {
        if(config==null || source==null)return;
        AudioClip clip=sound switch {
            IndustriesSound.FlipperUp=>config.flipperUpSound, IndustriesSound.FlipperDown=>config.flipperDownSound,
            IndustriesSound.Launch=>config.launchSound, IndustriesSound.Bumper=>config.bumperSound,
            IndustriesSound.Target=>config.targetSound, IndustriesSound.Scoop=>config.scoopSound,
            IndustriesSound.Bonus=>config.bonusSound, IndustriesSound.Drain=>config.drainSound, _=>null
        };
        if(clip!=null)source.PlayOneShot(clip,config.soundVolume);
    }
}
