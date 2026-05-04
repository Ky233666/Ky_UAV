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
    private const string RLModuleFolderName = "RLPathPlanning";
    private static readonly string[] RLRootFiles =
    {
        ".gitignore",
        "config.py",
        "export_result.py",
        "grid_env.py",
        "qlearning_agent.py",
        "README.md",
        "train_qlearning.py"
    };

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

        CopyRLPathPlanningModule(fullBuildDirectory);
        UnityEngine.Debug.Log($"[KyUavBuildTools] Windows build completed: {buildOptions.locationPathName}");
    }

    private static void CopyRLPathPlanningModule(string fullBuildDirectory)
    {
        string sourceDirectory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", RLModuleFolderName));
        string targetDirectory = Path.Combine(fullBuildDirectory, RLModuleFolderName);
        if (!Directory.Exists(sourceDirectory))
        {
            UnityEngine.Debug.LogWarning($"[KyUavBuildTools] RL module directory not found: {sourceDirectory}");
            return;
        }

        Directory.CreateDirectory(targetDirectory);
        for (int i = 0; i < RLRootFiles.Length; i++)
        {
            CopyFileIfExists(
                Path.Combine(sourceDirectory, RLRootFiles[i]),
                Path.Combine(targetDirectory, RLRootFiles[i]));
        }

        CopyDirectory(
            Path.Combine(sourceDirectory, ".venv"),
            Path.Combine(targetDirectory, ".venv"));

        EnsureRuntimeDirectory(Path.Combine(targetDirectory, "input"));
        EnsureRuntimeDirectory(Path.Combine(targetDirectory, "output"));
        EnsureRuntimeDirectory(Path.Combine(targetDirectory, "cases"));

        UnityEngine.Debug.Log($"[KyUavBuildTools] Copied RL path planning module to: {targetDirectory}");
    }

    private static void CopyFileIfExists(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
        {
            UnityEngine.Debug.LogWarning($"[KyUavBuildTools] RL module file not found: {sourcePath}");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
        File.Copy(sourcePath, targetPath, true);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            UnityEngine.Debug.LogWarning($"[KyUavBuildTools] RL module subdirectory not found: {sourceDirectory}");
            return;
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, directory);
            if (ShouldSkipRLModulePath(relativePath))
            {
                continue;
            }

            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, file);
            if (ShouldSkipRLModulePath(relativePath))
            {
                continue;
            }

            string targetPath = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
            File.Copy(file, targetPath, true);
        }
    }

    private static bool ShouldSkipRLModulePath(string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/');
        return normalized.Contains("/__pycache__/") ||
               normalized.StartsWith("__pycache__/") ||
               normalized.EndsWith(".pyc", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureRuntimeDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        string keepPath = Path.Combine(directory, ".gitkeep");
        if (!File.Exists(keepPath))
        {
            File.WriteAllText(keepPath, string.Empty);
        }
    }
}
