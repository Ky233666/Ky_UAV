using System.IO;
using System.Collections.Generic;
using UnityEngine;

public static class RLPathPlanningFileUtility
{
    public const string ModuleFolderName = "RLPathPlanning";
    public const string InputFolderName = "input";
    public const string OutputFolderName = "output";
    public const string CasesFolderName = "cases";
    public const string DefaultMapFileName = "map.json";
    public const string DefaultPathFileName = "path.json";
    public const string DefaultPolicyFileName = "policy.json";
    public const string DefaultRewardLogFileName = "reward_log.csv";

    public static string SelectedCaseName { get; private set; } = "";

    public static string GetProjectModuleDirectory()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ModuleFolderName));
    }

    public static string GetProjectInputDirectory()
    {
        return Path.Combine(GetProjectModuleDirectory(), InputFolderName);
    }

    public static string GetProjectOutputDirectory()
    {
        return Path.Combine(GetProjectModuleDirectory(), OutputFolderName);
    }

    public static string GetProjectCasesDirectory()
    {
        return Path.Combine(GetProjectModuleDirectory(), CasesFolderName);
    }

    public static string GetPersistentOutputDirectory()
    {
        return Path.Combine(Application.persistentDataPath, ModuleFolderName, OutputFolderName);
    }

    public static string GetDefaultMapPath()
    {
        return Path.Combine(GetProjectInputDirectory(), DefaultMapFileName);
    }

    public static string GetDefaultPathPath()
    {
        return Path.Combine(GetProjectOutputDirectory(), DefaultPathFileName);
    }

    public static string CreateCaseName(int droneId, int taskId)
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"map_drone_{droneId:D2}_task_{taskId:D3}_{timestamp}";
    }

    public static void SetSelectedCaseName(string caseName)
    {
        SelectedCaseName = SanitizeCaseName(caseName);
    }

    public static string GetCaseDirectory(string caseName)
    {
        return Path.Combine(GetProjectCasesDirectory(), SanitizeCaseName(caseName));
    }

    public static string GetCaseMapPath(string caseName)
    {
        return Path.Combine(GetCaseDirectory(caseName), DefaultMapFileName);
    }

    public static string GetCasePathPath(string caseName)
    {
        return Path.Combine(GetCaseDirectory(caseName), DefaultPathFileName);
    }

    public static string GetCasePolicyPath(string caseName)
    {
        return Path.Combine(GetCaseDirectory(caseName), DefaultPolicyFileName);
    }

    public static string GetCaseRewardLogPath(string caseName)
    {
        return Path.Combine(GetCaseDirectory(caseName), DefaultRewardLogFileName);
    }

    public static bool TryGetSelectedCaseName(out string caseName)
    {
        if (!string.IsNullOrWhiteSpace(SelectedCaseName) &&
            Directory.Exists(GetCaseDirectory(SelectedCaseName)))
        {
            caseName = SelectedCaseName;
            return true;
        }

        List<string> cases = GetCaseNames();
        if (cases.Count > 0)
        {
            caseName = cases[0];
            SelectedCaseName = caseName;
            return true;
        }

        caseName = "";
        return false;
    }

    public static string GetSelectedCaseMapPath()
    {
        return TryGetSelectedCaseName(out string caseName)
            ? GetCaseMapPath(caseName)
            : GetDefaultMapPath();
    }

    public static string GetSelectedCasePathPath()
    {
        return TryGetSelectedCaseName(out string caseName)
            ? GetCasePathPath(caseName)
            : GetDefaultPathPath();
    }

    public static List<string> GetCaseNames()
    {
        List<string> result = new List<string>();
        string casesDirectory = GetProjectCasesDirectory();
        if (!Directory.Exists(casesDirectory))
        {
            Directory.CreateDirectory(casesDirectory);
            return result;
        }

        DirectoryInfo root = new DirectoryInfo(casesDirectory);
        DirectoryInfo[] directories = root.GetDirectories();
        System.Array.Sort(directories, (left, right) =>
        {
            int timeCompare = right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc);
            return timeCompare != 0 ? timeCompare : string.CompareOrdinal(left.Name, right.Name);
        });

        for (int i = 0; i < directories.Length; i++)
        {
            string mapPath = Path.Combine(directories[i].FullName, DefaultMapFileName);
            if (File.Exists(mapPath))
            {
                result.Add(directories[i].Name);
            }
        }

        return result;
    }

    public static string GetNamedMapPath(int droneId, int taskId)
    {
        return Path.Combine(GetProjectInputDirectory(), $"map_drone_{droneId:D2}_task_{taskId:D3}.json");
    }

    public static string GetNamedPathPath(int droneId, int taskId)
    {
        return Path.Combine(GetProjectOutputDirectory(), $"path_drone_{droneId:D2}_task_{taskId:D3}.json");
    }

    private static string SanitizeCaseName(string caseName)
    {
        string trimmed = string.IsNullOrWhiteSpace(caseName) ? "case" : caseName.Trim();
        char[] invalidChars = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidChars.Length; i++)
        {
            trimmed = trimmed.Replace(invalidChars[i], '_');
        }

        return trimmed.Replace(' ', '_');
    }
}
