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
    private readonly List<AudioSource> sfxSources = new List<AudioSource>();
    private readonly Dictionary<string, AudioClip> registry = new Dictionary<string, AudioClip>();
    private int nextVoice;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[AudioManager] Un second gestionnaire existe sur '{name}' : il est ignoré.", this);
            enabled = false;
            return;
        }

        Instance = this;

        // Une source pour la musique, N pour les effets (voix round-robin).
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = musicVolume;

        for (int i = 0; i < Mathf.Max(1, sfxVoices); i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.volume = sfxVolume;
            sfxSources.Add(src);
        }

        foreach (var clip in sfxClips)
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

    /// <summary>Lance la musique de fond en boucle. Sans effet si déjà en cours.</summary>
    public void PlayMusic()
    {
        if (musicClip == null || musicSource == null || musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = musicClip;
        musicSource.Play();
    }

    /// <summary>Arrête la musique de fond.</summary>
    public void StopMusic()
    {
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
        if (string.IsNullOrEmpty(clipName) || sfxSources.Count == 0)
        {
            return;
        }

        if (!registry.TryGetValue(clipName, out var clip) || clip == null)
        {
            return;
        }

        var src = sfxSources[nextVoice];
        nextVoice = (nextVoice + 1) % sfxSources.Count;

        src.clip = clip;
        src.Play();
    }

    /// <summary>Joue un effet sonore déjà chargé. Silencieux si null.</summary>
    public void Play(AudioClip clip)
    {
        if (clip == null || sfxSources.Count == 0)
        {
            return;
        }

        var src = sfxSources[nextVoice];
        nextVoice = (nextVoice + 1) % sfxSources.Count;

        src.clip = clip;
        src.Play();
    }
}
