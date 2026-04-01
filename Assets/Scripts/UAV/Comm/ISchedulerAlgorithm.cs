/// <summary>
/// 任务调度算法统一接口。
/// </summary>
public interface ISchedulerAlgorithm
{
    /// <summary>
    /// 算法名称，用于 UI 展示和实验记录。
    /// </summary>
    string AlgorithmName { get; }

    /// <summary>
    /// 根据无人机和任务点生成调度结果。
    /// </summary>
    SchedulingResult ScheduleTasks(SchedulingRequest request);
}
