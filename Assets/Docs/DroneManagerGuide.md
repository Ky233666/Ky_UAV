# 多无人机仿真系统 - 管理器设计教程

---

## 1. 推荐的无人机管理器设计

采用 **单例模式 + 分层管理**：

```
┌─────────────────────────────────────┐
│           DroneManager              │
│  （单例，整个场景只需一个）          │
├─────────────────────────────────────┤
│  - dronePrefab: DroneController    │
│  - droneCount: int                  │
│  - drones: List<DroneController>   │
│  - droneDataList: List<DroneData>   │
├─────────────────────────────────────┤
│  + SpawnDrones(count)               │
│  + ClearAllDrones()                 │
│  + GetDrone(id)                     │
│  + AssignTaskQueue(id, tasks)       │
│  + AutoAssignTasks(allTasks)        │
└─────────────────────────────────────┘
                    │
                    ▼
        ┌───────────────────┐
        │   DroneController │
        │   （每个无人机）  │
        ├───────────────────┤
        │ - droneId         │
        │ - droneName       │
        │ - speed           │
        │ - targetPoint     │
        │ - currentState   │
        └───────────────────┘
                    │
                    ▼
        ┌───────────────────┐
        │     DroneData    │
        │   （数据容器）    │
        ├───────────────────┤
        │ - droneId         │
        │ - droneName       │
        │ - state           │
        │ - taskQueue       │
        │ - currentTaskIdx  │
        │ - isOnline        │
        └───────────────────┘
```

---

## 2. 需要新增的脚本

| 脚本 | 作用 |
|---|---|
| `DroneData.cs` | 无人机的数据结构体，存储任务队列、状态、飞行统计等信息 |
| `DroneManager.cs` | 负责批量生成、清除、获取无人机，支持任务分配 |

**修改现有脚本：**

| 脚本 | 修改内容 |
|---|---|
| `DroneController.cs` | 新增 `droneId`、`droneName`、`currentState` 字段和 `Initialize()` 方法，状态更新为 `DroneState` 枚举 |

---

## 3. 完整代码

### DroneData.cs

```csharp
using UnityEngine;

/// <summary>
/// 无人机状态
/// </summary>
public enum DroneState
{
    Idle,         // 空闲
    Flying,       // 飞行中
    Arrived,      // 已到达目标
    Returning     // 返回中
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
```

### DroneManager.cs

```csharp
using UnityEngine;
using System.Collections.Generic;

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
                    state = DroneState.Idle
                };
                droneDataList.Add(data);
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
    /// 为所有空闲无人机自动分配任务（简单轮询）
    /// </summary>
    public void AutoAssignTasks(TaskPoint[] allTasks)
    {
        if (allTasks == null || allTasks.Length == 0)
        {
            Debug.LogWarning("[DroneManager] 没有可分配的任务点");
            return;
        }

        int droneIndex = 0;
        foreach (var drone in drones)
        {
            if (drone == null) continue;

            DroneData data = GetDroneData(drone.droneId);
            if (data == null) continue;

            // 每个无人机分配部分任务
            int tasksPerDrone = Mathf.CeilToInt((float)allTasks.Length / drones.Count);
            int startIndex = droneIndex * tasksPerDrone;
            int endIndex = Mathf.Min(startIndex + tasksPerDrone, allTasks.Length);

            if (startIndex < allTasks.Length)
            {
                int count = endIndex - startIndex;
                TaskPoint[] subset = new TaskPoint[count];
                for (int i = 0; i < count; i++)
                {
                    subset[i] = allTasks[startIndex + i];
                }

                AssignTaskQueue(drone.droneId, subset);
                droneIndex++;
            }
        }

        Debug.Log($"[DroneManager] 已为 {droneIndex} 架无人机分配任务");
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
```

### DroneController.cs 新增字段

```csharp
[Header("无人机信息")]
[Tooltip("无人机唯一编号")]
public int droneId;

[Tooltip("无人机名称")]
public string droneName = "无人机";

[Header("状态")]
[Tooltip("当前状态")]
public DroneState currentState = DroneState.Idle;
```

### DroneController.cs 新增方法

```csharp
/// <summary>
/// 初始化无人机信息
/// </summary>
public void Initialize(int id, string name)
{
    droneId = id;
    droneName = name;
    gameObject.name = $"Drone_{id:D2}_{name}";
}
```

### DroneController.cs Update 修改

```csharp
void Update()
{
    // 检查仿真状态
    if (SimulationManager.Instance != null)
    {
        if (SimulationManager.Instance.currentState != SimulationState.Running)
            return;
    }

    // 没有目标点或已到达则不移动
    if (targetPoint == null || hasArrived)
    {
        if (hasArrived && currentState != DroneState.Arrived)
            currentState = DroneState.Arrived;
        return;
    }

    // 更新状态为飞行中
    if (currentState != DroneState.Flying)
        currentState = DroneState.Flying;

    // ... 其余原有代码 ...
}
```

---

## 4. 如何在场景中生成多架无人机

### 步骤 1：创建 DroneManager 对象

1. 在 Hierarchy 右键 → Create Empty，命名为 `DroneManager`
2. 把 `DroneManager.cs` 拖上去

### 步骤 2：配置 Inspector

选中 `DroneManager`，设置：

| 属性 | 值 |
|---|---|
| Drone Prefab | 拖入你的 **Drone Prefab**（从 Project 窗口拖） |
| Drone Count | 4（或 5、6） |
| Spawn Origin | `0, 0, 0`（或你想设置的起始位置） |
| Spawn Spacing | 3（无人机之间的间距） |
| Spawn Direction | `1, 0, 0`（X 轴排列） |

### 步骤 3：运行测试

1. 运行 Unity
2. 场景中应该自动生成 4 架无人机
3. 每架无人机的名称会是 `Drone_01_无人机 01`、`Drone_02_无人机 02`……

---

## 5. 每架无人机至少保存的数据

从 `DroneData` 可以看出，每架无人机保存：

| 数据 | 说明 |
|---|---|
| `droneId` | 唯一编号 |
| `droneName` | 显示名称 |
| `state` | 当前状态（Idle/Flying/Arrived/Returning） |
| `speed` | 飞行速度 |
| `taskQueue` | 任务点数组 |
| `currentTaskIndex` | 当前任务索引 |
| `totalFlightDistance` | 累计飞行距离 |
| `completedTasks` | 已完成任务数 |
| `isOnline` | 是否在线 |

这些数据足够支撑后续的任务分配和状态监控。

---

## 6. 如何为后续任务分配做准备

当前代码已经预留了以下接口：

### 方式一：手动分配任务给单架无人机

```csharp
DroneManager dm = DroneManager.Instance;
TaskPoint[] tasks = FindObjectsOfType<TaskPoint>();
dm.AssignTaskQueue(1, tasks); // 给 1 号无人机分配所有任务点
```

### 方式二：自动平分任务

```csharp
DroneManager dm = DroneManager.Instance;
TaskPoint[] allTasks = FindObjectsOfType<TaskPoint>();
dm.AutoAssignTasks(allTasks); // 自动平分给所有无人机
```

### 方式三：通过数据类获取任务

```csharp
DroneData data = DroneManager.Instance.GetDroneData(1);
TaskPoint currentTask = data.GetCurrentTask(); // 获取当前要飞的任务点
```

后续可以在 `DroneController.Update()` 中检测 `DroneData.HasPendingTasks()`，如果有任务就自动取下一个 `GetCurrentTask()` 并飞过去。

---

## 常见问题

### Q：无人机Prefabs 从哪来？
A：把场景里的一架无人机拖到 Project 窗口，变成 Prefab，然后删掉场景里的那个。

### Q：可以动态改数量吗？
A：可以，运行时调用 `DroneManager.Instance.SpawnDrones(6)` 即可重新生成 6 架。

### Q：怎么知道某架无人机飞到了？
A：后续在 `DroneController.Update()` 里检测 `hasArrived == true`，然后从 `DroneData` 取下一个任务继续飞。
