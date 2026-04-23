using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared helpers for building planner visualization steps and fallback traces.
/// </summary>
public static class PathPlanningVisualizationBuilder
{
    private const int MaxObstaclePreviewCells = 4096;
    private const int MaxBlockedPreviewNodes = 2048;

    public static PathPlanningVisualizationStep CreateStep(
        PathPlanningVisualizationStepType stepType,
        string description)
    {
        return new PathPlanningVisualizationStep
        {
            stepType = stepType,
            description = description ?? string.Empty
        };
    }

    public static PathPlanningVisualizationNodeState CreateNode(
        Vector3 position,
        PathPlanningVisualizationNodeRole role,
        int order = 0,
        float cost = 0f,
        string label = "")
    {
        return new PathPlanningVisualizationNodeState
        {
            position = position,
            role = role,
            order = order,
            cost = cost,
            label = label ?? string.Empty
        };
    }

    public static PathPlanningVisualizationEdgeState CreateEdge(
        Vector3 from,
        Vector3 to,
        PathPlanningVisualizationEdgeRole role,
        int order = 0,
        float weight = 0f,
        string label = "")
    {
        return new PathPlanningVisualizationEdgeState
        {
            from = from,
            to = to,
            role = role,
            order = order,
            weight = weight,
            label = label ?? string.Empty
        };
    }

    public static void RecordInitialization(
        PathPlanningVisualizationRecorder recorder,
        PathPlanningRequest request,
        string description)
    {
        if (recorder == null || request == null)
        {
            return;
        }

        PathPlanningVisualizationStep step = CreateStep(
            PathPlanningVisualizationStepType.Initialize,
            description);
        step.nodeUpdates.Add(CreateNode(request.startPosition, PathPlanningVisualizationNodeRole.Start, label: "S"));
        step.nodeUpdates.Add(CreateNode(request.targetPosition, PathPlanningVisualizationNodeRole.Goal, label: "G"));

        List<Vector3> blockedNodes = BuildBlockedNodePreview(request);
        for (int i = 0; i < blockedNodes.Count; i++)
        {
            step.nodeUpdates.Add(CreateNode(blockedNodes[i], PathPlanningVisualizationNodeRole.Blocked));
        }

        recorder.RecordStep(step);
    }

    public static void RecordSearchFinished(
        PathPlanningVisualizationRecorder recorder,
        bool succeeded,
        string description)
    {
        if (recorder == null)
        {
            return;
        }

        PathPlanningVisualizationStep step = CreateStep(
            PathPlanningVisualizationStepType.SearchFinished,
            description);
        step.markSearchComplete = true;
        step.markSearchSucceeded = succeeded;
        recorder.RecordStep(step);
    }

    public static void RecordFallbackTrace(
        PathPlanningVisualizationRecorder recorder,
        PathPlanningRequest request,
        PathPlanningResult result)
    {
        if (recorder == null || request == null)
        {
            return;
        }

        PathPlanningVisualizationTrace trace = recorder.BuildTrace();
        if (trace != null && trace.HasSteps())
        {
            return;
        }

        RecordInitialization(
            recorder,
            request,
            "初始化可视化轨迹。当前算法未输出细粒度事件，已回退为结果摘要模式。");

        if (result != null && result.HasPath())
        {
            PathPlanningVisualizationStep backtrackStep = CreateStep(
                PathPlanningVisualizationStepType.BacktrackPathUpdated,
                "已根据规划结果回放最终路径回溯。");
            backtrackStep.replaceBacktrackPath = true;
            backtrackStep.backtrackPath = new List<Vector3>(result.waypoints);
            recorder.RecordStep(backtrackStep);

            PathPlanningVisualizationStep finalStep = CreateStep(
                PathPlanningVisualizationStepType.FinalPathConfirmed,
                "已确认最终路径。");
            finalStep.replaceFinalPath = true;
            finalStep.finalPath = new List<Vector3>(result.waypoints);
            recorder.RecordStep(finalStep);
        }

        RecordSearchFinished(
            recorder,
            result != null && result.success,
            result != null ? result.message : "规划未生成有效结果。");
    }

    private static List<Vector3> BuildBlockedNodePreview(PathPlanningRequest request)
    {
        List<Vector3> blockedNodes = new List<Vector3>();
        if (request == null || request.obstacleLayer.value == 0 || request.gridCellSize <= 0f)
        {
            return blockedNodes;
        }

        int width = Mathf.RoundToInt((request.worldMax.x - request.worldMin.x) / request.gridCellSize) + 1;
        int height = Mathf.RoundToInt((request.worldMax.z - request.worldMin.z) / request.gridCellSize) + 1;
        if (width <= 0 || height <= 0 || width * height > MaxObstaclePreviewCells)
        {
            return blockedNodes;
        }

        HashSet<string> keys = new HashSet<string>();
        for (int x = 0; x < width && blockedNodes.Count < MaxBlockedPreviewNodes; x++)
        {
            for (int z = 0; z < height && blockedNodes.Count < MaxBlockedPreviewNodes; z++)
            {
                Vector3 point = new Vector3(
                    request.worldMin.x + x * request.gridCellSize,
                    Mathf.Lerp(request.worldMin.y, request.worldMax.y, 0.5f),
                    request.worldMin.z + z * request.gridCellSize);

                if (ApproximatelySamePoint(point, request.startPosition) ||
                    ApproximatelySamePoint(point, request.targetPosition) ||
                    !IsBlocked(point, request))
                {
                    continue;
                }

                if (keys.Add(BuildPointKey(point)))
                {
                    blockedNodes.Add(point);
                }
            }
        }

        return blockedNodes;
    }

    private static bool IsBlocked(Vector3 worldPosition, PathPlanningRequest request)
    {
        float horizontalHalfExtent = Mathf.Max(request.gridCellSize * 0.5f, 0.2f);
        float verticalHalfExtent = Mathf.Max((request.worldMax.y - request.worldMin.y) * 0.5f, 1f);
        Vector3 halfExtents = new Vector3(horizontalHalfExtent, verticalHalfExtent, horizontalHalfExtent);
        Vector3 probeCenter = new Vector3(worldPosition.x, request.worldMin.y + verticalHalfExtent, worldPosition.z);
        return Physics.CheckBox(
            probeCenter,
            halfExtents,
            Quaternion.identity,
            request.obstacleLayer,
            QueryTriggerInteraction.Ignore);
    }

    private static bool ApproximatelySamePoint(Vector3 a, Vector3 b)
    {
        return Vector3.SqrMagnitude(a - b) <= 0.0001f;
    }

    private static string BuildPointKey(Vector3 point)
    {
        return $"{Mathf.RoundToInt(point.x * 100f)}_{Mathf.RoundToInt(point.y * 100f)}_{Mathf.RoundToInt(point.z * 100f)}";
    }
}
