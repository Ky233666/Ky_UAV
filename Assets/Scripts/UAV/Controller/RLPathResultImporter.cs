using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RLPathResultImporter : MonoBehaviour
{
    [Header("References")]
    public DroneManager droneManager;

    public string LastImportPath { get; private set; } = "";
    public string LastImportMessage { get; private set; } = "RL path not imported";

    [ContextMenu("Validate Default Q-learning Path")]
    public void ValidateDefaultPath()
    {
        if (TryImportDefaultPath(out PathPlanningResult result))
        {
            Debug.Log($"[RLPathResultImporter] Imported path with {result.waypoints.Count} waypoints");
        }
        else
        {
            Debug.LogWarning($"[RLPathResultImporter] {LastImportMessage}");
        }
    }

    public bool TryImportDefaultPath(out PathPlanningResult result)
    {
        return TryImportPath(RLPathPlanningFileUtility.GetSelectedCasePathPath(), out result);
    }

    public bool TryImportPath(string path, out PathPlanningResult result)
    {
        CacheReferences();
        result = new PathPlanningResult
        {
            success = false,
            plannerName = "Q-learning Offline",
            message = "RL path import not executed"
        };

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            LastImportMessage = $"Path file not found: {path}";
            result.message = LastImportMessage;
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            RLPathJson pathJson = JsonUtility.FromJson<RLPathJson>(json);
            if (pathJson == null)
            {
                LastImportMessage = "Path JSON parse failed";
                result.message = LastImportMessage;
                return false;
            }

            result = ConvertToPlanningResult(pathJson);
            LastImportPath = path;
            LastImportMessage = result.message;
            return result.success;
        }
        catch (System.Exception exception)
        {
            LastImportMessage = $"Path import failed: {exception.Message}";
            result.message = LastImportMessage;
            Debug.LogError($"[RLPathResultImporter] {LastImportMessage}");
            return false;
        }
    }

    public PathPlanningResult ConvertToPlanningResult(RLPathJson pathJson)
    {
        PathPlanningResult result = new PathPlanningResult
        {
            success = pathJson != null && pathJson.success,
            plannerName = string.IsNullOrWhiteSpace(pathJson != null ? pathJson.algorithm : null)
                ? "Q-learning Offline"
                : pathJson.algorithm,
            message = pathJson != null ? pathJson.message : "Path JSON is null",
            waypoints = new List<Vector3>()
        };

        if (pathJson == null || pathJson.path == null || pathJson.path.Count == 0)
        {
            result.success = false;
            result.message = "RL path is empty";
            return result;
        }

        RLCoordinateConverter converter = BuildConverter(pathJson.world_transform);
        for (int i = 0; i < pathJson.path.Count; i++)
        {
            result.waypoints.Add(converter.GridToWorld(pathJson.path[i]));
        }

        result.totalCost = CalculatePathLength(result.waypoints);
        if (string.IsNullOrWhiteSpace(result.message))
        {
            result.message = result.success ? "Imported Q-learning path" : "Q-learning path reports failure";
        }

        return result;
    }

    private void CacheReferences()
    {
        droneManager = RuntimeSceneRegistry.Resolve(droneManager, this);
    }

    private RLCoordinateConverter BuildConverter(RLWorldTransform transform)
    {
        CacheReferences();
        if (transform != null && transform.cell_size > 0f)
        {
            Vector3 min = new Vector3(transform.origin_x, 0f, transform.origin_z);
            Vector3 max = droneManager != null
                ? droneManager.planningWorldMax
                : new Vector3(transform.origin_x + 100f * transform.cell_size, 0f, transform.origin_z + 100f * transform.cell_size);
            float cruiseY = transform.cruise_y;
            if (Mathf.Approximately(cruiseY, 0f) && droneManager != null)
            {
                cruiseY = droneManager.cruiseHeight;
            }

            return new RLCoordinateConverter(min, max, transform.cell_size, cruiseY);
        }

        if (droneManager != null)
        {
            return new RLCoordinateConverter(
                droneManager.planningWorldMin,
                droneManager.planningWorldMax,
                droneManager.planningGridCellSize,
                droneManager.cruiseHeight);
        }

        return new RLCoordinateConverter(Vector3.zero, new Vector3(100f, 0f, 100f), 1f, 5f);
    }

    private static float CalculatePathLength(List<Vector3> path)
    {
        if (path == null || path.Count <= 1)
        {
            return 0f;
        }

        float length = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            length += Vector3.Distance(path[i - 1], path[i]);
        }

        return length;
    }
}
