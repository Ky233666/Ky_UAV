using UnityEngine;

/// <summary>
/// 最小可用路径规划器：直接返回起点到终点的直线路径。
/// 该实现主要用于先接通路径规划入口层，便于后续平滑替换为 A*。
/// </summary>
public class StraightLinePlanner : IPathPlanner
{
    public string PlannerName => "Straight Line";

    public bool SupportsDynamicReplan => true;

    public PathPlanningResult PlanPath(PathPlanningRequest request)
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

        result.waypoints.Add(request.startPosition);
        result.waypoints.Add(request.targetPosition);
        result.totalCost = Vector3.Distance(request.startPosition, request.targetPosition);
        result.success = true;
        result.message = "已生成直线路径";
        return result;
    }
}
