using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class RLMapExporter : MonoBehaviour
{
    [Header("References")]
    public DroneManager droneManager;

    [Header("Export")]
    public bool includeAllTaskPoints = true;
    public bool alsoWriteDefaultMapFile = false;

    [Header("Training Map Limits")]
    public int maxGridWidth = 80;
    public int maxGridHeight = 80;
    public int maxGridCells = 6400;

    public string LastExportPath { get; private set; } = "";
    public string LastExportCaseName { get; private set; } = "";
    public string LastExportMessage { get; private set; } = "RL map not exported";

    [ContextMenu("Export First Drone First Task Map")]
    public void ExportFirstDroneFirstTaskMap()
    {
        CacheReferences();
        if (droneManager == null || droneManager.drones == null || droneManager.drones.Count == 0)
        {
            LastExportMessage = "No drone available for RL map export";
            Debug.LogWarning($"[RLMapExporter] {LastExportMessage}");
            return;
        }

        TaskPoint[] tasks = SimulationContext.GetOrCreate(this).GetTaskPoints();
        if (tasks == null || tasks.Length == 0 || tasks[0] == null)
        {
            LastExportMessage = "No task point available for RL map export";
            Debug.LogWarning($"[RLMapExporter] {LastExportMessage}");
            return;
        }

        ExportMapForTask(droneManager.drones[0].droneId, tasks[0]);
    }

    public string ExportCurrentTaskMap(int droneId)
    {
        CacheReferences();
        if (droneManager == null)
        {
            LastExportMessage = "DroneManager not found";
            Debug.LogWarning($"[RLMapExporter] {LastExportMessage}");
            return string.Empty;
        }

        DroneData droneData = droneManager.GetDroneData(droneId);
        TaskPoint currentTask = droneData != null ? droneData.GetCurrentTask() : null;
        return ExportMapForTask(droneId, currentTask);
    }

    public string ExportMapForTask(int droneId, TaskPoint taskPoint)
    {
        CacheReferences();
        if (droneManager == null)
        {
            LastExportMessage = "DroneManager not found";
            Debug.LogWarning($"[RLMapExporter] {LastExportMessage}");
            return string.Empty;
        }

        DroneController drone = droneManager.GetDrone(droneId);
        if (drone == null)
        {
            LastExportMessage = $"Drone {droneId} not found";
            Debug.LogWarning($"[RLMapExporter] {LastExportMessage}");
            return string.Empty;
        }

        if (taskPoint == null)
        {
            LastExportMessage = "Task point is null";
            Debug.LogWarning($"[RLMapExporter] {LastExportMessage}");
            return string.Empty;
        }

        if (!TryValidateExportGrid(out string validationMessage))
        {
            LastExportMessage = validationMessage;
            Debug.LogWarning($"[RLMapExporter] {LastExportMessage}");
            return string.Empty;
        }

        RLMapJson map = BuildMap(drone, taskPoint);
        string caseName = RLPathPlanningFileUtility.CreateCaseName(droneId, taskPoint.taskId);
        string casePath = RLPathPlanningFileUtility.GetCaseMapPath(caseName);
        WriteMapJson(map, casePath);
        RLPathPlanningFileUtility.SetSelectedCaseName(caseName);

        if (alsoWriteDefaultMapFile)
        {
            WriteMapJson(map, RLPathPlanningFileUtility.GetDefaultMapPath());
            WriteMapJson(map, RLPathPlanningFileUtility.GetNamedMapPath(droneId, taskPoint.taskId));
        }

        LastExportPath = casePath;
        LastExportCaseName = caseName;
        LastExportMessage = $"Exported RL case: {caseName}";
        Debug.Log($"[RLMapExporter] {LastExportMessage} | {casePath}");
        return casePath;
    }

    public RLMapJson BuildMap(DroneController drone, TaskPoint taskPoint)
    {
        CacheReferences();
        RLCoordinateConverter converter = BuildConverter();
        RLMapJson map = new RLMapJson
        {
            width = converter.Width,
            height = converter.Height,
            grid_size = Mathf.Max(0.01f, droneManager.planningGridCellSize),
            drone_id = drone != null ? drone.droneId : 0,
            task_id = taskPoint != null ? taskPoint.taskId : 0,
            start = converter.WorldToGridCoord(drone != null ? drone.transform.position : Vector3.zero),
            goal = converter.WorldToGridCoord(taskPoint != null ? taskPoint.transform.position : Vector3.zero),
            world_transform = converter.BuildWorldTransform()
        };

        map.obstacles = SampleObstacleGrid(converter, map.start, map.goal);
        if (includeAllTaskPoints)
        {
            map.task_points = BuildTaskPointList(converter);
        }

        return map;
    }

    public bool TryValidateExportGrid(out string message)
    {
        CacheReferences();
        if (droneManager == null)
        {
            message = "DroneManager not found";
            return false;
        }

        RLCoordinateConverter converter = BuildConverter();
        int cellCount = converter.Width * converter.Height;
        if (converter.Width > maxGridWidth ||
            converter.Height > maxGridHeight ||
            cellCount > maxGridCells)
        {
            message =
                $"RL map too large: {converter.Width}x{converter.Height}={cellCount} cells. " +
                $"Limit is {maxGridWidth}x{maxGridHeight} and {maxGridCells} cells. Increase grid size or shrink X/Z bounds.";
            return false;
        }

        message = $"RL map size OK: {converter.Width}x{converter.Height}={cellCount} cells";
        return true;
    }

    private void CacheReferences()
    {
        droneManager = RuntimeSceneRegistry.Resolve(droneManager, this);
    }

    private RLCoordinateConverter BuildConverter()
    {
        float cruiseHeight = droneManager != null ? droneManager.cruiseHeight : 5f;
        return new RLCoordinateConverter(
            droneManager.planningWorldMin,
            droneManager.planningWorldMax,
            droneManager.planningGridCellSize,
            cruiseHeight);
    }

    private List<RLGridCoord> SampleObstacleGrid(
        RLCoordinateConverter converter,
        RLGridCoord start,
        RLGridCoord goal)
    {
        List<RLGridCoord> obstacles = new List<RLGridCoord>();
        if (droneManager == null || droneManager.planningObstacleLayer.value == 0)
        {
            return obstacles;
        }

        float cellSize = Mathf.Max(0.01f, droneManager.planningGridCellSize);
        float verticalHalfExtent = Mathf.Max(
            (droneManager.planningWorldMax.y - droneManager.planningWorldMin.y) * 0.5f,
            1f);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(cellSize * 0.5f, 0.15f),
            verticalHalfExtent,
            Mathf.Max(cellSize * 0.5f, 0.15f));

        for (int y = 0; y < converter.Height; y++)
        {
            for (int x = 0; x < converter.Width; x++)
            {
                if (IsSameCell(start, x, y) || IsSameCell(goal, x, y))
                {
                    continue;
                }

                Vector3 world = converter.GridToWorld(x, y);
                Vector3 probeCenter = new Vector3(
                    world.x,
                    droneManager.planningWorldMin.y + verticalHalfExtent,
                    world.z);
                if (Physics.CheckBox(
                        probeCenter,
                        halfExtents,
                        Quaternion.identity,
                        droneManager.planningObstacleLayer,
                        QueryTriggerInteraction.Ignore))
                {
                    obstacles.Add(new RLGridCoord(x, y));
                }
            }
        }

        return obstacles;
    }

    private List<RLTaskPointJson> BuildTaskPointList(RLCoordinateConverter converter)
    {
        List<RLTaskPointJson> taskPoints = new List<RLTaskPointJson>();
        TaskPoint[] sceneTasks = SimulationContext.GetOrCreate(this).GetTaskPoints();
        for (int i = 0; i < sceneTasks.Length; i++)
        {
            TaskPoint taskPoint = sceneTasks[i];
            if (taskPoint == null)
            {
                continue;
            }

            RLGridCoord grid = converter.WorldToGridCoord(taskPoint.transform.position);
            taskPoints.Add(new RLTaskPointJson
            {
                task_id = taskPoint.taskId,
                x = grid.x,
                y = grid.y,
                priority = taskPoint.priority
            });
        }

        return taskPoints;
    }

    private static bool IsSameCell(RLGridCoord coord, int x, int y)
    {
        return coord != null && coord.x == x && coord.y == y;
    }

    private static void WriteMapJson(RLMapJson map, string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonUtility.ToJson(map, true), new UTF8Encoding(true));
    }
}
