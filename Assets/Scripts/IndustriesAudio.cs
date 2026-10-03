using UnityEngine;

public enum IndustriesSound { FlipperUp, FlipperDown, Launch, Bumper, Target, Scoop, Bonus, Drain }

/// <summary>Retour sonore de la seconde table, indépendant des règles et du moteur physique.</summary>
[RequireComponent(typeof(AudioSource))]
public sealed class IndustriesAudio : MonoBehaviour
{
    [SerializeField] private IndustriesConfig config;
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup mixerGroup;
    [SerializeField] private GameManager game;
    [SerializeField] private AudioManager audioManager;
    private AudioSource source;
    private bool paused;
    private void Awake()
    {
        source=GetComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0f;
        if(mixerGroup!=null)source.outputAudioMixerGroup=mixerGroup;
        if(game==null)game=FindAnyObjectByType<GameManager>();
        if(audioManager==null)audioManager=FindAnyObjectByType<AudioManager>();
    }
    private void OnEnable(){if(game!=null)game.StateChanged+=OnState;}
    private void Start(){if(game!=null)OnState(game.State);}
    private void OnDisable(){if(game!=null)game.StateChanged-=OnState;}
    private void OnState(GameManager.GameState state)
    {
        bool next=state==GameManager.GameState.Paused;
        if(next==paused || source==null)return;
        paused=next;if(paused)source.Pause();else source.UnPause();
    }
    public void Play(IndustriesSound sound)
    {
        if(config==null || source==null || paused)return;
        AudioClip clip=sound switch {
            IndustriesSound.FlipperUp=>config.flipperUpSound, IndustriesSound.FlipperDown=>config.flipperDownSound,
            IndustriesSound.Launch=>config.launchSound, IndustriesSound.Bumper=>config.bumperSound,
            IndustriesSound.Target=>config.targetSound, IndustriesSound.Scoop=>config.scoopSound,
            IndustriesSound.Bonus=>config.bonusSound, IndustriesSound.Drain=>config.drainSound, _=>null
        };
        if(clip==null)return;
        if(audioManager!=null)audioManager.Play(clip,config.soundVolume,1f);
        else source.PlayOneShot(clip,config.soundVolume);
    }
}
