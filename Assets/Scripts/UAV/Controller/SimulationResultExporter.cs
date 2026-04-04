using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 导出当前仿真结果，供论文实验记录和答辩展示使用。
/// </summary>
public class SimulationResultExporter : MonoBehaviour
{
    [Header("References")]
    public SimulationManager simulationManager;
    public DroneManager droneManager;
    public CameraManager cameraManager;

    [Header("Export Settings")]
    [Tooltip("当一轮任务全部完成时，自动追加一条 CSV 记录。")]
    public bool autoExportOnCompletion = true;

    [Tooltip("导出目录名，最终位置位于 Application.persistentDataPath 下。")]
    public string exportFolderName = "ExperimentResults";

    [Tooltip("CSV 文件名。")]
    public string csvFileName = "simulation_experiment_records.csv";

    [Tooltip("自动导出时写入的备注。")]
    public string autoExportNote = "simulation-complete";

    [Tooltip("手动导出时写入的默认备注。")]
    public string manualExportNote = "manual-export";

    public string LastExportPath { get; private set; } = string.Empty;
    public string LastExportMessage { get; private set; } = "尚未导出";

    private bool hasStartedRun;
    private bool hasAutoExportedCurrentRun;

    void Awake()
    {
        CacheReferences();
    }

    void Update()
    {
        if (!autoExportOnCompletion || !hasStartedRun || hasAutoExportedCurrentRun)
        {
            return;
        }

        if (!IsRunCompleted())
        {
            return;
        }

        if (ExportCurrentResult(autoExportNote, true))
        {
            hasAutoExportedCurrentRun = true;
        }
    }

    public void BeginRun()
    {
        hasStartedRun = true;
        hasAutoExportedCurrentRun = false;
    }

    public void ResetRunTracking()
    {
        hasStartedRun = false;
        hasAutoExportedCurrentRun = false;
    }

    public bool ExportCurrentResult(string note = "", bool autoTriggered = false)
    {
        CacheReferences();

        SimulationExperimentRecord record = BuildCurrentRecord(
            string.IsNullOrWhiteSpace(note)
                ? (autoTriggered ? autoExportNote : manualExportNote)
                : note,
            autoTriggered ? "auto" : "manual");

        if (record == null)
        {
            LastExportMessage = "导出失败：缺少仿真管理器";
            return false;
        }

        try
        {
            string directory = GetExportDirectoryPath();
            Directory.CreateDirectory(directory);

            string csvPath = Path.Combine(directory, csvFileName);
            bool fileExists = File.Exists(csvPath);

            using (StreamWriter writer = new StreamWriter(csvPath, true, new UTF8Encoding(true)))
            {
                if (!fileExists || new FileInfo(csvPath).Length == 0)
                {
                    writer.WriteLine(SimulationExperimentRecord.CsvHeader);
                }

                writer.WriteLine(record.ToCsvRow());
            }

            LastExportPath = csvPath;
            LastExportMessage = $"已导出 CSV：{Path.GetFileName(csvPath)}";
            Debug.Log($"[SimulationResultExporter] {LastExportMessage} | {csvPath}");
            return true;
        }
        catch (Exception exception)
        {
            LastExportMessage = $"导出失败：{exception.Message}";
            Debug.LogError($"[SimulationResultExporter] {LastExportMessage}");
            return false;
        }
    }

    public string GetExportDirectoryPath()
    {
        return Path.Combine(Application.persistentDataPath, exportFolderName);
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

        if (droneManager == null)
        {
            droneManager = simulationManager != null ? simulationManager.droneManager : null;
            if (droneManager == null)
            {
                droneManager = FindObjectOfType<DroneManager>();
            }
        }

        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<CameraManager>();
        }
    }

    private bool IsRunCompleted()
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
            TaskPoint taskPoint = taskPoints[i];
            if (taskPoint != null && taskPoint.currentState != TaskState.Completed)
            {
                return false;
            }
        }

        return true;
    }

    private SimulationExperimentRecord BuildCurrentRecord(string note, string exportTrigger)
    {
        if (simulationManager == null)
        {
            return null;
        }

        TaskPoint[] taskPoints = FindObjectsOfType<TaskPoint>();

        SimulationExperimentRecord record = new SimulationExperimentRecord
        {
            experimentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            exportTrigger = exportTrigger,
            simulationState = simulationManager.currentState.ToString(),
            taskCount = taskPoints.Length,
            completedTaskCount = CountTasksByState(taskPoints, TaskState.Completed),
            pendingTaskCount = CountTasksByState(taskPoints, TaskState.Pending),
            inProgressTaskCount = CountTasksByState(taskPoints, TaskState.InProgress),
            schedulerAlgorithm = ResolveSchedulerAlgorithmName(),
            pathPlanner = ResolvePathPlannerName(),
            elapsedSeconds = simulationManager.ElapsedSimulationTime,
            cameraTarget = cameraManager != null && cameraManager.targetDrone != null
                ? cameraManager.targetDrone.name
                : "-",
            notes = note
        };

        if (droneManager != null)
        {
            record.droneCount = droneManager.drones.Count;
            record.onlineDroneCount = droneManager.GetOnlineDroneCount();
            record.totalFlightDistance = CalculateTotalFlightDistance();
            record.totalWaitCount = CalculateTotalWaitCount();
            record.droneBreakdown = BuildDroneBreakdown();
        }

        return record;
    }

    private int CountTasksByState(TaskPoint[] taskPoints, TaskState state)
    {
        int count = 0;
        for (int i = 0; i < taskPoints.Length; i++)
        {
            if (taskPoints[i] != null && taskPoints[i].currentState == state)
            {
                count++;
            }
        }

        return count;
    }

    private float CalculateTotalFlightDistance()
    {
        float total = 0f;
        if (droneManager == null || droneManager.droneDataList == null)
        {
            return total;
        }

        foreach (DroneData data in droneManager.droneDataList)
        {
            if (data != null)
            {
                total += data.totalFlightDistance;
            }
        }

        return total;
    }

    private int CalculateTotalWaitCount()
    {
        int total = 0;
        if (droneManager == null || droneManager.droneDataList == null)
        {
            return total;
        }

        foreach (DroneData data in droneManager.droneDataList)
        {
            if (data != null)
            {
                total += data.waitCount;
            }
        }

        return total;
    }

    private string BuildDroneBreakdown()
    {
        if (droneManager == null || droneManager.droneDataList == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(256);
        for (int i = 0; i < droneManager.droneDataList.Count; i++)
        {
            DroneData data = droneManager.droneDataList[i];
            if (data == null)
            {
                continue;
            }

            int assignedTaskCount = data.taskQueue != null ? data.taskQueue.Length : 0;
            builder.Append('[').Append(data.droneId.ToString("D2")).Append(']')
                .Append(data.state)
                .Append(" tasks ")
                .Append(data.completedTasks).Append('/').Append(assignedTaskCount)
                .Append(" distance ")
                .Append(data.totalFlightDistance.ToString("0.0")).Append("m")
                .Append(" waits ").Append(data.waitCount);

            if (i < droneManager.droneDataList.Count - 1)
            {
                builder.Append("; ");
            }
        }

        return builder.ToString();
    }

    private string ResolveSchedulerAlgorithmName()
    {
        if (droneManager == null)
        {
            return "-";
        }

        switch (droneManager.schedulerAlgorithm)
        {
            case SchedulerAlgorithmType.GreedyNearest:
                return "GreedyNearest";
            case SchedulerAlgorithmType.EvenSplit:
            default:
                return "EvenSplit";
        }
    }

    private string ResolvePathPlannerName()
    {
        if (droneManager == null)
        {
            return "-";
        }

        switch (droneManager.pathPlannerType)
        {
            case PathPlannerType.AStar:
                return "AStar";
            case PathPlannerType.StraightLine:
            default:
                return "StraightLine";
        }
    }
}
