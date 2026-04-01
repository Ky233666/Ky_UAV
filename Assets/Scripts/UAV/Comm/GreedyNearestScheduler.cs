using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 贪心调度算法：
/// 每一步都选择“当前评分最优”的 无人机-任务 对进行分配。
/// 当前评分规则为：距离越近越优先，任务优先级越高越优先。
/// </summary>
public class GreedyNearestScheduler : ISchedulerAlgorithm
{
    public string AlgorithmName => "Greedy Nearest";

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

        List<TaskPoint> remainingTasks = new List<TaskPoint>(tasks);
        Dictionary<int, Vector3> currentPositions = new Dictionary<int, Vector3>();
        Dictionary<int, DroneTaskAssignment> assignments = new Dictionary<int, DroneTaskAssignment>();

        foreach (DroneData drone in drones)
        {
            if (drone == null)
            {
                continue;
            }

            currentPositions[drone.droneId] = drone.lastKnownPosition;
            assignments[drone.droneId] = new DroneTaskAssignment
            {
                droneId = drone.droneId,
                droneName = drone.droneName
            };
        }

        while (remainingTasks.Count > 0)
        {
            DroneData bestDrone = null;
            TaskPoint bestTask = null;
            float bestScore = float.MaxValue;

            foreach (DroneData drone in drones)
            {
                if (drone == null || !assignments.ContainsKey(drone.droneId))
                {
                    continue;
                }

                Vector3 currentPosition = currentPositions.TryGetValue(drone.droneId, out Vector3 knownPosition)
                    ? knownPosition
                    : request.fallbackSpawnOrigin;

                foreach (TaskPoint task in remainingTasks)
                {
                    if (task == null)
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(currentPosition, task.transform.position);
                    float score = distance - task.priority * request.priorityWeight;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestDrone = drone;
                        bestTask = task;
                    }
                }
            }

            if (bestDrone == null || bestTask == null)
            {
                result.message = "贪心调度中断，未找到可分配组合";
                return result;
            }

            assignments[bestDrone.droneId].assignedTasks.Add(bestTask);
            currentPositions[bestDrone.droneId] = bestTask.transform.position;
            remainingTasks.Remove(bestTask);
        }

        foreach (DroneData drone in drones)
        {
            if (drone != null && assignments.TryGetValue(drone.droneId, out DroneTaskAssignment assignment))
            {
                result.assignments.Add(assignment);
            }
        }

        result.success = true;
        result.message = $"已按贪心最近优先策略为 {result.assignments.Count} 架无人机生成任务分配";
        return result;
    }
}
