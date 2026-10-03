#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Development-only opt-in recorder for GDD §Performances. It never runs during normal play.
/// Usage: Neutral.exe -neutralQualityProbe D:/.../report.json -screen-width 1920 -screen-height 1080
/// </summary>
public class NeutralQualityProbe : MonoBehaviour
{
    [Serializable]
    private class Result
    {
        public string unity, platform, cpu, gpu, graphicsApi, scene, scope;
        public int width, height, samples, targetFrameRate, vSync, drawCalls, triangles;
        public float medianFrameMs, p95FrameMs, p99FrameMs, maxAudioPeak, maxAudioRms;
        public long unityAllocatedBytes, monoUsedBytes, audioReservedBytes;
        public int exceptions, warnings;
        public bool gameStarted, ballLaunched, screenshot;
    }
    private string output;
    private readonly List<float> frames = new List<float>(4096);
    private readonly float[] audioSamples = new float[1024];
    private readonly Result result = new Result();
    private ProfilerRecorder drawCalls, triangles, audioMemory;
    private bool measuring;
    private GameManager game;
    private Plunger plunger;
    private AudioManager audioManager;
    private Action<bool> leftInput, rightInput;
    private Camera view;
    private RenderTexture renderedFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Begin()
    {
        if (!Debug.isDebugBuild) return;
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-neutralQualityProbe");
        if (index < 0 || index + 1 >= args.Length) return;
        var host = new GameObject("NeutralQualityProbe_OptIn");
        host.hideFlags = HideFlags.DontSave;
        var recorder = host.AddComponent<NeutralQualityProbe>();
        recorder.output = Path.GetFullPath(args[index + 1]);
    }

    private IEnumerator Start()
    {
        Application.runInBackground = true;
        Application.logMessageReceived += Log;
        game = FindAnyObjectByType<GameManager>();
        plunger = FindAnyObjectByType<Plunger>();
        audioManager = FindAnyObjectByType<AudioManager>();
        view = Camera.main;
        if (view != null)
        {
            renderedFrame = new RenderTexture(1920, 1080, 24);
            renderedFrame.Create();
            view.targetTexture = renderedFrame;
            view.enabled = false;
            var viewport = view.GetComponent<CabinetViewport>();
            if (viewport != null) viewport.Fit();
        }
        var router = FindAnyObjectByType<InputRouter>();
        if (router != null)
        {
            leftInput = (Action<bool>)typeof(InputRouter).GetProperty("LeftFlipperHeld").GetSetMethod(true).CreateDelegate(typeof(Action<bool>), router);
            rightInput = (Action<bool>)typeof(InputRouter).GetProperty("RightFlipperHeld").GetSetMethod(true).CreateDelegate(typeof(Action<bool>), router);
        }
        drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        audioMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Audio Reserved Memory");
        yield return new WaitForSecondsRealtime(2f);
        result.gameStarted = game != null && game.State == GameManager.GameState.ReadyToLaunch;
        // Exercise the real launcher and let the ball circulate, rather than profiling an empty table.
        if (plunger != null)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(Plunger).GetField("pullAmount", flags).SetValue(plunger, .7f);
            typeof(Plunger).GetMethod("Launch", flags).Invoke(plunger, null);
            result.ballLaunched = game != null && game.State == GameManager.GameState.Playing;
        }
        yield return new WaitForSecondsRealtime(3f);
        if (view != null && renderedFrame != null)
        {
            view.Render();
            var active = RenderTexture.active;
            RenderTexture.active = renderedFrame;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.ChangeExtension(output, ".png"), image.EncodeToPNG());
            RenderTexture.active = active;
            Destroy(image);
            result.screenshot = true;
        }
        measuring = true;
        // Exercise the new sound bank during the measured interval.
        for (int i = 0; i < 30; i++)
        {
            // Keep this short workload active without letting the probe commit a game-over record.
            if (game != null && game.BallsRemaining < 3) game.StartGame();
            if (game != null && game.State == GameManager.GameState.ReadyToLaunch && plunger != null)
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(Plunger).GetField("pullAmount", flags).SetValue(plunger, .7f);
                typeof(Plunger).GetMethod("Launch", flags).Invoke(plunger, null);
            }
            if (audioManager != null) audioManager.Play((PinballSound)(i % 10));
            yield return new WaitForSecondsRealtime(.5f);
        }
        measuring = false;
        frames.Sort();
        result.unity = Application.unityVersion;
        result.platform = Application.platform.ToString();
        result.cpu = SystemInfo.processorType;
        result.gpu = SystemInfo.graphicsDeviceName;
        result.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
        result.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        result.scope = "Development player launched hidden, forced Camera.Render into 1920x1080 RenderTexture each frame; repeated ball launches + automatic flipper cycles + music + cues. No display presentation, human input or cabinet hardware. Wall frame times include the forced render. Not an isolated GPU or presentation-latency benchmark.";
        result.width = Screen.width;
        result.height = Screen.height;
        result.samples = frames.Count;
        result.targetFrameRate = Application.targetFrameRate;
        result.vSync = QualitySettings.vSyncCount;
        result.medianFrameMs = Percentile(.5f);
        result.p95FrameMs = Percentile(.95f);
        result.p99FrameMs = Percentile(.99f);
        result.drawCalls = drawCalls.Valid ? (int)drawCalls.LastValue : -1;
        result.triangles = triangles.Valid ? (int)triangles.LastValue : -1;
        result.audioReservedBytes = audioMemory.Valid ? audioMemory.LastValue : -1;
        result.unityAllocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        result.monoUsedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonUtility.ToJson(result, true));
        Debug.Log("[NeutralQualityProbe] " + output);
        Application.Quit(result.exceptions == 0 && result.gameStarted && result.ballLaunched ? 0 : 1);
    }

    private void LateUpdate()
    {
        if (view != null && renderedFrame != null) view.Render();
        if (!measuring) return;
        float cycle = Time.unscaledTime % .6f;
        leftInput?.Invoke(cycle < .18f);
        rightInput?.Invoke(cycle > .3f && cycle < .48f);
        frames.Add(Time.unscaledDeltaTime * 1000f);
        AudioListener.GetOutputData(audioSamples, 0);
        double energy = 0;
        float peak = 0;
        for (int i = 0; i < audioSamples.Length; i++)
        {
            float value = audioSamples[i];
            energy += value * value;
            peak = Mathf.Max(peak, Mathf.Abs(value));
        }
        result.maxAudioPeak = Mathf.Max(result.maxAudioPeak, peak);
        result.maxAudioRms = Mathf.Max(result.maxAudioRms, (float)Math.Sqrt(energy / audioSamples.Length));
    }
    private float Percentile(float p) => frames.Count > 0 ? frames[Mathf.Min(frames.Count-1, Mathf.FloorToInt(p * frames.Count))] : -1;
    private void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) result.exceptions++;
        else if (type == LogType.Warning) result.warnings++;
    }
    private void OnDestroy()
    {
        Application.logMessageReceived -= Log;
        drawCalls.Dispose(); triangles.Dispose(); audioMemory.Dispose();
        if (renderedFrame != null) { renderedFrame.Release(); Destroy(renderedFrame); }
    }
}
#endif
