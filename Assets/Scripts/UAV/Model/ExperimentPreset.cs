using System;
using UnityEngine;

/// <summary>
/// 可复现实验预设。
/// </summary>
[CreateAssetMenu(fileName = "ExperimentPreset", menuName = "KY UAV/Experiment Preset")]
public class ExperimentPreset : ScriptableObject
{
    [Tooltip("预设显示名称。")]
    public string presetName = "New Preset";

    [Tooltip("实验分组名称，例如 scheduling / planning / scaling / density。")]
    public string groupName = "general";

    [Tooltip("备注前缀，会写入批量实验导出备注中。")]
    public string notePrefix = "preset";

    [Tooltip("批量实验轮数。")]
    public int batchRuns = 5;

    [Tooltip("无人机数量。")]
    public int droneCount = 4;

    [Tooltip("调度算法。")]
    public SchedulerAlgorithmType scheduler = SchedulerAlgorithmType.PriorityGreedy;

    [Tooltip("路径规划算法。")]
    public PathPlannerType planner = PathPlannerType.AStar;

    [Tooltip("规划边界最小值。")]
    public Vector3 planningWorldMin = new Vector3(-20f, 0f, -20f);

    [Tooltip("规划边界最大值。")]
    public Vector3 planningWorldMax = new Vector3(80f, 10f, 80f);
}

[Serializable]
public class BatchSessionManifest
{
    public string sessionFolderName = "";
    public string generatedAt = "";
    public string exportDirectory = "";
    public string batchNotePrefix = "";
    public int batchRuns;
    public int completedRuns;
    public bool stoppedEarly;
    public string lastBatchMessage = "";
    public ExperimentPresetSnapshot preset = new ExperimentPresetSnapshot();
    public PlanningSettingsSnapshot planning = new PlanningSettingsSnapshot();
    public StringListWrapper exportedFiles = new StringListWrapper();
}

[Serializable]
public class ExperimentPresetSnapshot
{
    public string presetName = "";
    public string groupName = "";
    public string notePrefix = "";
    public int batchRuns;
    public int droneCount;
    public string scheduler = "";
    public string planner = "";
    public Vector3 planningWorldMin;
    public Vector3 planningWorldMax;
}

[Serializable]
public class StringListWrapper
{
    public string[] items = Array.Empty<string>();
}
