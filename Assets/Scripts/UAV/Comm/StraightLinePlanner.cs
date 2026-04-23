using UnityEngine;

/// <summary>
/// 最小可用路径规划器：直接返回起点到终点的直线路径。
/// </summary>
public class StraightLinePlanner : IPathPlannerWithVisualization
{
    public string PlannerName => "Straight Line";

    public bool SupportsDynamicReplan => true;

    public PathPlanningResult PlanPath(PathPlanningRequest request)
    {
        return PlanPath(request, null);
    }

    public PathPlanningResult PlanPath(
        PathPlanningRequest request,
        PathPlanningVisualizationRecorder recorder)
    {
        PathPlanningResult result = new PathPlanningResult
        {
            success = false,
            plannerName = PlannerName,
            message = "路径规划未执行"
        };

        if (request == null)
        {
            result.message = "路径规划请求为空";
            return result;
        }

        PathPlanningVisualizationBuilder.RecordInitialization(
            recorder,
            request,
            "直线路径规划初始化完成，准备直接连接起点与终点。");

        result.waypoints.Add(request.startPosition);
        result.waypoints.Add(request.targetPosition);
        result.totalCost = Vector3.Distance(request.startPosition, request.targetPosition);
        result.success = true;
        result.message = "已生成直线路径";

        if (recorder != null)
        {
            PathPlanningVisualizationStep candidateStep = PathPlanningVisualizationBuilder.CreateStep(
                PathPlanningVisualizationStepType.CandidatePathUpdated,
                "直线规划器不做搜索，直接生成唯一候选路径。");
            candidateStep.replaceCandidatePath = true;
            candidateStep.candidatePath = new System.Collections.Generic.List<Vector3>(result.waypoints);
            recorder.RecordStep(candidateStep);

            PathPlanningVisualizationStep finalStep = PathPlanningVisualizationBuilder.CreateStep(
                PathPlanningVisualizationStepType.FinalPathConfirmed,
                "已确认最终路径。");
            finalStep.replaceFinalPath = true;
            finalStep.finalPath = new System.Collections.Generic.List<Vector3>(result.waypoints);
            recorder.RecordStep(finalStep);

            PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, true, result.message);
        }

        return result;
    }
}
