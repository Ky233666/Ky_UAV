using System.Text;

/// <summary>
/// 单次仿真实验记录，用于导出 CSV。
/// </summary>
[System.Serializable]
public class SimulationExperimentRecord
{
    public string experimentTime = "";
    public string exportTrigger = "";
    public string simulationState = "";
    public int droneCount;
    public int onlineDroneCount;
    public int taskCount;
    public int completedTaskCount;
    public int pendingTaskCount;
    public int inProgressTaskCount;
    public string schedulerAlgorithm = "";
    public string pathPlanner = "";
    public float elapsedSeconds;
    public float totalFlightDistance;
    public int totalWaitCount;
    public int totalConflictCount;
    public string cameraTarget = "";
    public string notes = "";
    public string droneBreakdown = "";

    public static string CsvHeader =>
        "experiment_time,export_trigger,simulation_state,drone_count,online_drone_count," +
        "task_count,completed_task_count,pending_task_count,in_progress_task_count," +
        "scheduler_algorithm,path_planner,elapsed_seconds,total_flight_distance,total_wait_count,total_conflict_count," +
        "camera_target,notes,drone_breakdown";

    public string ToCsvRow()
    {
        StringBuilder builder = new StringBuilder(512);
        AppendCsvField(builder, experimentTime);
        AppendCsvField(builder, exportTrigger);
        AppendCsvField(builder, simulationState);
        AppendCsvField(builder, droneCount.ToString());
        AppendCsvField(builder, onlineDroneCount.ToString());
        AppendCsvField(builder, taskCount.ToString());
        AppendCsvField(builder, completedTaskCount.ToString());
        AppendCsvField(builder, pendingTaskCount.ToString());
        AppendCsvField(builder, inProgressTaskCount.ToString());
        AppendCsvField(builder, schedulerAlgorithm);
        AppendCsvField(builder, pathPlanner);
        AppendCsvField(builder, elapsedSeconds.ToString("0.###"));
        AppendCsvField(builder, totalFlightDistance.ToString("0.###"));
        AppendCsvField(builder, totalWaitCount.ToString());
        AppendCsvField(builder, totalConflictCount.ToString());
        AppendCsvField(builder, cameraTarget);
        AppendCsvField(builder, notes);
        AppendCsvField(builder, droneBreakdown, true);
        return builder.ToString();
    }

    private static void AppendCsvField(StringBuilder builder, string value, bool isLast = false)
    {
        string safeValue = value ?? string.Empty;
        bool requiresQuotes =
            safeValue.Contains(",") ||
            safeValue.Contains("\"") ||
            safeValue.Contains("\n") ||
            safeValue.Contains("\r");

        if (requiresQuotes)
        {
            safeValue = "\"" + safeValue.Replace("\"", "\"\"") + "\"";
        }

        builder.Append(safeValue);
        if (!isLast)
        {
            builder.Append(',');
        }
    }
}
