/// <summary>
/// 额外支持过程记录的路径规划器接口。
/// 规划算法仍然只关心自身求解；是否记录步骤由外部 recorder 决定。
/// </summary>
public interface IPathPlannerWithVisualization : IPathPlanner
{
    PathPlanningResult PlanPath(PathPlanningRequest request, PathPlanningVisualizationRecorder recorder);
}
