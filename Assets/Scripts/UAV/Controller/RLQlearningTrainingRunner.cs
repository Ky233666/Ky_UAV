using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class RLQlearningTrainingRunner : MonoBehaviour
{
    [Header("Python")]
    public string pythonExecutablePath = "";
    public bool preferProjectVirtualEnvironment = true;
    public bool allowSystemPythonFallback = true;
    public string trainerScriptName = "train_qlearning.py";
    public int timeoutSeconds = 120;

    public bool IsTraining { get; private set; }
    public string LastTrainingCaseName { get; private set; } = "";
    public string LastTrainingMessage { get; private set; } = "RL trainer not started";
    public string LastStdout { get; private set; } = "";
    public string LastStderr { get; private set; } = "";

    private Process activeProcess;

    public bool TryStartTraining(string caseName, Action<bool, string> onCompleted)
    {
        if (IsTraining)
        {
            LastTrainingMessage = "RL training is already running";
            return false;
        }

        if (string.IsNullOrWhiteSpace(caseName))
        {
            LastTrainingMessage = "No RL case selected";
            return false;
        }

        string caseDirectory = RLPathPlanningFileUtility.GetCaseDirectory(caseName);
        string mapPath = RLPathPlanningFileUtility.GetCaseMapPath(caseName);
        if (!Directory.Exists(caseDirectory) || !File.Exists(mapPath))
        {
            LastTrainingMessage = $"RL case map not found: {mapPath}";
            return false;
        }

        string moduleDirectory = RLPathPlanningFileUtility.GetProjectModuleDirectory();
        string trainerScriptPath = Path.Combine(moduleDirectory, trainerScriptName);
        if (!File.Exists(trainerScriptPath))
        {
            LastTrainingMessage = $"Q-learning trainer not found: {trainerScriptPath}";
            return false;
        }

        string pythonPath = ResolvePythonExecutable(moduleDirectory);
        if (string.IsNullOrWhiteSpace(pythonPath))
        {
            LastTrainingMessage =
                "Python executable not found. Expected RLPathPlanning/.venv/Scripts/python.exe or a system python on PATH.";
            return false;
        }

        LastTrainingCaseName = caseName;
        LastStdout = "";
        LastStderr = "";
        LastTrainingMessage = $"Training RL case: {caseName}";
        StartCoroutine(RunTrainingProcess(pythonPath, trainerScriptPath, moduleDirectory, caseName, onCompleted));
        return true;
    }

    private IEnumerator RunTrainingProcess(
        string pythonPath,
        string trainerScriptPath,
        string moduleDirectory,
        string caseName,
        Action<bool, string> onCompleted)
    {
        StringBuilder stdout = new StringBuilder(1024);
        StringBuilder stderr = new StringBuilder(1024);
        IsTraining = true;

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"{Quote(trainerScriptPath)} --case {Quote(caseName)}",
            WorkingDirectory = moduleDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            activeProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = false };
            activeProcess.OutputDataReceived += (_, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    stdout.AppendLine(args.Data);
                }
            };
            activeProcess.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    stderr.AppendLine(args.Data);
                }
            };

            activeProcess.Start();
            activeProcess.BeginOutputReadLine();
            activeProcess.BeginErrorReadLine();
        }
        catch (Exception exception)
        {
            IsTraining = false;
            LastTrainingMessage = $"Failed to start Python trainer: {exception.Message}";
            activeProcess?.Dispose();
            activeProcess = null;
            onCompleted?.Invoke(false, LastTrainingMessage);
            yield break;
        }

        float startedAt = Time.realtimeSinceStartup;
        while (activeProcess != null && !activeProcess.HasExited)
        {
            if (timeoutSeconds > 0 && Time.realtimeSinceStartup - startedAt > timeoutSeconds)
            {
                TryKillActiveProcess();
                LastStdout = stdout.ToString();
                LastStderr = stderr.ToString();
                LastTrainingMessage = $"RL training timed out after {timeoutSeconds}s";
                IsTraining = false;
                onCompleted?.Invoke(false, LastTrainingMessage);
                yield break;
            }

            LastStdout = stdout.ToString();
            LastStderr = stderr.ToString();
            LastTrainingMessage = $"Training RL case: {caseName}...";
            yield return null;
        }

        int exitCode = activeProcess != null ? activeProcess.ExitCode : -1;
        activeProcess?.Dispose();
        activeProcess = null;

        LastStdout = stdout.ToString();
        LastStderr = stderr.ToString();

        string casePath = RLPathPlanningFileUtility.GetCasePathPath(caseName);
        bool success = exitCode == 0 && File.Exists(casePath);
        LastTrainingMessage = success
            ? $"RL training finished: {caseName}"
            : BuildFailureMessage(exitCode, caseName, LastStderr);
        IsTraining = false;

        Debug.Log($"[RLQlearningTrainingRunner] {LastTrainingMessage}\n{LastStdout}");
        if (!success && !string.IsNullOrWhiteSpace(LastStderr))
        {
            Debug.LogWarning($"[RLQlearningTrainingRunner] stderr:\n{LastStderr}");
        }

        onCompleted?.Invoke(success, LastTrainingMessage);
    }

    private string ResolvePythonExecutable(string moduleDirectory)
    {
        if (!string.IsNullOrWhiteSpace(pythonExecutablePath))
        {
            string configuredPath = pythonExecutablePath;
            if (!Path.IsPathRooted(configuredPath))
            {
                configuredPath = Path.Combine(moduleDirectory, configuredPath);
            }

            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }
        }

        if (preferProjectVirtualEnvironment)
        {
            string venvPython = Path.Combine(moduleDirectory, ".venv", "Scripts", "python.exe");
            if (File.Exists(venvPython))
            {
                return venvPython;
            }
        }

        if (allowSystemPythonFallback)
        {
            return ResolveSystemPythonExecutable();
        }

        return "";
    }

    private static string ResolveSystemPythonExecutable()
    {
        string[] environmentKeys = { "PYTHON", "PYTHON_EXE", "PYTHONHOME" };
        for (int i = 0; i < environmentKeys.Length; i++)
        {
            string value = Environment.GetEnvironmentVariable(environmentKeys[i]);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            string candidate = value;
            if (Directory.Exists(candidate))
            {
                candidate = Path.Combine(candidate, "python.exe");
            }

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] pathEntries = pathVariable.Split(Path.PathSeparator);
        for (int i = 0; i < pathEntries.Length; i++)
        {
            string directory = pathEntries[i];
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            string candidate = Path.Combine(directory.Trim(), "python.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "";
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private static string BuildFailureMessage(int exitCode, string caseName, string stderr)
    {
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            string trimmed = stderr.Trim();
            string firstLine = trimmed.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
            return $"RL training failed for {caseName}: {firstLine}";
        }

        return $"RL training failed for {caseName}, exit code {exitCode}";
    }

    private void TryKillActiveProcess()
    {
        try
        {
            if (activeProcess != null && !activeProcess.HasExited)
            {
                activeProcess.Kill();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RLQlearningTrainingRunner] Failed to kill Python process: {exception.Message}");
        }
        finally
        {
            activeProcess?.Dispose();
            activeProcess = null;
        }
    }

    private void OnDestroy()
    {
        TryKillActiveProcess();
    }
}
