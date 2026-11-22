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
    private AudioClip automateMotor;
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
        PlayClip(clip);
    }

    public void PlayAutomateMotor()
    {
        if(config==null || source==null || paused)return;
        if (automateMotor == null)
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate * 4 / 5];
            float phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / sampleRate;
                phase += 2f * Mathf.PI * (58f + 78f * t) / sampleRate;
                float gain = Mathf.Clamp01(t * 18f) * Mathf.Clamp01((.8f - t) * 7f);
                samples[i] = gain * (.29f * Mathf.Sin(phase) + .13f * Mathf.Sin(phase * 2f)
                    + .07f * Mathf.Sin(phase * 5f)) * (.72f + .28f * Mathf.Cos(t * 75f));
            }
            automateMotor = AudioClip.Create("Démarrage de l’automate", samples.Length, 1, sampleRate, false);
            automateMotor.SetData(samples, 0);
        }
        PlayClip(automateMotor);
    }

    private void PlayClip(AudioClip clip)
    {
        if(clip==null)return;
        if(audioManager!=null)audioManager.Play(clip,config.soundVolume,1f);
        else source.PlayOneShot(clip,config.soundVolume);
    }

    private void OnDestroy()
    {
        if (automateMotor != null) Destroy(automateMotor);
    }
}
