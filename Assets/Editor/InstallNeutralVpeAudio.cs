using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Explicit, undoable Neutral audio authoring; leaves subsequent manual settings intact.</summary>
public static class InstallNeutralVpeAudio
{
    private const string Folder = "Assets/Generated/NeutralVpeAudio";
    private const string Action = "Installer l'audio de Neutral";

    [MenuItem("Pinball/Neutral/Installer les sons et le mixage")]
    public static void Install()
    {
        if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/Neutral.unity")
            throw new InvalidOperationException("Ouvrir Neutral hors Play.");
        var manager = UnityEngine.Object.FindAnyObjectByType<AudioManager>();
        if (manager == null) throw new InvalidOperationException("AudioManager absent.");
        var serialized = new SerializedObject(manager);
        if (serialized.FindProperty("config").objectReferenceValue != null) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(Action);
        try
        {
            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>(Folder + "/NeutralAudioConfig.asset");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<AudioConfig>();
                config.musicVolume = serialized.FindProperty("musicVolume").floatValue;
                config.effectsVolume = serialized.FindProperty("sfxVolume").floatValue;
                var mixer = Mixer();
                config.musicGroup = mixer.FindMatchingGroups("Music").First();
                config.mechanicalGroup = mixer.FindMatchingGroups("Mechanical").First();
                config.rewardGroup = mixer.FindMatchingGroups("Rewards").First();
                config.cues = new[]
                {
                    Cue(PinballSound.FlipperUp, .75f, true, "flipper_up_0", "flipper_up_1", "flipper_up_2"),
                    Cue(PinballSound.FlipperDown, .6f, true, "flipper_down_0", "flipper_down_1", "flipper_down_2"),
                    Cue(PinballSound.Launch, .8f, true, "launch_0", "launch_1"),
                    Cue(PinballSound.TargetValidated, .55f, false, "target_validated"),
                    Cue(PinballSound.RampCompleted, .6f, false, "ramp_completed"),
                    Cue(PinballSound.LoopCompleted, .6f, false, "loop_completed"),
                    Cue(PinballSound.MultiplierRaised, .65f, false, "multiplier_raised"),
                    Cue(PinballSound.ExtraBall, .65f, false, "extra_ball"),
                    Cue(PinballSound.GameOver, .6f, false, "game_over"),
                    Cue(PinballSound.HighScore, .7f, false, "high_score")
                };
                AssetDatabase.CreateAsset(config, Folder + "/NeutralAudioConfig.asset");
                Undo.RegisterCreatedObjectUndo(config, Action);
            }
            Undo.RecordObject(manager, Action);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedProperties();
            if (manager.GetComponent<PinballGameAudio>() == null) Undo.AddComponent<PinballGameAudio>(manager.gameObject);
            foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>().Where(c =>
                c is Flipper || c is Plunger || c is SubjectTarget || c is RampGate r && !r.IsEntry || c is LoopGate l && !l.IsEntry))
                if (component.GetComponent<PinballMechanismAudio>() == null) Undo.AddComponent<PinballMechanismAudio>(component.gameObject);
            var source = GameObject.Find("BackgroundMusic");
            if (source != null && source.TryGetComponent<AudioSource>(out var music))
            {
                Undo.RecordObject(music, Action);
                music.volume = config.musicVolume;
                music.outputAudioMixerGroup = config.musicGroup;
                var path = AssetDatabase.GetAssetPath(music.clip);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer != null)
                {
                    Undo.RecordObject(importer, Action);
                    var settings = importer.defaultSampleSettings;
                    settings.loadType = AudioClipLoadType.Streaming;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = .7f;
                    settings.preloadAudioData = false;
                    importer.defaultSampleSettings = settings;
                    importer.loadInBackground = true;
                    importer.SaveAndReimport();
                }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[]{ Folder, "Assets/Generated/NeutralPresentation/Audio" }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as AudioImporter;
                if (importer == null) continue;
                Undo.RecordObject(importer, Action);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    private static AudioConfig.Cue Cue(PinballSound sound, float volume, bool mechanical, params string[] names)
    {
        var clips = names.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/" + n + ".wav")).ToArray();
        if (clips.Any(c => c == null)) throw new InvalidOperationException("Missing PCM clip for " + sound);
        return new AudioConfig.Cue { sound = sound, clips = clips, volume = volume, mechanical = mechanical,
            pitchSemitones = mechanical ? .2f : 0f, cooldown = mechanical ? 0f : .08f };
    }

    // These Editor APIs exist in this project's Unity version but are internal. Runtime uses only public APIs.
    private static AudioMixer Mixer()
    {
        string path = Folder + "/NeutralMixer.mixer";
        var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
        var type = typeof(Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController", true);
        var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var mixer = existing != null ? existing : (AudioMixer)type.GetMethod("CreateMixerControllerAtPath", flags).Invoke(null, new object[]{path});
        if (existing == null) Undo.RegisterCreatedObjectUndo(mixer, Action);
        else Undo.RecordObject(mixer, Action);
        var master = type.GetProperty("masterGroup").GetValue(mixer);
        var groupType = master.GetType();
        var create = type.GetMethod("CreateNewGroup", flags);
        if (existing == null)
        {
            var groups = Array.CreateInstance(groupType, 3);
            string[] names = { "Music", "Mechanical", "Rewards" };
            for (int i = 0; i < 3; i++) groups.SetValue(create.Invoke(mixer, new object[]{names[i], true}), i);
            groupType.GetProperty("children").SetValue(master, groups);
        }
        var snapshot = type.GetProperty("TargetSnapshot", flags).GetValue(mixer);
        groupType.GetMethod("SetValueForVolume", flags).Invoke(master, new object[]{mixer, snapshot, -6f});
        EditorUtility.SetDirty(mixer);
        return mixer;
    }
}
