using System;
using System.Collections.Generic;
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

    [Tooltip("是否优先使用自定义导出目录。")]
    public bool useCustomExportDirectory = false;

    [Tooltip("自定义导出目录；为空时回退到默认目录。")]
    public string customExportDirectory = "";

    [Tooltip("是否按日期子目录归档导出结果。")]
    public bool organizeByDate = true;

    [Tooltip("是否按实验会话子目录归档导出结果。")]
    public bool organizeBySession = true;

    [Tooltip("实验会话目录名的默认标签。")]
    public string exportSessionLabel = "runtime";

    [Tooltip("CSV 文件名。")]
    public string csvFileName = "simulation_experiment_records.csv";

    [Tooltip("JSON 明细导出文件名前缀。")]
    public string jsonFileNamePrefix = "simulation_experiment_detail";

    [Tooltip("自动导出时写入的备注。")]
    public string autoExportNote = "simulation-complete";

    [Tooltip("手动导出时写入的默认备注。")]
    public string manualExportNote = "manual-export";

    public string LastExportPath { get; private set; } = string.Empty;
    public string LastExportMessage { get; private set; } = "尚未导出";
    public bool IsUsingCustomExportDirectory =>
        useCustomExportDirectory && !string.IsNullOrWhiteSpace(customExportDirectory);
    public string CurrentSessionFolderName { get; private set; } = string.Empty;

    private bool hasStartedRun;
    private bool hasAutoExportedCurrentRun;

    void Awake()
    {
        CacheReferences();
        EnsureSessionFolderName();
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
            string directory = GetArchiveDirectoryPath();
            Directory.CreateDirectory(directory);

            string csvPath = ResolveCsvExportPath(directory);
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

    public bool ExportCurrentResultAsJson(string note = "", bool autoTriggered = false)
    {
        CacheReferences();

        SimulationExperimentDetailExport detail = BuildCurrentDetailExport(
            string.IsNullOrWhiteSpace(note)
                ? (autoTriggered ? autoExportNote : manualExportNote)
                : note,
            autoTriggered ? "auto" : "manual");

        if (detail == null)
        {
            LastExportMessage = "导出失败：缺少仿真管理器";
            return false;
        }

        try
        {
            string directory = GetArchiveDirectoryPath();
            Directory.CreateDirectory(directory);

            string fileName =
                $"{jsonFileNamePrefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{detail.exportTrigger}.json";
            string jsonPath = Path.Combine(directory, fileName);
            string json = JsonUtility.ToJson(detail, true);
            File.WriteAllText(jsonPath, json, new UTF8Encoding(true));

            LastExportPath = jsonPath;
            LastExportMessage = $"已导出 JSON：{Path.GetFileName(jsonPath)}";
            Debug.Log($"[SimulationResultExporter] {LastExportMessage} | {jsonPath}");
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
        if (IsUsingCustomExportDirectory)
        {
            try
            {
                return Path.GetFullPath(customExportDirectory);
            }
            catch
            {
                // Ignore invalid custom path here; apply flow will surface the actual error.
            }
        }

        return GetDefaultExportDirectoryPath();
    }

    public string GetArchiveDirectoryPath()
    {
        string directory = GetExportDirectoryPath();

        if (organizeByDate)
        {
            directory = Path.Combine(directory, DateTime.Now.ToString("yyyyMMdd"));
        }

        if (organizeBySession)
        {
            directory = Path.Combine(directory, EnsureSessionFolderName());
        }

        return directory;
    }

    public string GetDefaultExportDirectoryPath()
    {
        return Path.Combine(Application.persistentDataPath, exportFolderName);
    }

    public string BeginNewArchiveSession(string labelOverride = "")
    {
        CurrentSessionFolderName = BuildSessionFolderName(labelOverride);
        LastExportMessage = $"已创建导出会话：{CurrentSessionFolderName}";
        return CurrentSessionFolderName;
    }

    public bool SetCustomExportDirectory(string directoryPath, out string message)
    {
        string rawPath = directoryPath != null ? directoryPath.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            message = "导出目录不能为空";
            LastExportMessage = message;
            return false;
        }

        try
        {
            string resolvedPath = Path.GetFullPath(rawPath);
            Directory.CreateDirectory(resolvedPath);
            customExportDirectory = resolvedPath;
            useCustomExportDirectory = true;
            message = $"已切换导出目录：{resolvedPath}";
            LastExportMessage = message;
            return true;
        }
        catch (Exception exception)
        {
            message = $"目录无效：{exception.Message}";
            LastExportMessage = message;
            return false;
        }
    }

    public void ResetExportDirectoryToDefault()
    {
        useCustomExportDirectory = false;
        customExportDirectory = string.Empty;
        LastExportMessage = $"已切回默认目录：{GetDefaultExportDirectoryPath()}";
    }

    private string ResolveCsvExportPath(string directory)
    {
        string basePath = Path.Combine(directory, csvFileName);
        if (!File.Exists(basePath))
        {
            return basePath;
        }

        string firstLine;
        using (StreamReader reader = new StreamReader(basePath, Encoding.UTF8, true))
        {
            firstLine = reader.ReadLine() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(firstLine) ||
            string.Equals(firstLine, SimulationExperimentRecord.CsvHeader, StringComparison.Ordinal))
        {
            return basePath;
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(csvFileName);
        string extension = Path.GetExtension(csvFileName);
        return Path.Combine(directory, $"{fileNameWithoutExtension}_v2{extension}");
    }

    private string EnsureSessionFolderName()
    {
        if (string.IsNullOrWhiteSpace(CurrentSessionFolderName))
        {
            CurrentSessionFolderName = BuildSessionFolderName(string.Empty);
        }

        return CurrentSessionFolderName;
    }

    private string BuildSessionFolderName(string labelOverride)
    {
        string rawLabel = string.IsNullOrWhiteSpace(labelOverride) ? exportSessionLabel : labelOverride;
        string safeLabel = SanitizePathSegment(rawLabel);
        if (string.IsNullOrWhiteSpace(safeLabel))
        {
            safeLabel = "runtime";
        }

        return $"{DateTime.Now:yyyyMMdd_HHmmss}_{safeLabel}";
    }

    private string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        char[] invalidChars = Path.GetInvalidFileNameChars();
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (Array.IndexOf(invalidChars, character) >= 0)
            {
                builder.Append('_');
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                builder.Append('-');
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim('-', '_', '.');
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
            record.totalConflictCount = CalculateTotalConflictCount();
            record.droneBreakdown = BuildDroneBreakdown();
        }

        return record;
    }

    private SimulationExperimentDetailExport BuildCurrentDetailExport(string note, string exportTrigger)
    {
        if (simulationManager == null)
        {
            return null;
        }

        TaskPoint[] taskPoints = FindObjectsOfType<TaskPoint>();
        SimulationExperimentDetailExport detail = new SimulationExperimentDetailExport
        {
            experimentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            exportTrigger = exportTrigger,
            simulationState = simulationManager.currentState.ToString(),
            schedulerAlgorithm = ResolveSchedulerAlgorithmName(),
            pathPlanner = ResolvePathPlannerName(),
            cameraMode = cameraManager != null ? (cameraManager.isOverview ? "Overview" : "Follow") : "Unknown",
            cameraTarget = cameraManager != null && cameraManager.targetDrone != null
                ? cameraManager.targetDrone.name
                : "-",
            notes = note,
            elapsedSeconds = simulationManager.ElapsedSimulationTime,
            taskCount = taskPoints.Length,
            completedTaskCount = CountTasksByState(taskPoints, TaskState.Completed),
            pendingTaskCount = CountTasksByState(taskPoints, TaskState.Pending),
            inProgressTaskCount = CountTasksByState(taskPoints, TaskState.InProgress)
        };

        if (droneManager != null)
        {
            detail.droneCount = droneManager.drones.Count;
            detail.onlineDroneCount = droneManager.GetOnlineDroneCount();
            detail.totalFlightDistance = CalculateTotalFlightDistance();
            detail.totalWaitCount = CalculateTotalWaitCount();
            detail.totalConflictCount = CalculateTotalConflictCount();
            detail.planning = new PlanningSettingsSnapshot
            {
                gridCellSize = droneManager.planningGridCellSize,
                allowDiagonal = droneManager.allowDiagonalPlanning,
                autoConfigureObstacles = droneManager.autoConfigurePlanningObstacles,
                worldMin = droneManager.planningWorldMin,
                worldMax = droneManager.planningWorldMax
            };
            detail.drones = BuildDroneDetails();
        }

        detail.tasks = BuildTaskDetails(taskPoints);
        return detail;
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

    private int CalculateTotalConflictCount()
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
                total += data.conflictCount;
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
                .Append(" waits ").Append(data.waitCount)
                .Append(" conflicts ").Append(data.conflictCount);

            if (i < droneManager.droneDataList.Count - 1)
            {
                builder.Append("; ");
            }
        }

        return builder.ToString();
    }

    private List<DroneExperimentDetail> BuildDroneDetails()
    {
        List<DroneExperimentDetail> details = new List<DroneExperimentDetail>();
        if (droneManager == null || droneManager.droneDataList == null)
        {
            return details;
        }

        foreach (DroneData data in droneManager.droneDataList)
        {
            if (data == null)
            {
                continue;
            }

            DroneController controller = droneManager.GetDrone(data.droneId);
            details.Add(new DroneExperimentDetail
            {
                droneId = data.droneId,
                droneName = data.droneName ?? string.Empty,
                state = data.state.ToString(),
                isOnline = data.isOnline,
                speed = data.speed,
                assignedTaskCount = data.taskQueue != null ? data.taskQueue.Length : 0,
                currentTaskIndex = data.currentTaskIndex,
                completedTasks = data.completedTasks,
                totalFlightDistance = data.totalFlightDistance,
                waitCount = data.waitCount,
                conflictCount = data.conflictCount,
                waitReason = data.waitReason ?? string.Empty,
                lastConflictReason = data.lastConflictReason ?? string.Empty,
                plannerName = data.currentPlannerName ?? string.Empty,
                currentPosition = controller != null ? controller.transform.position : data.lastKnownPosition,
                lastKnownPosition = data.lastKnownPosition,
                currentWaypointIndex = data.currentWaypointIndex,
                plannedPath = data.plannedPath != null ? new List<Vector3>(data.plannedPath) : new List<Vector3>()
            });
        }

        return details;
    }

    private List<TaskPointExperimentDetail> BuildTaskDetails(TaskPoint[] taskPoints)
    {
        List<TaskPointExperimentDetail> details = new List<TaskPointExperimentDetail>();
        if (taskPoints == null)
        {
            return details;
        }

        for (int i = 0; i < taskPoints.Length; i++)
        {
            TaskPoint taskPoint = taskPoints[i];
            if (taskPoint == null)
            {
                continue;
            }

            details.Add(new TaskPointExperimentDetail
            {
                taskId = taskPoint.taskId,
                taskName = taskPoint.taskName ?? string.Empty,
                description = taskPoint.description ?? string.Empty,
                priority = taskPoint.priority,
                estimatedDuration = taskPoint.estimatedDuration,
                state = taskPoint.currentState.ToString(),
                assignedDroneId = taskPoint.assignedDrone != null ? taskPoint.assignedDrone.droneId : 0,
                assignedDroneName = taskPoint.assignedDrone != null ? taskPoint.assignedDrone.droneName : string.Empty,
                startTime = taskPoint.startTime,
                completionTime = taskPoint.completionTime,
                position = taskPoint.transform.position
            });
        }

        return details;
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
