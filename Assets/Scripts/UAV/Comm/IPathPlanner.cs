/// <summary>
/// 路径规划算法统一接口。
/// </summary>
public interface IPathPlanner
{
    /// <summary>
    /// 算法名称，用于 UI 展示和实验记录。
    /// </summary>
    string PlannerName { get; }

    /// <summary>
    /// 是否支持运行中的重规划。
    /// </summary>
    bool SupportsDynamicReplan { get; }

    /// <summary>
    /// 执行一次路径规划。
    /// </summary>
    PathPlanningResult PlanPath(PathPlanningRequest request);
}
