using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无人机状态（用于任务执行与冲突规避）
/// </summary>
public enum DroneState
{
    Idle,         // 空闲：未分配任务或任务已完成
    Moving,       // 移动中：正在飞向目标点
    Waiting,      // 等待中：等待资源或避让
    Finished      // 已完成：所有任务执行完毕
}

/// <summary>
/// 无人机数据结构（用于管理）
/// </summary>
[System.Serializable]
public class DroneData
{
    /// <summary>
    /// 无人机唯一编号
    /// </summary>
    public int droneId;

    /// <summary>
    /// 无人机名称
    /// </summary>
    public string droneName;

    /// <summary>
    /// 当前状态
    /// </summary>
    public DroneState state = DroneState.Idle;

    /// <summary>
    /// 当前速度
    /// </summary>
    public float speed;

    /// <summary>
    /// 最近一次已知位置，用于调度算法估算距离。
    /// </summary>
    public UnityEngine.Vector3 lastKnownPosition = UnityEngine.Vector3.zero;

    /// <summary>
    /// 当前任务最近一次规划得到的路径点列表。
    /// 后续接入 A* 后，将由状态机按该列表逐点执行。
    /// </summary>
    public List<Vector3> plannedPath = new List<Vector3>();

    /// <summary>
    /// 当前路径执行到的 waypoint 索引。
    /// </summary>
    public int currentWaypointIndex = 0;

    /// <summary>
    /// 最近一次使用的路径规划器名称。
    /// </summary>
    public string currentPlannerName = "";

    /// <summary>
    /// 任务队列（待分配任务点列表）
    /// </summary>
    public TaskPoint[] taskQueue;

    /// <summary>
    /// 当前任务索引
    /// </summary>
    public int currentTaskIndex = 0;

    /// <summary>
    /// 累计飞行距离
    /// </summary>
    public float totalFlightDistance = 0f;

    /// <summary>
    /// 完成任务数
    /// </summary>
    public int completedTasks = 0;

    /// <summary>
    /// 是否在线（用于模拟故障/离线）
    /// </summary>
    public bool isOnline = true;

    /// <summary>
    /// 等待原因（Waiting 状态时）
    /// </summary>
    public string waitReason = "";

    /// <summary>
    /// 本轮仿真进入 Waiting 的累计次数。
    /// </summary>
    public int waitCount = 0;

    /// <summary>
    /// 本轮仿真累计记录到的冲突事件次数。
    /// 该计数用于区分“等待次数”和“冲突触发次数”。
    /// </summary>
    public int conflictCount = 0;

    /// <summary>
    /// 最近一次冲突原因。
    /// </summary>
    public string lastConflictReason = "";

    /// <summary>
    /// 获取当前目标任务点
    /// </summary>
    public TaskPoint GetCurrentTask()
    {
        if (taskQueue == null || currentTaskIndex >= taskQueue.Length)
            return null;
        return taskQueue[currentTaskIndex];
    }

    /// <summary>
    /// 是否还有待执行任务
    /// </summary>
    public bool HasPendingTasks()
    {
        return taskQueue != null && currentTaskIndex < taskQueue.Length;
    }

    /// <summary>
    /// 移动到下一个任务
    /// </summary>
    public void MoveToNextTask()
    {
        if (HasPendingTasks())
        {
            currentTaskIndex++;
            completedTasks++;
        }
    }
}
