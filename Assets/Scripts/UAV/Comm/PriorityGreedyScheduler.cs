using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 自定义优先级贪心调度算法：
/// 综合考虑任务优先级、飞行距离和当前负载，逐步选择评分最优的无人机-任务组合。
/// 评分越高越优，默认公式为：
/// score = priorityWeight * priority - distanceWeight * distance - loadWeight * assignedTaskCount
/// </summary>
public class PriorityGreedyScheduler : ISchedulerAlgorithm
{
    public string AlgorithmName => "Priority Greedy";

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
            float bestScore = float.MinValue;

            foreach (DroneData drone in drones)
            {
                if (drone == null || !assignments.ContainsKey(drone.droneId))
                {
                    continue;
                }

                Vector3 currentPosition = currentPositions.TryGetValue(drone.droneId, out Vector3 knownPosition)
                    ? knownPosition
                    : request.fallbackSpawnOrigin;

                int currentLoad = assignments[drone.droneId].assignedTasks.Count;
                if (request.maxTaskCapacity > 0 && currentLoad >= request.maxTaskCapacity)
                {
                    continue;
                }

                foreach (TaskPoint task in remainingTasks)
                {
                    if (task == null)
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(currentPosition, task.transform.position);
                    float score =
                        request.priorityWeight * task.priority -
                        request.distanceWeight * distance -
                        request.loadWeight * currentLoad;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDrone = drone;
                        bestTask = task;
                    }
                }
            }

            if (bestDrone == null || bestTask == null)
            {
                break;
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

        int assignedTaskCount = tasks.Count - remainingTasks.Count;
        result.success = assignedTaskCount == tasks.Count;
        result.message = assignedTaskCount == tasks.Count
            ? $"已按优先级贪心策略为 {result.assignments.Count} 架无人机生成任务分配"
            : $"优先级贪心受任务容量限制，仅分配 {assignedTaskCount}/{tasks.Count} 个任务";
        return result;
    }
}
