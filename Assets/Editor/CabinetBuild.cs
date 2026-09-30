using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CabinetBuild
{
    [Serializable]
    private sealed class Manifest
    {
        public int heartbeatProtocol = 1;
        public string builtUtc;
        public string unityVersion;
        public string buildGuid;
        public string[] scenes;
        public int warnings;
        public int errors;
    }

    [MenuItem("MASSIVE/Cabinet/Build Watchdog Player")]
    public static void BuildFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before building.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string output = Path.GetFullPath("Builds/Cabinet-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-Watchdog/MASSIVE.exe");
        Build(output);
        Debug.Log("[CabinetBuild] Ready: " + Path.Combine(Path.GetDirectoryName(output), "Start-Cabinet.cmd"));
    }

    // Unity -batchmode -quit -executeMethod CabinetBuild.BuildBatch
    //       -cabinet-build-output <absolute output path ending in MASSIVE.exe>
    public static void BuildBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-cabinet-build-output");
        if (index < 0 || index + 1 >= args.Length)
            throw new ArgumentException("Missing -cabinet-build-output.");
        Build(Path.GetFullPath(args[index + 1]));
    }

    private static void Build(string output)
    {
        if (!string.Equals(Path.GetFileName(output), "MASSIVE.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The watchdog player must be named MASSIVE.exe.");
        string directory = Path.GetDirectoryName(output);
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            throw new InvalidOperationException("Choose a new output directory; existing builds are preserved.");
        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length == 0 || Path.GetFileNameWithoutExtension(scenes[0]) != SceneFlow.ChooseModeScene)
            throw new InvalidOperationException("The cabinet build must start with the Attract scene.");
        string[] launcherFiles = { "Watchdog.ps1", "watchdog.json", "Start-Cabinet.cmd", "Stop-Cabinet.cmd", "Cabinet-Status.cmd", "README.md" };
        foreach (string file in launcherFiles)
            if (!File.Exists(Path.Combine("Tools/Cabinet", file))) throw new FileNotFoundException("Missing watchdog source: " + file);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = scenes, locationPathName = output,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Cabinet build failed: " + report.summary.result);
        foreach (string file in launcherFiles)
            File.Copy(Path.Combine("Tools/Cabinet", file), Path.Combine(directory, file));
        var manifest = new Manifest {
            builtUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
            buildGuid = report.summary.guid.ToString(), scenes = scenes,
            warnings = report.summary.totalWarnings, errors = report.summary.totalErrors
        };
        File.WriteAllText(Path.Combine(directory, "watchdog-build.json"), JsonUtility.ToJson(manifest, true));
        Debug.Log($"[CabinetBuild] Succeeded: {report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings, {report.summary.totalTime.TotalSeconds:F2}s. {output}");
    }
}
