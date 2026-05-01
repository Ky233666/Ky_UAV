using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class SimulationRuntimeControlPanel
{
    private void EnsureExperimentPresetCatalogLoaded()
    {
        if (experimentPresetGroups.Count > 0)
        {
            ClampExperimentSelection();
            return;
        }

        ExperimentPreset[] presets = Resources.LoadAll<ExperimentPreset>(ExperimentPresetCatalog.ResourcesRoot);
        experimentPresetGroups.Clear();
        experimentPresetGroups.AddRange(ExperimentPresetCatalog.Build(presets));
        ClampExperimentSelection();
    }

    private void SyncExperimentSelectionFromSystems()
    {
        ExperimentPreset activePreset = ResolveConfiguredExperimentPreset();
        if (activePreset == null)
        {
            ClampExperimentSelection();
            return;
        }

        SelectExperimentPreset(activePreset);
    }

    private ExperimentPreset ResolveConfiguredExperimentPreset()
    {
        if (batchExperimentRunner == null)
        {
            return null;
        }

        if (batchExperimentRunner.ActivePreset != null)
        {
            return batchExperimentRunner.ActivePreset;
        }

        if (!string.IsNullOrWhiteSpace(batchExperimentRunner.experimentPresetResourcePath))
        {
            return Resources.Load<ExperimentPreset>(batchExperimentRunner.experimentPresetResourcePath);
        }

        return null;
    }

    private void SelectExperimentPreset(ExperimentPreset preset)
    {
        if (preset == null)
        {
            ClampExperimentSelection();
            return;
        }

        for (int groupIndex = 0; groupIndex < experimentPresetGroups.Count; groupIndex++)
        {
            ExperimentPresetGroupInfo group = experimentPresetGroups[groupIndex];
            for (int presetIndex = 0; presetIndex < group.Presets.Count; presetIndex++)
            {
                if (group.Presets[presetIndex] != preset)
                {
                    continue;
                }

                selectedExperimentGroupIndex = groupIndex;
                selectedExperimentPresetIndex = presetIndex;
                return;
            }
        }

        ClampExperimentSelection();
    }

    private void ClampExperimentSelection()
    {
        if (experimentPresetGroups.Count == 0)
        {
            selectedExperimentGroupIndex = 0;
            selectedExperimentPresetIndex = 0;
            return;
        }

        selectedExperimentGroupIndex = Mathf.Clamp(selectedExperimentGroupIndex, 0, experimentPresetGroups.Count - 1);
        ExperimentPresetGroupInfo selectedGroup = experimentPresetGroups[selectedExperimentGroupIndex];
        if (selectedGroup.Presets.Count == 0)
        {
            selectedExperimentPresetIndex = 0;
            return;
        }

        selectedExperimentPresetIndex = Mathf.Clamp(selectedExperimentPresetIndex, 0, selectedGroup.Presets.Count - 1);
    }

    private ExperimentPresetGroupInfo GetSelectedExperimentGroup()
    {
        if (experimentPresetGroups.Count == 0)
        {
            return null;
        }

        selectedExperimentGroupIndex = Mathf.Clamp(selectedExperimentGroupIndex, 0, experimentPresetGroups.Count - 1);
        return experimentPresetGroups[selectedExperimentGroupIndex];
    }

    private ExperimentPreset GetSelectedExperimentPreset()
    {
        ExperimentPresetGroupInfo selectedGroup = GetSelectedExperimentGroup();
        if (selectedGroup == null || selectedGroup.Presets.Count == 0)
        {
            return null;
        }

        selectedExperimentPresetIndex = Mathf.Clamp(selectedExperimentPresetIndex, 0, selectedGroup.Presets.Count - 1);
        return selectedGroup.Presets[selectedExperimentPresetIndex];
    }

    private string GetSelectedExperimentGroupDisplayName()
    {
        ExperimentPresetGroupInfo selectedGroup = GetSelectedExperimentGroup();
        return selectedGroup != null ? selectedGroup.DisplayName : "No Presets";
    }

    private void LoadPresetIntoRuntimeConfiguration(ExperimentPreset preset)
    {
        if (preset == null)
        {
            return;
        }

        schedulerIndex = IndexOfSchedulerOption(preset.scheduler);
        plannerIndex = IndexOfPlannerOption(preset.planner);
        configuredDroneCount = Mathf.Clamp(preset.droneCount, MinDroneCount, MaxDroneCount);
        configuredBatchRunCount = Mathf.Clamp(preset.batchRuns, MinBatchRunCount, MaxBatchRunCount);
        configuredPlanningMinX = preset.planningWorldMin.x;
        configuredPlanningMaxX = preset.planningWorldMax.x;
        configuredPlanningMinZ = preset.planningWorldMin.z;
        configuredPlanningMaxZ = preset.planningWorldMax.z;
        configuredPlanningMinY = preset.planningWorldMin.y;
        configuredPlanningMaxY = preset.planningWorldMax.y;
        NormalizePlanningBounds();
    }

    private void RefreshExperimentCenterLabels()
    {
        if (experimentGroupValueText != null)
        {
            experimentGroupValueText.text = GetCompactExperimentGroupLabel();
        }

        if (experimentPresetValueText != null)
        {
            experimentPresetValueText.text = GetCompactExperimentPresetLabel();
        }

        if (experimentPresetSummaryText == null)
        {
            return;
        }

        ExperimentPreset selectedPreset = GetSelectedExperimentPreset();
        if (selectedPreset == null)
        {
            experimentPresetSummaryText.text =
                "未找到 ExperimentPreset 资源。\n" +
                "请先执行 Tools/KY UAV/Bootstrap Delivery Assets。";
            return;
        }

        string groupName = GetSelectedExperimentGroupDisplayName();
        string schedulerName = FormatSchedulerName(selectedPreset.scheduler);
        string plannerName = FormatPlannerName(selectedPreset.planner);
        string batchSource = batchExperimentRunner != null && batchExperimentRunner.ActivePreset == selectedPreset
            ? "预设批量"
            : "可应用到当前运行时";

        StringBuilder builder = new StringBuilder(256);
        builder.Append("分组: ").Append(groupName).AppendLine();
        builder.Append("预设: ").Append(ExperimentPresetCatalog.GetPresetDisplayName(selectedPreset)).AppendLine();
        builder.Append("调度 / 规划: ").Append(schedulerName).Append(" / ").Append(plannerName).AppendLine();
        builder.Append("机群 / 轮次: ").Append(selectedPreset.droneCount).Append(" / ").Append(selectedPreset.batchRuns).AppendLine();
        builder.Append("边界 X[").Append(selectedPreset.planningWorldMin.x.ToString("0"))
            .Append(',').Append(selectedPreset.planningWorldMax.x.ToString("0"))
            .Append("] Z[").Append(selectedPreset.planningWorldMin.z.ToString("0"))
            .Append(',').Append(selectedPreset.planningWorldMax.z.ToString("0"))
            .Append("] 检测Y[").Append(selectedPreset.planningWorldMin.y.ToString("0"))
            .Append(',').Append(selectedPreset.planningWorldMax.y.ToString("0"))
            .Append(']').AppendLine();
        builder.Append("备注: ").Append(string.IsNullOrWhiteSpace(selectedPreset.notePrefix) ? "-" : selectedPreset.notePrefix)
            .Append("    模式: ").Append(batchSource);
        experimentPresetSummaryText.text = builder.ToString();
    }

    private string GetCompactExperimentGroupLabel()
    {
        ExperimentPresetGroupInfo selectedGroup = GetSelectedExperimentGroup();
        if (selectedGroup == null)
        {
            return "--";
        }

        switch (selectedGroup.GroupKey)
        {
            case "scheduling":
                return "调度";
            case "planning":
                return "规划";
            case "scaling":
                return "机群";
            case "density":
                return "密度";
            default:
                return selectedGroup.DisplayName;
        }
    }

    private string GetCompactExperimentPresetLabel()
    {
        ExperimentPreset selectedPreset = GetSelectedExperimentPreset();
        if (selectedPreset == null)
        {
            return "--";
        }

        return ExperimentPresetCatalog.GetPresetShortLabel(selectedPreset);
    }

    private int IndexOfSchedulerOption(SchedulerAlgorithmType algorithmType)
    {
        int index = Array.IndexOf(schedulerOptions, algorithmType);
        return index >= 0 ? index : 0;
    }

    private int IndexOfPlannerOption(PathPlannerType plannerType)
    {
        int index = Array.IndexOf(plannerOptions, plannerType);
        return index >= 0 ? index : 0;
    }

    private void OnPreviousExperimentGroupClicked()
    {
        ChangeExperimentGroup(-1);
    }

    private void OnNextExperimentGroupClicked()
    {
        ChangeExperimentGroup(1);
    }

    private void OnPreviousExperimentPresetClicked()
    {
        ChangeExperimentPreset(-1);
    }

    private void OnNextExperimentPresetClicked()
    {
        ChangeExperimentPreset(1);
    }

    private void SelectSchedulingExperimentGroup()
    {
        SelectExperimentGroup("scheduling");
    }

    private void SelectPlanningExperimentGroup()
    {
        SelectExperimentGroup("planning");
    }

    private void SelectScalingExperimentGroup()
    {
        SelectExperimentGroup("scaling");
    }

    private void SelectDensityExperimentGroup()
    {
        SelectExperimentGroup("density");
    }

    private void ChangeExperimentGroup(int delta)
    {
        EnsureExperimentPresetCatalogLoaded();
        if (experimentPresetGroups.Count == 0)
        {
            transientMessage = "未找到实验预设资源";
            RefreshAllLabels();
            return;
        }

        selectedExperimentGroupIndex = WrapIndex(selectedExperimentGroupIndex + delta, experimentPresetGroups.Count);
        selectedExperimentPresetIndex = 0;
        transientMessage = $"实验分组切换为 {GetSelectedExperimentGroupDisplayName()}";
        RefreshAllLabels();
    }

    private void ChangeExperimentPreset(int delta)
    {
        EnsureExperimentPresetCatalogLoaded();
        ExperimentPresetGroupInfo selectedGroup = GetSelectedExperimentGroup();
        if (selectedGroup == null || selectedGroup.Presets.Count == 0)
        {
            transientMessage = "当前分组没有可用预设";
            RefreshAllLabels();
            return;
        }

        selectedExperimentPresetIndex = WrapIndex(selectedExperimentPresetIndex + delta, selectedGroup.Presets.Count);
        transientMessage = $"实验预设切换为 {ExperimentPresetCatalog.GetPresetShortLabel(GetSelectedExperimentPreset())}";
        RefreshAllLabels();
    }

    private void SelectExperimentGroup(string groupKey)
    {
        EnsureExperimentPresetCatalogLoaded();
        string normalizedGroupKey = ExperimentPresetCatalog.NormalizeGroupKey(groupKey);
        for (int i = 0; i < experimentPresetGroups.Count; i++)
        {
            if (!string.Equals(experimentPresetGroups[i].GroupKey, normalizedGroupKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            selectedExperimentGroupIndex = i;
            selectedExperimentPresetIndex = 0;
            transientMessage = $"实验分组切换为 {experimentPresetGroups[i].DisplayName}";
            RefreshAllLabels();
            return;
        }

        transientMessage = $"未找到分组 {groupKey}";
        RefreshAllLabels();
    }

    private void ApplySelectedExperimentPreset()
    {
        ExperimentPreset selectedPreset = GetSelectedExperimentPreset();
        if (selectedPreset == null)
        {
            transientMessage = "未选择实验预设";
            RefreshAllLabels();
            return;
        }

        LoadPresetIntoRuntimeConfiguration(selectedPreset);

        if (batchExperimentRunner != null)
        {
            batchExperimentRunner.UseCurrentRuntimeConfiguration();
            batchExperimentRunner.SetBatchRunCount(configuredBatchRunCount);
        }

        RebuildFleet();
        transientMessage = $"已将预设 {ExperimentPresetCatalog.GetPresetShortLabel(selectedPreset)} 应用到当前机群";
        RefreshAllLabels();
    }

    private void StartSelectedPresetBatch()
    {
        ExperimentPreset selectedPreset = GetSelectedExperimentPreset();
        if (selectedPreset == null)
        {
            transientMessage = "未选择实验预设";
            RefreshAllLabels();
            return;
        }

        if (batchExperimentRunner == null)
        {
            transientMessage = "未找到批量实验执行器";
            RefreshAllLabels();
            return;
        }

        configuredBatchRunCount = Mathf.Clamp(selectedPreset.batchRuns, MinBatchRunCount, MaxBatchRunCount);
        batchExperimentRunner.SetExperimentPreset(selectedPreset);
        batchExperimentRunner.SetBatchRunCount(configuredBatchRunCount);
        if (!string.IsNullOrWhiteSpace(selectedPreset.notePrefix))
        {
            batchExperimentRunner.batchNotePrefix = selectedPreset.notePrefix;
        }

        batchExperimentRunner.StartBatch();
        transientMessage = batchExperimentRunner.LastBatchMessage;
        RefreshBatchStatus();
        RefreshAllLabels();
    }
}
