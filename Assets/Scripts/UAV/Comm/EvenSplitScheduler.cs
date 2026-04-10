using System.Collections.Generic;

/// <summary>
/// 当前默认调度算法：按顺序均分任务。
/// 这是把原有 DroneManager 里的分配逻辑抽出后的兼容实现。
/// </summary>
public class EvenSplitScheduler : ISchedulerAlgorithm
{
    public string AlgorithmName => "Even Split";

    public SchedulingResult ScheduleTasks(SchedulingRequest request)
    {
        SchedulingResult result = new SchedulingResult
        {
            success = false,
            algorithmName = AlgorithmName,
            message = "调度未执行"
        };

        if (request == null)
        {
            result.message = "调度请求为空";
            return result;
        }

        List<DroneData> drones = request.drones ?? new List<DroneData>();
        List<TaskPoint> tasks = request.tasks ?? new List<TaskPoint>();

        if (drones.Count == 0)
        {
            result.message = "没有可用无人机";
            return result;
        }

        if (tasks.Count == 0)
        {
            result.success = true;
            result.message = "没有可调度的任务";
            return result;
        }

        int tasksPerDrone = UnityEngine.Mathf.CeilToInt((float)tasks.Count / drones.Count);
        if (request.maxTaskCapacity > 0)
        {
            tasksPerDrone = UnityEngine.Mathf.Min(tasksPerDrone, request.maxTaskCapacity);
        }

        for (int droneIndex = 0; droneIndex < drones.Count; droneIndex++)
        {
            DroneData drone = drones[droneIndex];
            DroneTaskAssignment assignment = new DroneTaskAssignment
            {
                droneId = drone.droneId,
                droneName = drone.droneName
            };

            int startIndex = droneIndex * tasksPerDrone;
            int endIndex = UnityEngine.Mathf.Min(startIndex + tasksPerDrone, tasks.Count);

            for (int taskIndex = startIndex; taskIndex < endIndex; taskIndex++)
            {
                assignment.assignedTasks.Add(tasks[taskIndex]);
            }

            result.assignments.Add(assignment);
        }

        int assignedTaskCount = 0;
        foreach (DroneTaskAssignment assignment in result.assignments)
        {
            if (assignment != null && assignment.assignedTasks != null)
            {
                assignedTaskCount += assignment.assignedTasks.Count;
            }
        }

        result.success = assignedTaskCount == tasks.Count;
        result.message = assignedTaskCount == tasks.Count
            ? $"已按均分策略为 {result.assignments.Count} 架无人机生成任务分配"
            : $"均分策略受任务容量限制，仅分配 {assignedTaskCount}/{tasks.Count} 个任务";
        return result;
    }
}
