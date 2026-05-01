using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class OfflineQlearningPathPlanner : IPathPlanner
{
    public string PlannerName => "Q-learning Offline";

    public bool SupportsDynamicReplan => false;

    public PathPlanningResult PlanPath(PathPlanningRequest request)
    {
        PathPlanningResult failedResult = new PathPlanningResult
        {
            success = false,
            plannerName = PlannerName,
            message = "Q-learning offline path planning not executed"
        };

        if (request == null)
        {
            failedResult.message = "Path planning request is null";
            return failedResult;
        }

        List<string> candidates = BuildCandidatePathList(request.droneId);
        for (int i = 0; i < candidates.Count; i++)
        {
            string path = candidates[i];
            if (!File.Exists(path))
            {
                continue;
            }

            PathPlanningResult result = TryReadPath(path, request);
            if (result.HasPath())
            {
                return result;
            }

            failedResult = result;
        }

        failedResult.message =
            "No usable Q-learning path.json found. Export map.json, run the Python trainer, then import the generated path.";
        return failedResult;
    }

    private static List<string> BuildCandidatePathList(int droneId)
    {
        List<string> candidates = new List<string>();
        if (RLPathPlanningFileUtility.TryGetSelectedCaseName(out string selectedCaseName))
        {
            string selectedCaseDirectory = RLPathPlanningFileUtility.GetCaseDirectory(selectedCaseName);
            candidates.Add(Path.Combine(selectedCaseDirectory, $"path_drone_{droneId:D2}.json"));
            candidates.Add(RLPathPlanningFileUtility.GetCasePathPath(selectedCaseName));
        }

        List<string> caseNames = RLPathPlanningFileUtility.GetCaseNames();
        for (int i = 0; i < caseNames.Count; i++)
        {
            string caseDirectory = RLPathPlanningFileUtility.GetCaseDirectory(caseNames[i]);
            candidates.Add(Path.Combine(caseDirectory, $"path_drone_{droneId:D2}.json"));
            candidates.Add(RLPathPlanningFileUtility.GetCasePathPath(caseNames[i]));
        }

        string projectOutput = RLPathPlanningFileUtility.GetProjectOutputDirectory();
        candidates.Add(Path.Combine(projectOutput, $"path_drone_{droneId:D2}.json"));
        candidates.Add(RLPathPlanningFileUtility.GetDefaultPathPath());

        string persistentOutput = RLPathPlanningFileUtility.GetPersistentOutputDirectory();
        candidates.Add(Path.Combine(persistentOutput, $"path_drone_{droneId:D2}.json"));
        candidates.Add(Path.Combine(persistentOutput, RLPathPlanningFileUtility.DefaultPathFileName));
        return candidates;
    }

    private PathPlanningResult TryReadPath(string path, PathPlanningRequest request)
    {
        PathPlanningResult result = new PathPlanningResult
        {
            success = false,
            plannerName = PlannerName,
            message = $"Failed to read Q-learning path: {Path.GetFileName(path)}"
        };

        try
        {
            RLPathJson pathJson = JsonUtility.FromJson<RLPathJson>(File.ReadAllText(path));
            if (pathJson == null)
            {
                result.message = "Q-learning path JSON is empty or invalid";
                return result;
            }

            if (pathJson.drone_id > 0 && pathJson.drone_id != request.droneId)
            {
                result.message = $"Q-learning path belongs to drone {pathJson.drone_id}, request is drone {request.droneId}";
                return result;
            }

            if (!pathJson.success)
            {
                result.message = string.IsNullOrWhiteSpace(pathJson.message)
                    ? "Q-learning path reports failure"
                    : pathJson.message;
                return result;
            }

            if (pathJson.path == null || pathJson.path.Count == 0)
            {
                result.message = "Q-learning path contains no waypoints";
                return result;
            }

            RLCoordinateConverter converter = BuildConverter(pathJson.world_transform, request);
            RLGridCoord requestStart = converter.WorldToGridCoord(request.startPosition);
            RLGridCoord requestGoal = converter.WorldToGridCoord(request.targetPosition);
            if (!IsSameCell(pathJson.start, requestStart) || !IsSameCell(pathJson.goal, requestGoal))
            {
                result.message = "Q-learning path start or goal does not match the current planning request";
                return result;
            }

            result.waypoints = new List<Vector3>(pathJson.path.Count);
            for (int i = 0; i < pathJson.path.Count; i++)
            {
                result.waypoints.Add(converter.GridToWorld(pathJson.path[i]));
            }

            result.totalCost = CalculatePathLength(result.waypoints);
            result.success = result.waypoints.Count > 0;
            result.message = string.IsNullOrWhiteSpace(pathJson.message)
                ? $"Imported Q-learning path from {Path.GetFileName(path)}"
                : pathJson.message;
            return result;
        }
        catch (System.Exception exception)
        {
            result.message = $"Q-learning path import failed: {exception.Message}";
            return result;
        }
    }

    private RLCoordinateConverter BuildConverter(RLWorldTransform transform, PathPlanningRequest request)
    {
        float cruiseY = request.startPosition.y;
        if (transform != null && transform.cell_size > 0f)
        {
            if (!Mathf.Approximately(transform.cruise_y, 0f))
            {
                cruiseY = transform.cruise_y;
            }

            Vector3 min = new Vector3(transform.origin_x, request.worldMin.y, transform.origin_z);
            return new RLCoordinateConverter(min, request.worldMax, transform.cell_size, cruiseY);
        }

        return new RLCoordinateConverter(
            request.worldMin,
            request.worldMax,
            request.gridCellSize,
            cruiseY);
    }

    private static bool IsSameCell(RLGridCoord left, RLGridCoord right)
    {
        return left != null &&
               right != null &&
               left.x == right.x &&
               left.y == right.y;
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
