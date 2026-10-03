using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Local validation build with an explicit scene list; does not change Build Settings.</summary>
public static class BuildNeutralValidationPlayer
{
    public static void Queue()
    {
        EditorApplication.update -= RunQueued;
        EditorApplication.update += RunQueued;
        EditorApplication.QueuePlayerLoopUpdate();
    }
    private static void RunQueued()
    {
        EditorApplication.update -= RunQueued;
        try { Build(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    [MenuItem("Pinball/Neutral/Build Windows de contrôle")]
    public static void Build()
    {
        if (Application.isPlaying || EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Neutral.unity" || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Neutral enregistrée hors Play requise.");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            throw new InvalidOperationException("Activer Windows64 avant ce build.");
        string project = Path.GetDirectoryName(Application.dataPath);
        string folder = Path.Combine(project, "Tools/build/NeutralVpeQuality");
        string output = Path.Combine(project, "Tools/unity/out/vpe-windows-build.txt");
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        try
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[]{ "Assets/Scenes/Neutral.unity" },
                locationPathName = Path.Combine(folder, "Neutral.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            var sb = new StringBuilder();
            sb.AppendLine($"Result={report.summary.result} platform={report.summary.platform} size={report.summary.totalSize} duration={report.summary.totalTime} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings}");
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type == LogType.Error || message.type == LogType.Warning || message.type == LogType.Exception)
                        sb.AppendLine(message.type + ": " + message.content);
            File.WriteAllText(output, sb.ToString());
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Build failed; see " + output);
        }
        catch (Exception exception)
        {
            File.AppendAllText(output, exception + Environment.NewLine);
            throw;
        }
    }
}
