using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ExperimentPresetGroupInfo
{
    public string GroupKey { get; }
    public string DisplayName { get; }
    public List<ExperimentPreset> Presets { get; }

    public ExperimentPresetGroupInfo(string groupKey, string displayName, List<ExperimentPreset> presets)
    {
        GroupKey = groupKey ?? "general";
        DisplayName = displayName ?? "General";
        Presets = presets ?? new List<ExperimentPreset>();
    }
}

public static class ExperimentPresetCatalog
{
    public const string ResourcesRoot = "ExperimentPresets";

    private static readonly string[] PreferredGroupOrder =
    {
        "scheduling",
        "planning",
        "scaling",
        "density"
    };

    public static List<ExperimentPresetGroupInfo> Build(IEnumerable<ExperimentPreset> presets)
    {
        Dictionary<string, List<ExperimentPreset>> groupedPresets =
            new Dictionary<string, List<ExperimentPreset>>(StringComparer.OrdinalIgnoreCase);

        if (presets != null)
        {
            foreach (ExperimentPreset preset in presets)
            {
                if (preset == null)
                {
                    continue;
                }

                string groupKey = NormalizeGroupKey(preset.groupName);
                if (!groupedPresets.TryGetValue(groupKey, out List<ExperimentPreset> groupItems))
                {
                    groupItems = new List<ExperimentPreset>();
                    groupedPresets[groupKey] = groupItems;
                }

                groupItems.Add(preset);
            }
        }

        List<string> groupKeys = new List<string>(groupedPresets.Keys);
        groupKeys.Sort(CompareGroupKeys);

        List<ExperimentPresetGroupInfo> result = new List<ExperimentPresetGroupInfo>(groupKeys.Count);
        for (int i = 0; i < groupKeys.Count; i++)
        {
            string groupKey = groupKeys[i];
            List<ExperimentPreset> groupItems = groupedPresets[groupKey];
            groupItems.Sort(ComparePresetNames);
            result.Add(new ExperimentPresetGroupInfo(groupKey, GetGroupDisplayName(groupKey), groupItems));
        }

        return result;
    }

    public static string NormalizeGroupKey(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return "general";
        }

        return groupName.Trim().ToLowerInvariant();
    }

    public static string GetGroupDisplayName(string groupKey)
    {
        switch (NormalizeGroupKey(groupKey))
        {
            case "scheduling":
                return "Scheduling";
            case "planning":
                return "Planning";
            case "scaling":
                return "Scaling";
            case "density":
                return "Density";
            case "general":
                return "General";
            default:
                return ToDisplayCase(groupKey);
        }
    }

    public static string GetPresetDisplayName(ExperimentPreset preset)
    {
        if (preset == null)
        {
            return "Unknown Preset";
        }

        if (!string.IsNullOrWhiteSpace(preset.presetName))
        {
            return preset.presetName.Trim();
        }

        return string.IsNullOrWhiteSpace(preset.name) ? "Unnamed Preset" : preset.name.Trim();
    }

    public static string GetPresetShortLabel(ExperimentPreset preset)
    {
        string label = GetPresetDisplayName(preset);
        int firstSpaceIndex = label.IndexOf(' ');
        if (firstSpaceIndex > 0)
        {
            string prefix = label.Substring(0, firstSpaceIndex);
            if (string.Equals(prefix, "Scheduling", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prefix, "Planning", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prefix, "Scaling", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prefix, "Density", StringComparison.OrdinalIgnoreCase))
            {
                label = label.Substring(firstSpaceIndex + 1).Trim();
            }
        }

        return label;
    }

    private static int CompareGroupKeys(string left, string right)
    {
        int leftIndex = GetPreferredGroupIndex(left);
        int rightIndex = GetPreferredGroupIndex(right);
        if (leftIndex != rightIndex)
        {
            return leftIndex.CompareTo(rightIndex);
        }

        return string.Compare(GetGroupDisplayName(left), GetGroupDisplayName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static int ComparePresetNames(ExperimentPreset left, ExperimentPreset right)
    {
        return string.Compare(GetPresetDisplayName(left), GetPresetDisplayName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static int GetPreferredGroupIndex(string groupKey)
    {
        string normalizedGroupKey = NormalizeGroupKey(groupKey);
        for (int i = 0; i < PreferredGroupOrder.Length; i++)
        {
            if (string.Equals(PreferredGroupOrder[i], normalizedGroupKey, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return PreferredGroupOrder.Length;
    }

    private static string ToDisplayCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "General";
        }

        string normalized = value.Trim();
        if (normalized.Length == 1)
        {
            return normalized.ToUpperInvariant();
        }

        return char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
    }
}
