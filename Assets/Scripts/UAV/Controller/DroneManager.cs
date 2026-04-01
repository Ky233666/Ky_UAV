using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 无人机管理器：生成并管理多架无人机
/// </summary>
public class DroneManager : MonoBehaviour
{
    public static DroneManager Instance { get; private set; }

    [Header("无人机 Prefab")]
    [Tooltip("无人机 Prefab")]
    public DroneController dronePrefab;

    [Header("生成设置")]
    [Tooltip("无人机数量")]
    public int droneCount = 4;

    [Tooltip("生成起始位置")]
    public Vector3 spawnOrigin = new Vector3(0, 0, 0);

    [Tooltip("生成间距")]
    public float spawnSpacing = 3f;

    [Tooltip("排列方向")]
    public Vector3 spawnDirection = new Vector3(1, 0, 0);

    [Header("管理")]
    [Tooltip("所有无人机列表")]
    public List<DroneController> drones = new List<DroneController>();

    [Tooltip("无人机数据列表")]
    public List<DroneData> droneDataList = new List<DroneData>();

    [Header("调度算法")]
    [Tooltip("当前使用的任务调度算法")]
    public SchedulerAlgorithmType schedulerAlgorithm = SchedulerAlgorithmType.EvenSplit;

    void Awake()
    {
        // 单例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // 自动生成无人机（如果需要）
        if (drones.Count == 0 && dronePrefab != null)
        {
            SpawnDrones(droneCount);
        }
    }

    /// <summary>
    /// 生成指定数量的无人机
    /// </summary>
    public void SpawnDrones(int count)
    {
        if (dronePrefab == null)
        {
            Debug.LogError("[DroneManager] 未设置无人机 Prefab");
            return;
        }

        // 清除现有无人机
        ClearAllDrones();

        for (int i = 0; i < count; i++)
        {
            // 计算位置
            Vector3 position = spawnOrigin + spawnDirection * (i * spawnSpacing);

            // 实例化
            GameObject go = Instantiate(dronePrefab.gameObject, position, Quaternion.identity);
            go.transform.SetParent(transform);

            // 获取组件
            DroneController drone = go.GetComponent<DroneController>();
            if (drone != null)
            {
                // 初始化
                string name = $"无人机 {(i + 1):D2}";
                drone.Initialize(i + 1, name);

                drones.Add(drone);

                // 创建数据
                DroneData data = new DroneData
                {
                    droneId = drone.droneId,
                    droneName = drone.droneName,
                    speed = drone.speed,
                    state = DroneState.Idle,
                    lastKnownPosition = drone.transform.position
                };
                droneDataList.Add(data);

                // 创建并关联状态机
                DroneStateMachine sm = go.GetComponent<DroneStateMachine>();
                if (sm == null)
                    sm = go.AddComponent<DroneStateMachine>();

                sm.droneController = drone;
                sm.droneData = data;
                drone.stateMachine = sm;

                Debug.Log($"[DroneManager] 无人机 {drone.droneId} 状态机已创建");
            }
        }

        Debug.Log($"[DroneManager] 已生成 {count} 架无人机");
    }

    /// <summary>
    /// 清除所有无人机
    /// </summary>
    public void ClearAllDrones()
    {
        foreach (var drone in drones)
        {
            if (drone != null)
                Destroy(drone.gameObject);
        }
        drones.Clear();
        droneDataList.Clear();
    }

    /// <summary>
    /// 获取指定编号的无人机
    /// </summary>
    public DroneController GetDrone(int droneId)
    {
        return drones.Find(d => d.droneId == droneId);
    }

    /// <summary>
    /// 获取指定编号的无人机数据
    /// </summary>
    public DroneData GetDroneData(int droneId)
    {
        return droneDataList.Find(d => d.droneId == droneId);
    }

    /// <summary>
    /// 获取指定编号无人机的状态机
    /// </summary>
    public DroneStateMachine GetDroneStateMachine(int droneId)
    {
        DroneController drone = GetDrone(droneId);
        return drone?.stateMachine;
    }

    /// <summary>
    /// 重置所有无人机
    /// </summary>
    public void ResetAllDrones()
    {
        foreach (var drone in drones)
        {
            if (drone != null)
            {
                drone.Reset();
            }
        }

        SyncDronePositionsToData();
        Debug.Log("[DroneManager] 所有无人机已重置");
    }

    /// <summary>
    /// 为指定无人机分配任务队列
    /// </summary>
    public void AssignTaskQueue(int droneId, TaskPoint[] tasks)
    {
        DroneData data = GetDroneData(droneId);
        if (data != null)
        {
            data.taskQueue = tasks;
            data.currentTaskIndex = 0;
            data.state = DroneState.Idle;
            Debug.Log($"[DroneManager] 无人机 {droneId} 已分配 {tasks.Length} 个任务");
        }
    }

    /// <summary>
    /// 为所有空闲无人机自动分配任务。
    /// 当前默认仍使用均分策略，但入口已切换为调度算法接口。
    /// </summary>
    public SchedulingResult AutoAssignTasks(TaskPoint[] allTasks)
    {
        if (allTasks == null || allTasks.Length == 0)
        {
            Debug.LogWarning("[DroneManager] 没有可分配的任务点");
            return new SchedulingResult
            {
                success = false,
                algorithmName = schedulerAlgorithm.ToString(),
                message = "没有可分配的任务点"
            };
        }

        SyncDronePositionsToData();

        SchedulingRequest request = new SchedulingRequest
        {
            drones = droneDataList.Where(data => data != null && data.isOnline).ToList(),
            tasks = allTasks.Where(task => task != null).ToList(),
            sortByPriority = false,
            fallbackSpawnOrigin = spawnOrigin,
            priorityWeight = 5f
        };

        ISchedulerAlgorithm scheduler = CreateSchedulerAlgorithm();
        SchedulingResult result = scheduler.ScheduleTasks(request);

        if (!result.success)
        {
            Debug.LogWarning($"[DroneManager] 调度失败: {result.message}");
            return result;
        }

        foreach (DroneTaskAssignment assignment in result.assignments)
        {
            if (assignment == null)
            {
                continue;
            }

            AssignTaskQueue(assignment.droneId, assignment.assignedTasks.ToArray());
        }

        Debug.Log($"[DroneManager] 调度完成: {result.algorithmName} | {result.message}");
        return result;
    }

    /// <summary>
    /// 创建当前配置对应的调度算法实例。
    /// </summary>
    public ISchedulerAlgorithm CreateSchedulerAlgorithm()
    {
        switch (schedulerAlgorithm)
        {
            case SchedulerAlgorithmType.GreedyNearest:
                return new GreedyNearestScheduler();

            case SchedulerAlgorithmType.EvenSplit:
            default:
                return new EvenSplitScheduler();
        }
    }

    /// <summary>
    /// 将场景中无人机当前位置同步到 DroneData，供调度算法估算距离。
    /// </summary>
    private void SyncDronePositionsToData()
    {
        foreach (DroneController drone in drones)
        {
            if (drone == null)
            {
                continue;
            }

            DroneData data = GetDroneData(drone.droneId);
            if (data != null)
            {
                data.lastKnownPosition = drone.transform.position;
            }
        }
    }

    /// <summary>
    /// 获取在线无人机数量
    /// </summary>
    public int GetOnlineDroneCount()
    {
        int count = 0;
        foreach (var data in droneDataList)
        {
            if (data.isOnline) count++;
        }
        return count;
    }

    /// <summary>
    /// 获取所有无人机状态摘要
    /// </summary>
    public string GetStatusSummary()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== 无人机状态摘要 ===");
        sb.AppendLine($"总数: {drones.Count} | 在线: {GetOnlineDroneCount()}");

        foreach (var data in droneDataList)
        {
            string status = data.state.ToString();
            string tasks = data.HasPendingTasks()
                ? $"{data.currentTaskIndex + 1}/{data.taskQueue.Length}"
                : "无任务";
            sb.AppendLine($"  [{data.droneId:D2}] {data.droneName}: {status} | 任务: {tasks}");
        }

        return sb.ToString();
    }
}
