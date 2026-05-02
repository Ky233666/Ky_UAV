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
        if (request == null || request.gridCellSize <= 0f)
        {
            return blockedNodes;
        }

        PlanningGridMap map = request.planningMap;
        if (map == null || !map.IsValid)
        {
            return blockedNodes;
        }

        if (map.width * map.height > MaxObstaclePreviewCells)
        {
            return blockedNodes;
        }

        return map.GetBlockedWorldPositions(MaxBlockedPreviewNodes);
    }

}
