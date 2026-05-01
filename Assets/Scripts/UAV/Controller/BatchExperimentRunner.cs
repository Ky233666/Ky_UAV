using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 按当前配置或实验预设连续执行多轮实验，并在每轮结束后自动导出结果。
/// </summary>
public class BatchExperimentRunner : MonoBehaviour
{
    [Header("References")]
    public SimulationManager simulationManager;
    public SimulationResultExporter resultExporter;
    public DroneManager droneManager;

    [Header("Batch Settings")]
    [Tooltip("单次触发时的批量实验轮数。")]
    public int batchRunCount = 5;

    [Tooltip("每轮实验重置后的额外等待时间（秒，真实时间）。")]
    public float delayBetweenRuns = 0.35f;

    [Tooltip("批量实验是否导出 CSV。")]
    public bool exportCsv = true;

    [Tooltip("批量实验是否导出 JSON。")]
    public bool exportJson = true;

    [Tooltip("批量实验导出备注前缀。")]
    public string batchNotePrefix = "batch";

    [Header("Experiment Preset")]
    [Tooltip("当前批量实验预设；为空时按当前运行时配置执行。")]
    public ExperimentPreset experimentPreset;

    [Tooltip("默认实验预设的 Resources 路径；为空时不自动加载。")]
    public string experimentPresetResourcePath = "ExperimentPresets/Density/Medium";

    public bool IsBatchRunning => batchCoroutine != null;
    public int CurrentRunIndex { get; private set; }
    public int CompletedRunCount { get; private set; }
    public string LastBatchMessage { get; private set; } = "未开始批量实验";
    public string ActivePresetName => resolvedExperimentPreset != null ? resolvedExperimentPreset.presetName : "Current Runtime";
    public ExperimentPreset ActivePreset => resolvedExperimentPreset != null ? resolvedExperimentPreset : experimentPreset;

    private Coroutine batchCoroutine;
    private bool stopRequested;
    private bool cachedAutoExportOnCompletion;
    private ExperimentPreset resolvedExperimentPreset;
    private readonly List<SimulationExperimentRecord> sessionRecords = new List<SimulationExperimentRecord>();
    private readonly List<string> exportedFiles = new List<string>();

    void Awake()
    {
        CacheReferences();
    }

    public void SetBatchRunCount(int count)
    {
        batchRunCount = Mathf.Clamp(count, 1, 50);
    }

    public void SetExperimentPreset(ExperimentPreset preset)
    {
        experimentPreset = preset;
        resolvedExperimentPreset = preset;
        if (preset != null)
        {
            experimentPresetResourcePath = string.Empty;
        }
    }

    public void UseCurrentRuntimeConfiguration()
    {
        experimentPreset = null;
        resolvedExperimentPreset = null;
        experimentPresetResourcePath = string.Empty;
    }

    public bool StartBatch()
    {
        CacheReferences();

        if (IsBatchRunning)
        {
            LastBatchMessage = $"批量实验进行中：{CompletedRunCount}/{batchRunCount}";
            return false;
        }

        if (simulationManager == null || resultExporter == null || droneManager == null)
        {
            LastBatchMessage = "批量实验启动失败：缺少仿真管理器、导出器或无人机管理器";
            return false;
        }

        if (!exportCsv && !exportJson)
        {
            LastBatchMessage = "批量实验启动失败：CSV 和 JSON 至少启用一种导出";
            return false;
        }

        resolvedExperimentPreset = ResolvePreset();
        ApplyResolvedPreset();

        stopRequested = false;
        CurrentRunIndex = 0;
        CompletedRunCount = 0;
        sessionRecords.Clear();
        exportedFiles.Clear();
        cachedAutoExportOnCompletion = resultExporter.autoExportOnCompletion;
        resultExporter.autoExportOnCompletion = false;
        resultExporter.BeginNewArchiveSession($"{batchNotePrefix}-{batchRunCount:D2}-runs");
        batchCoroutine = StartCoroutine(RunBatchCoroutine());
        LastBatchMessage = $"批量实验已启动，共 {batchRunCount} 轮，预设 {ActivePresetName}，归档到 {resultExporter.CurrentSessionFolderName}";
        return true;
    }

    public void StopBatch()
    {
        if (!IsBatchRunning)
        {
            LastBatchMessage = "当前没有正在执行的批量实验";
            return;
        }

        stopRequested = true;
        LastBatchMessage = $"正在停止批量实验，已完成 {CompletedRunCount}/{batchRunCount}";
    }

    private IEnumerator RunBatchCoroutine()
    {
        bool aborted = false;

        try
        {
            for (int runIndex = 1; runIndex <= batchRunCount; runIndex++)
            {
                if (stopRequested)
                {
                    aborted = true;
                    break;
                }

                CurrentRunIndex = runIndex;
                LastBatchMessage = $"第 {runIndex}/{batchRunCount} 轮准备中";

                simulationManager.OnResetClicked();
                yield return null;

                ApplyResolvedPreset();

                simulationManager.OnStartClicked();
                yield return null;

                if (simulationManager.currentState != SimulationState.Running)
                {
                    LastBatchMessage = $"第 {runIndex}/{batchRunCount} 轮启动失败";
                    aborted = true;
                    break;
                }

                LastBatchMessage = $"第 {runIndex}/{batchRunCount} 轮运行中";

                while (!stopRequested)
                {
                    if (IsCurrentRunCompleted())
                    {
                        break;
                    }

                    if (simulationManager == null || simulationManager.currentState == SimulationState.Idle)
                    {
                        LastBatchMessage = $"第 {runIndex}/{batchRunCount} 轮被中断";
                        aborted = true;
                        break;
                    }

                    yield return null;
                }

                if (stopRequested || aborted)
                {
                    break;
                }

                string note = $"{batchNotePrefix}-run-{runIndex:D2}-of-{batchRunCount:D2}";
                SimulationExperimentRecord record = resultExporter.CaptureCurrentRecord(note, "batch");
                bool exportedAny = false;

                if (exportCsv)
                {
                    exportedAny |= resultExporter.ExportCurrentResult(note, false);
                    RegisterExportPath(resultExporter.LastExportPath);
                }

                if (exportJson)
                {
                    exportedAny |= resultExporter.ExportCurrentResultAsJson(note, false);
                    RegisterExportPath(resultExporter.LastExportPath);
                }

                if (record != null)
                {
                    sessionRecords.Add(record);
                }

                CompletedRunCount = runIndex;
                LastBatchMessage = exportedAny
                    ? $"第 {runIndex}/{batchRunCount} 轮已导出"
                    : $"第 {runIndex}/{batchRunCount} 轮导出失败";

                simulationManager.OnResetClicked();

                if (runIndex < batchRunCount)
                {
                    yield return new WaitForSecondsRealtime(Mathf.Max(0f, delayBetweenRuns));
                }
            }
        }
        finally
        {
            WriteSessionArtifacts(aborted || stopRequested);

            if (resultExporter != null)
            {
                resultExporter.autoExportOnCompletion = cachedAutoExportOnCompletion;
            }

            if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
            {
                simulationManager.OnResetClicked();
            }

            if (stopRequested)
            {
                LastBatchMessage = $"批量实验已停止，完成 {CompletedRunCount}/{batchRunCount} 轮";
            }
            else if (!aborted && CompletedRunCount >= batchRunCount)
            {
                LastBatchMessage = $"批量实验完成，共 {CompletedRunCount} 轮";
            }

            batchCoroutine = null;
            stopRequested = false;
        }
    }

    private bool IsCurrentRunCompleted()
    {
        if (simulationManager == null || simulationManager.currentState != SimulationState.Running)
        {
            return false;
        }

        TaskPoint[] taskPoints = SimulationContext.GetOrCreate(this).GetTaskPoints();
        if (taskPoints.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < taskPoints.Length; i++)
        {
            if (taskPoints[i] != null && taskPoints[i].currentState != TaskState.Completed)
            {
                return false;
            }
        }

        return true;
    }

    private void CacheReferences()
    {
        simulationManager = RuntimeSceneRegistry.Resolve(simulationManager, this);
        resultExporter = RuntimeSceneRegistry.Resolve(resultExporter, this);
        droneManager = RuntimeSceneRegistry.Resolve(
            droneManager,
            simulationManager != null ? simulationManager.droneManager : null,
            this);
    }

    private ExperimentPreset ResolvePreset()
    {
        if (experimentPreset != null)
        {
            return experimentPreset;
        }

        if (string.IsNullOrWhiteSpace(experimentPresetResourcePath))
        {
            return null;
        }

        experimentPreset = Resources.Load<ExperimentPreset>(experimentPresetResourcePath);
        return experimentPreset;
    }

    private void ApplyResolvedPreset()
    {
        if (resolvedExperimentPreset == null || droneManager == null)
        {
            return;
        }

        batchRunCount = Mathf.Clamp(resolvedExperimentPreset.batchRuns, 1, 50);
        batchNotePrefix = string.IsNullOrWhiteSpace(resolvedExperimentPreset.notePrefix)
            ? batchNotePrefix
            : resolvedExperimentPreset.notePrefix;

        droneManager.schedulerAlgorithm = resolvedExperimentPreset.scheduler;
        droneManager.pathPlannerType = resolvedExperimentPreset.planner;
        droneManager.ApplyPlanningSettings(
            droneManager.planningGridCellSize,
            droneManager.allowDiagonalPlanning,
            droneManager.autoConfigurePlanningObstacles,
            resolvedExperimentPreset.planningWorldMin,
            resolvedExperimentPreset.planningWorldMax);
        droneManager.RespawnDrones(Mathf.Max(1, resolvedExperimentPreset.droneCount));
    }

    private void RegisterExportPath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return;
        }

        string fileName = Path.GetFileName(fullPath);
        if (!string.IsNullOrWhiteSpace(fileName) && !exportedFiles.Contains(fileName))
        {
            exportedFiles.Add(fileName);
        }
    }

    private void WriteSessionArtifacts(bool stoppedEarly)
    {
        if (resultExporter == null || sessionRecords.Count == 0)
        {
            return;
        }

        string summaryPath = resultExporter.WriteSessionSummaryCsv(sessionRecords);
        RegisterExportPath(summaryPath);

        BatchSessionManifest manifest = new BatchSessionManifest
        {
            sessionFolderName = resultExporter.CurrentSessionFolderName ?? string.Empty,
            generatedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            exportDirectory = resultExporter.GetArchiveDirectoryPath(),
            batchNotePrefix = batchNotePrefix,
            batchRuns = batchRunCount,
            completedRuns = CompletedRunCount,
            stoppedEarly = stoppedEarly,
            lastBatchMessage = LastBatchMessage,
            preset = BuildPresetSnapshot(resolvedExperimentPreset),
            planning = BuildPlanningSnapshot(),
            exportedFiles = new StringListWrapper { items = exportedFiles.ToArray() }
        };

        string manifestPath = resultExporter.WriteSessionManifest(manifest);
        RegisterExportPath(manifestPath);
    }

    private ExperimentPresetSnapshot BuildPresetSnapshot(ExperimentPreset preset)
    {
        if (preset == null)
        {
            return new ExperimentPresetSnapshot
            {
                presetName = "Current Runtime",
                groupName = "runtime",
                notePrefix = batchNotePrefix,
                batchRuns = batchRunCount,
                droneCount = droneManager != null ? droneManager.droneCount : 0,
                scheduler = droneManager != null ? UAVAlgorithmNames.GetSchedulerIdentifier(droneManager.schedulerAlgorithm) : "-",
                planner = droneManager != null ? UAVAlgorithmNames.GetPlannerIdentifier(droneManager.pathPlannerType) : "-",
                planningWorldMin = droneManager != null ? droneManager.planningWorldMin : Vector3.zero,
                planningWorldMax = droneManager != null ? droneManager.planningWorldMax : Vector3.zero
            };
        }

        return new ExperimentPresetSnapshot
        {
            presetName = preset.presetName ?? string.Empty,
            groupName = preset.groupName ?? string.Empty,
            notePrefix = preset.notePrefix ?? string.Empty,
            batchRuns = preset.batchRuns,
            droneCount = preset.droneCount,
            scheduler = UAVAlgorithmNames.GetSchedulerIdentifier(preset.scheduler),
            planner = UAVAlgorithmNames.GetPlannerIdentifier(preset.planner),
            planningWorldMin = preset.planningWorldMin,
            planningWorldMax = preset.planningWorldMax
        };
    }

    private PlanningSettingsSnapshot BuildPlanningSnapshot()
    {
        if (droneManager == null)
        {
            return new PlanningSettingsSnapshot();
        }

        return new PlanningSettingsSnapshot
        {
            gridCellSize = droneManager.planningGridCellSize,
            allowDiagonal = droneManager.allowDiagonalPlanning,
            autoConfigureObstacles = droneManager.autoConfigurePlanningObstacles,
            worldMin = droneManager.planningWorldMin,
            worldMax = droneManager.planningWorldMax
        };
    }
}
