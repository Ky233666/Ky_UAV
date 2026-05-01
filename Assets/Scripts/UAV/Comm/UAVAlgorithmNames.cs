/// <summary>
/// 统一维护算法标识与显示名称，避免导出、UI 和测试各自维护一套映射。
/// </summary>
public static class UAVAlgorithmNames
{
    public static string GetSchedulerIdentifier(SchedulerAlgorithmType algorithmType)
    {
        switch (algorithmType)
        {
            case SchedulerAlgorithmType.PriorityGreedy:
                return "PriorityGreedy";
            case SchedulerAlgorithmType.GreedyNearest:
                return "GreedyNearest";
            case SchedulerAlgorithmType.EvenSplit:
            default:
                return "EvenSplit";
        }
    }

    public static string GetPlannerIdentifier(PathPlannerType plannerType)
    {
        switch (plannerType)
        {
            case PathPlannerType.QLearningOffline:
                return "QLearningOffline";
            case PathPlannerType.RRT:
                return "RRT";
            case PathPlannerType.AStar:
                return "AStar";
            case PathPlannerType.StraightLine:
            default:
                return "StraightLine";
        }
    }

    public static string GetSchedulerDisplayName(SchedulerAlgorithmType algorithmType)
    {
        switch (algorithmType)
        {
            case SchedulerAlgorithmType.PriorityGreedy:
                return "优先级贪心";
            case SchedulerAlgorithmType.GreedyNearest:
                return "最近优先";
            case SchedulerAlgorithmType.EvenSplit:
            default:
                return "均分任务";
        }
    }

    public static string GetPlannerDisplayName(PathPlannerType plannerType)
    {
        switch (plannerType)
        {
            case PathPlannerType.QLearningOffline:
                return "Q-learning";
            case PathPlannerType.RRT:
                return "RRT";
            case PathPlannerType.AStar:
                return "A*";
            case PathPlannerType.StraightLine:
            default:
                return "直线";
        }
    }
}
