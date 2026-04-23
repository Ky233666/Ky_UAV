using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一收集路径规划过程步骤，供播放管理器和场景渲染器使用。
/// </summary>
public sealed class PathPlanningVisualizationRecorder
{
    private readonly PathPlanningVisualizationTrace trace;
    private readonly int maxRecordedSteps;

    public PathPlanningVisualizationRecorder(
        int droneId,
        string droneName,
        PathPlannerType plannerType,
        string plannerName,
        string plannerDisplayName,
        Color accentColor,
        PathPlanningRequest request,
        int maxRecordedSteps = 8000)
    {
        this.maxRecordedSteps = Mathf.Max(128, maxRecordedSteps);
        trace = new PathPlanningVisualizationTrace
        {
            droneId = droneId,
            droneName = droneName ?? string.Empty,
            plannerType = plannerType,
            plannerName = plannerName ?? string.Empty,
            plannerDisplayName = plannerDisplayName ?? string.Empty,
            accentColor = accentColor,
            request = CloneRequest(request),
            recordedStepLimit = this.maxRecordedSteps
        };
    }

    public bool IsRecording => !trace.truncated;

    public void RecordStep(PathPlanningVisualizationStep step)
    {
        if (step == null || trace.truncated)
        {
            return;
        }

        if (trace.steps.Count >= maxRecordedSteps)
        {
            trace.truncated = true;
            return;
        }

        PathPlanningVisualizationStep clone = step.Clone();
        clone.stepIndex = trace.steps.Count;
        trace.steps.Add(clone);
    }

    public void FinalizeResult(PathPlanningResult result)
    {
        if (result == null)
        {
            trace.success = false;
            trace.resultMessage = "路径规划结果为空";
            return;
        }

        trace.success = result.success;
        trace.resultMessage = result.message ?? string.Empty;
        trace.finalPath = result.waypoints != null
            ? new List<Vector3>(result.waypoints)
            : new List<Vector3>();
    }

    public PathPlanningVisualizationTrace BuildTrace()
    {
        return trace;
    }

    private static PathPlanningRequest CloneRequest(PathPlanningRequest source)
    {
        if (source == null)
        {
            return null;
        }

        return new PathPlanningRequest
        {
            droneId = source.droneId,
            startPosition = source.startPosition,
            targetPosition = source.targetPosition,
            gridCellSize = source.gridCellSize,
            worldMin = source.worldMin,
            worldMax = source.worldMax,
            obstacleLayer = source.obstacleLayer,
            allowDiagonal = source.allowDiagonal
        };
    }
}
