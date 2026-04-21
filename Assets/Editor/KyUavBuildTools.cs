using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class KyUavBuildTools
{
    private const string MainScenePath = "Assets/Scenes/Main/MainScene.unity";
    private const string DefaultBuildDirectory = @"D:\unityhub\project\build\Ky_UAV";
    private const string ExecutableName = "Ky_UAV.exe";
    private const string BatchBuildDirectoryEnv = "KY_UAV_BUILD_DIR";

    [MenuItem("Tools/KY UAV/Build Windows Player")]
    public static void BuildWindowsPlayerMenu()
    {
        BuildWindowsPlayer(DefaultBuildDirectory);
    }

    public static void BuildWindowsPlayerBatch()
    {
        string buildDirectory = Environment.GetEnvironmentVariable(BatchBuildDirectoryEnv);
        if (string.IsNullOrWhiteSpace(buildDirectory))
        {
            buildDirectory = DefaultBuildDirectory;
        }

        BuildWindowsPlayer(buildDirectory);
    }

    public static void BuildWindowsPlayer(string buildDirectory)
    {
        if (string.IsNullOrWhiteSpace(buildDirectory))
        {
            throw new ArgumentException("Build directory must not be empty.", nameof(buildDirectory));
        }

        string fullBuildDirectory = Path.GetFullPath(buildDirectory);
        Directory.CreateDirectory(fullBuildDirectory);

        KyUavDeliveryAssetTools.BootstrapDeliveryAssets();
        ProjectSmokeValidator.RunSmokeValidation();

        BuildPlayerOptions buildOptions = new BuildPlayerOptions
        {
            scenes = new[] { MainScenePath },
            locationPathName = Path.Combine(fullBuildDirectory, ExecutableName),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException($"KY UAV Windows build failed: {report.summary.result}");
        }

        UnityEngine.Debug.Log($"[KyUavBuildTools] Windows build completed: {buildOptions.locationPathName}");
    }
}
