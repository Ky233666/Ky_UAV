using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 调度请求。
/// </summary>
[System.Serializable]
public class SchedulingRequest
{
    /// <summary>
    /// 当前可用无人机列表。
    /// </summary>
    public List<DroneData> drones = new List<DroneData>();

    /// <summary>
    /// 当前待调度任务点列表。
    /// </summary>
    public List<TaskPoint> tasks = new List<TaskPoint>();

    /// <summary>
    /// 调度前是否先按任务优先级排序。
    /// </summary>
    public bool sortByPriority = true;

    /// <summary>
    /// 没有历史位置数据时使用的默认起点。
    /// </summary>
    public Vector3 fallbackSpawnOrigin = Vector3.zero;

    /// <summary>
    /// 任务优先级对贪心评分的影响权重。
    /// 数值越大，越倾向先分配高优先级任务。
    /// </summary>
    public float priorityWeight = 5f;
}

/// <summary>
/// 单架无人机的任务分配结果。
/// </summary>
[System.Serializable]
public class DroneTaskAssignment
{
    public int droneId;
    public string droneName = "";
    public List<TaskPoint> assignedTasks = new List<TaskPoint>();
}

/// <summary>
/// 调度结果。
/// </summary>
[System.Serializable]
public class SchedulingResult
{
    /// <summary>
    /// 调度是否成功。
    /// </summary>
    public bool success;

    /// <summary>
    /// 使用的调度算法名称。
    /// </summary>
    public string algorithmName = "";

    /// <summary>
    /// 给调用方的附加说明。
    /// </summary>
    public string message = "";

    /// <summary>
    /// 每架无人机的任务分配情况。
    /// </summary>
    public List<DroneTaskAssignment> assignments = new List<DroneTaskAssignment>();

    /// <summary>
    /// 获取指定无人机的分配结果。
    /// </summary>
    public DroneTaskAssignment GetAssignment(int droneId)
    {
        if (assignments == null)
        {
            return null;
        }

        return assignments.Find(item => item.droneId == droneId);
    }
}
