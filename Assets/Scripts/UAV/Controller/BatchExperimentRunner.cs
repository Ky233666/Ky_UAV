using System.Collections;
using UnityEngine;

/// <summary>
/// 按当前配置连续执行多轮实验，并在每轮结束后自动导出结果。
/// </summary>
public class BatchExperimentRunner : MonoBehaviour
{
    [Header("References")]
    public SimulationManager simulationManager;
    public SimulationResultExporter resultExporter;

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

    public bool IsBatchRunning => batchCoroutine != null;
    public int CurrentRunIndex { get; private set; }
    public int CompletedRunCount { get; private set; }
    public string LastBatchMessage { get; private set; } = "未开始批量实验";

    private Coroutine batchCoroutine;
    private bool stopRequested;
    private bool cachedAutoExportOnCompletion;

    void Awake()
    {
        CacheReferences();
    }

    public void SetBatchRunCount(int count)
    {
        batchRunCount = Mathf.Clamp(count, 1, 50);
    }

    public bool StartBatch()
    {
        CacheReferences();

        if (IsBatchRunning)
        {
            LastBatchMessage = $"批量实验进行中：{CompletedRunCount}/{batchRunCount}";
            return false;
        }

        if (simulationManager == null || resultExporter == null)
        {
            LastBatchMessage = "批量实验启动失败：缺少仿真管理器或导出器";
            return false;
        }

        if (!exportCsv && !exportJson)
        {
            LastBatchMessage = "批量实验启动失败：CSV 和 JSON 至少启用一种导出";
            return false;
        }

        stopRequested = false;
        CurrentRunIndex = 0;
        CompletedRunCount = 0;
        cachedAutoExportOnCompletion = resultExporter.autoExportOnCompletion;
        resultExporter.autoExportOnCompletion = false;
        resultExporter.BeginNewArchiveSession($"{batchNotePrefix}-{batchRunCount:D2}-runs");
        batchCoroutine = StartCoroutine(RunBatchCoroutine());
        LastBatchMessage = $"批量实验已启动，共 {batchRunCount} 轮，归档到 {resultExporter.CurrentSessionFolderName}";
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
                bool exportedAny = false;

                if (exportCsv)
                {
                    exportedAny |= resultExporter.ExportCurrentResult(note, false);
                }

                if (exportJson)
                {
                    exportedAny |= resultExporter.ExportCurrentResultAsJson(note, false);
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

        TaskPoint[] taskPoints = FindObjectsOfType<TaskPoint>();
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
        if (simulationManager == null)
        {
            simulationManager = GetComponent<SimulationManager>();
            if (simulationManager == null)
            {
                simulationManager = FindObjectOfType<SimulationManager>();
            }
        }

        if (resultExporter == null)
        {
            resultExporter = GetComponent<SimulationResultExporter>();
            if (resultExporter == null)
            {
                resultExporter = FindObjectOfType<SimulationResultExporter>();
            }
        }
    }
}
