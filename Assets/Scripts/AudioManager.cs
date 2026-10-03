using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire audio (GDD §Audio) : musique de fond et effets sonores.
///
/// <para><b>Musique</b> : une piste en boucle lancée au démarrage. C'est l'ambiance de la
/// borne.</para>
///
/// <para><b>Effets sonores</b> : un registre nom → clip, joué via <see cref="Play"/>. Les
/// clips sont des champs sérialisés : l'équipe les remplit dans l'Inspector quand les
/// fichiers audio existent. Sans clip pour un nom, <see cref="Play"/> ne fait rien — le
/// gameplay reste donc muet mais fonctionnel tant que les SFX ne sont pas posés.</para>
///
/// <para>Les éléments de table qui veulent un son ne dépendent pas de ce gestionnaire :
/// c'est <see cref="CollisionSound"/> qui écoute les collisions et appelle
/// <see cref="Play"/>. Aucun script de gameplay n'est modifié.</para>
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioConfig config;

    [Header("Musique")]
    [Tooltip("Piste de fond, jouée en boucle au démarrage.")]
    [SerializeField] private AudioClip musicClip;

    [Tooltip("Volume de la musique, 0 à 1.")]
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.45f;

    [Header("Effets sonores")]
    [Tooltip("Clips jouables par nom (le nom du clip sert d'identifiant).")]
    [SerializeField] private AudioClip[] sfxClips;

    [Tooltip("Volume des effets, 0 à 1.")]
    [SerializeField] [Range(0f, 1f)] private float sfxVolume = 0.8f;

    [Tooltip("Nombre d'effets jouables simultanément. Au-delà, le plus ancien est coupé.")]
    [SerializeField] private int sfxVoices = 8;

    private AudioSource musicSource;
    private Coroutine musicFade;
    private readonly List<AudioSource> sfxSources = new List<AudioSource>();
    private readonly Dictionary<string, AudioClip> registry = new Dictionary<string, AudioClip>();
    private int nextVoice;
    private bool paused;
    private long[] voiceOrder;
    private long playbackOrder;
    private readonly Dictionary<PinballSound, CueState> cues = new Dictionary<PinballSound, CueState>();
    private sealed class CueState
    {
        public AudioConfig.Cue cue;
        public int next;
        public float lastTime = float.NegativeInfinity;
    }
    private float MusicGain => config != null ? config.musicVolume : musicVolume;
    private float EffectsGain => config != null ? config.effectsVolume : sfxVolume;

    /// <summary>Nom d'un clip présent au registre, ou null si absent ou muet.</summary>
    public bool HasClip(string clipName)
    {
        return !string.IsNullOrEmpty(clipName) && registry.TryGetValue(clipName, out var clip) && clip != null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[AudioManager] Un second gestionnaire existe sur '{name}' : il est ignoré.", this);
            enabled = false;
            return;
        }

        Instance = this;

        // Reuse an authored scene source instead of playing an offset second copy.
        var background = GameObject.Find("BackgroundMusic");
        if (background != null && background.TryGetComponent<AudioSource>(out var source)
            && source.clip != null && source.clip == musicClip)
        {
            musicSource = source;
        }
        else
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }

        // Authored sources and newly created sources follow the same configured gain.
        musicSource.volume = Mathf.Clamp01(MusicGain);
        if (config != null && config.musicGroup != null)
            musicSource.outputAudioMixerGroup = config.musicGroup;

        for (int i = 0; i < Mathf.Clamp(config != null ? config.voices : sfxVoices, 1, 32); i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.volume = EffectsGain;
            if (config != null) src.outputAudioMixerGroup = config.mechanicalGroup;
            sfxSources.Add(src);
        }
        voiceOrder = new long[sfxSources.Count];
        if (config != null)
            foreach (var cue in config.cues ?? System.Array.Empty<AudioConfig.Cue>())
                if (cue != null && !cues.ContainsKey(cue.sound))
                    cues.Add(cue.sound, new CueState { cue = cue });

        foreach (var clip in sfxClips ?? System.Array.Empty<AudioClip>())
        {
            if (clip != null && !registry.ContainsKey(clip.name))
            {
                registry[clip.name] = clip;
            }
        }
    }

    private void Start()
    {
        PlayMusic();
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; }
    }

    /// <summary>Lance la musique de fond en boucle. Sans effet si déjà en cours.</summary>
    public void PlayMusic()
    {
        if (paused || musicClip == null || musicSource == null || musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = musicClip;
        musicSource.Play();
        if (config != null && config.musicFadeInSeconds > 0f)
        {
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(FadeMusicIn(config.musicFadeInSeconds));
        }
    }

    private System.Collections.IEnumerator FadeMusicIn(float seconds)
    {
        float elapsed = 0f;
        musicSource.volume = 0f;
        while (elapsed < seconds)
        {
            if (!paused) elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Clamp01(MusicGain) * Mathf.SmoothStep(0f, 1f, elapsed / seconds);
            yield return null;
        }
        musicSource.volume = Mathf.Clamp01(MusicGain);
        musicFade = null;
    }

    /// <summary>Arrête la musique de fond.</summary>
    public void StopMusic()
    {
        if (musicFade != null) { StopCoroutine(musicFade); musicFade = null; }
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    /// <summary>
    /// Joue un effet sonore par son nom. Silencieux si le nom n'est pas au registre : le
    /// gameplay ne doit pas dépendre de l'audio pour fonctionner.
    /// </summary>
    public void Play(string clipName)
    {
        Play(clipName, 1f, 1f);
    }

    /// <summary>
    /// Joue un effet sonore en dosant son volume et sa hauteur.
    /// </summary>
    /// <param name="clipName">Nom du clip au registre.</param>
    /// <param name="volumeScale">Multiplicateur du volume des effets. 0 = muet.</param>
    /// <param name="pitch">Hauteur de lecture. 1 = normale.</param>
    /// <remarks>
    /// Les deux paramètres sont réécrits à chaque lecture, jamais composés : une voix
    /// déjà jouée plus fort ne doit pas laisser un pitch derrière elle pour le clip suivant.
    /// </remarks>
    public void Play(string clipName, float volumeScale, float pitch)
    {
        if (string.IsNullOrEmpty(clipName) || sfxSources.Count == 0)
        {
            return;
        }

        if (!registry.TryGetValue(clipName, out var clip) || clip == null)
        {
            return;
        }

        Play(clip, volumeScale, pitch);
    }

    /// <summary>Joue un effet sonore déjà chargé. Silencieux si null.</summary>
    public void Play(AudioClip clip)
    {
        Play(clip, 1f, 1f);
    }

    /// <summary>Joue un effet sonore déjà chargé, en dosant volume et hauteur.</summary>
    public void Play(AudioClip clip, float volumeScale, float pitch)
    {
        PlayVoice(clip, volumeScale, pitch, config != null ? config.mechanicalGroup : null);
    }

    /// <summary>Round-robin variants and cooldown belong to the cue, independently of gameplay.</summary>
    public void Play(PinballSound sound)
    {
        if (paused || !cues.TryGetValue(sound, out var state)) return;
        var cue = state.cue;
        if (Time.unscaledTime - state.lastTime < cue.cooldown || cue.clips == null) return;
        AudioClip clip = null;
        for (int i = 0; i < cue.clips.Length; i++)
        {
            clip = cue.clips[state.next];
            state.next = (state.next + 1) % cue.clips.Length;
            if (clip != null) break;
        }
        if (clip == null) return;
        state.lastTime = Time.unscaledTime;
        float pitch = Mathf.Pow(2f, Random.Range(-cue.pitchSemitones, cue.pitchSemitones) / 12f);
        PlayVoice(clip, cue.volume, pitch,
            config != null ? (cue.mechanical ? config.mechanicalGroup : config.rewardGroup) : null);
    }

    /// <summary>Pause the existing voices without restarting music or losing source positions.</summary>
    public void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        if (musicSource != null) { if (value) musicSource.Pause(); else musicSource.UnPause(); }
        foreach (var source in sfxSources)
            if (source != null) { if (value) source.Pause(); else source.UnPause(); }
    }

    private void PlayVoice(AudioClip clip, float volumeScale, float pitch,
        UnityEngine.Audio.AudioMixerGroup group)
    {
        if (paused || clip == null || sfxSources.Count == 0 || volumeScale <= 0f)
        {
            return;
        }

        int voice = nextVoice;
        bool found = false;
        for (int i = 0; i < sfxSources.Count; i++)
        {
            int candidate = (nextVoice + i) % sfxSources.Count;
            if (!sfxSources[candidate].isPlaying) { voice = candidate; found = true; break; }
        }
        if (!found)
            for (int i = 0; i < voiceOrder.Length; i++)
                if (voiceOrder[i] < voiceOrder[voice]) voice = i;
        var src = sfxSources[voice];
        nextVoice = (voice + 1) % sfxSources.Count;
        voiceOrder[voice] = ++playbackOrder;

        src.clip = clip;
        src.outputAudioMixerGroup = group;
        src.volume = Mathf.Clamp01(EffectsGain * volumeScale);
        src.pitch = Mathf.Clamp(pitch, 0.25f, 3f);
        src.Play();
    }
}
