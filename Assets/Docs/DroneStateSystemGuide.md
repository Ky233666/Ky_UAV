# 无人机状态系统教程

---

## 1. 推荐的状态设计方式

采用 **有限状态机（FSM）** 模式：

```
┌─────────────────────────────────────────────────────┐
│                 DroneStateMachine                   │
│                  （状态机组件）                     │
├─────────────────────────────────────────────────────┤
│  状态: Idle → Moving → Waiting → Finished         │
│                                                     │
│  事件: OnStateEnter / OnStateExit / OnStateUpdate │
└─────────────────────────────────────────────────────┘
                          │
                          ▼
              ┌─────────────────────────┐
              │      DroneController   │
              │      （飞行控制）       │
              └─────────────────────────┘
                          │
                          ▼
              ┌─────────────────────────┐
              │        DroneData        │
              │      （数据容器）       │
              └─────────────────────────┘
```

---

## 2. 新增/修改的脚本

| 脚本 | 操作 | 说明 |
|---|---|---|
| `DroneData.cs` | 修改 | 状态枚举改为 Idle/Moving/Waiting/Finished |
| `DroneStateMachine.cs` | 新增 | 状态机组件，处理状态切换逻辑 |
| `DroneController.cs` | 修改 | 集成状态机引用 |
| `DroneManager.cs` | 修改 | 自动创建并关联状态机 |

---

## 3. 完整代码

### DroneState.cs（修改后的枚举）

```csharp
/// <summary>
/// 无人机状态（用于任务执行与冲突规避）
/// </summary>
public enum DroneState
{
    Idle,      // 空闲：未分配任务或任务已完成
    Moving,    // 移动中：正在飞向目标点
    Waiting,   // 等待中：等待资源或避让
    Finished   // 已完成：所有任务执行完毕
}
```

### DroneStateMachine.cs（新增）

```csharp
using UnityEngine;
using System;

/// <summary>
/// 无人机状态机：管理无人机的状态切换
/// </summary>
public class DroneStateMachine : MonoBehaviour
{
    [Header("无人机数据")]
    public DroneController droneController;
    public DroneData droneData;

    [Header("当前状态")]
    public DroneState currentState = DroneState.Idle;

    [Header("等待设置")]
    public float waitTimeout = 10f;

    // 事件
    public event Action<DroneState> OnStateEnter;
    public event Action<DroneState> OnStateExit;
    public event Action<DroneState> OnStateUpdate;

    private float waitTimer = 0f;
    private DroneState previousState;

    void Awake()
    {
        if (droneController == null)
            droneController = GetComponent<DroneController>();
        if (droneData == null)
            droneData = new DroneData();
    }

    void Start()
    {
        ChangeState(DroneState.Idle);
    }

    void Update()
    {
        if (droneData == null || !droneData.isOnline)
            return;

        OnStateUpdate?.Invoke(currentState);

        switch (currentState)
        {
            case DroneState.Idle:
                UpdateIdleState();
                break;
            case DroneState.Moving:
                UpdateMovingState();
                break;
            case DroneState.Waiting:
                UpdateWaitingState();
                break;
            case DroneState.Finished:
                UpdateFinishedState();
                break;
        }
    }

    /// <summary>
    /// 切换状态
    /// </summary>
    public void ChangeState(DroneState newState)
    {
        if (currentState == newState) return;

        OnStateExit?.Invoke(currentState);
        previousState = currentState;
        currentState = newState;

        if (droneData != null) droneData.state = newState;

        OnStateEnter?.Invoke(currentState);
        HandleStateEnter(newState);

        Debug.Log($"[DroneStateMachine] {droneController?.droneName}: {previousState} → {currentState}");
    }

    private void HandleStateEnter(DroneState state)
    {
        waitTimer = 0f;
    }

    private void UpdateIdleState()
    {
        if (droneData != null && droneData.HasPendingTasks())
            ChangeState(DroneState.Moving);
    }

    private void UpdateMovingState()
    {
        if (droneController == null) return;

        if (droneController.hasArrived)
        {
            if (droneData != null && droneData.HasPendingTasks())
            {
                droneData.MoveToNextTask();
                TaskPoint nextTask = droneData.GetCurrentTask();
                if (nextTask != null)
                {
                    droneController.SetTarget(nextTask.transform);
                    droneController.hasArrived = false;
                }
                else
                {
                    ChangeState(DroneState.Finished);
                }
            }
            else
            {
                ChangeState(DroneState.Finished);
            }
        }
    }

    private void UpdateWaitingState()
    {
        waitTimer += Time.deltaTime;
        if (waitTimer >= waitTimeout)
        {
            Debug.LogWarning($"[DroneStateMachine] {droneController?.droneName} 等待超时");
            ChangeState(DroneState.Moving);
        }
    }

    private void UpdateFinishedState()
    {
        // 保持 Finished 状态
    }

    /// <summary>
    /// 设置等待状态
    /// </summary>
    public void SetWaiting(string reason = "")
    {
        if (currentState != DroneState.Moving) return;
        if (droneData != null) droneData.waitReason = reason;
        ChangeState(DroneState.Waiting);
    }

    /// <summary>
    /// 解除等待
    /// </summary>
    public void ResumeMoving()
    {
        if (currentState != DroneState.Waiting) return;
        if (droneData != null) droneData.waitReason = "";
        ChangeState(DroneState.Moving);
    }

    /// <summary>
    /// 重置状态机
    /// </summary>
    public void Reset()
    {
        if (droneData != null)
        {
            droneData.currentTaskIndex = 0;
            droneData.completedTasks = 0;
            droneData.waitReason = "";
        }
        if (droneController != null)
        {
            droneController.hasArrived = false;
            droneController.targetPoint = null;
        }
        ChangeState(DroneState.Idle);
    }

    /// <summary>
    /// 是否可以接受新任务
    /// </summary>
    public bool CanAcceptTask()
    {
        return currentState == DroneState.Idle || currentState == DroneState.Finished;
    }
}
```

---

## 4. 每个脚本的职责

| 脚本 | 职责 |
|---|---|
| `DroneState` | 枚举定义：Idle/Moving/Waiting/Finished |
| `DroneData` | 数据容器：保存状态、任务队列、统计信息 |
| `DroneStateMachine` | 状态机：处理状态切换、事件回调、等待超时 |
| `DroneController` | 飞行控制：根据状态机指令移动无人机 |
| `DroneManager` | 管理器：生成无人机时自动创建并关联状态机 |

---

## 5. 与已有系统协作

### 协作关系

```
SimulationManager
       │
       ▼
  检查 SimulationState
       │
       ▼
DroneController.Update()
       │
       ▼
DroneStateMachine.Update()
       │
       ▼
  处理 Idle/Moving/Waiting/Finished
```

### SimulationManager 协作

`DroneController.Update()` 会先检查 `SimulationManager.Instance.currentState`：

```csharp
void Update()
{
    // 只有 SimulationState.Running 时才执行
    if (SimulationManager.Instance != null &&
        SimulationManager.Instance.currentState != SimulationState.Running)
        return;

    // 状态机处理...
}
```

### DroneManager 自动关联

生成无人机时自动创建状态机：

```csharp
// DroneManager.SpawnDrones() 中
DroneStateMachine sm = go.AddComponent<DroneStateMachine>();
sm.droneController = drone;
sm.droneData = data;
drone.stateMachine = sm;
```

---

## 6. 使用示例

### 手动让无人机进入等待

```csharp
DroneStateMachine sm = DroneManager.Instance.GetDroneStateMachine(1);
sm.SetWaiting("前方有障碍物");
```

### 解除等待

```csharp
sm.ResumeMoving();
```

### 查询状态

```csharp
DroneData data = DroneManager.Instance.GetDroneData(1);
Debug.Log($"无人机状态: {data.state}"); // Idle/Moving/Waiting/Finished
```

### 监听状态变化

```csharp
DroneStateMachine sm = DroneManager.Instance.GetDroneStateMachine(1);
sm.OnStateEnter += state => Debug.Log($"进入状态: {state}");
sm.OnStateExit += state => Debug.Log($"退出状态: {state}");
```

---

## 7. 后续扩展

当前实现是最小可运行版本，后续可扩展：

1. **冲突检测**：在 `UpdateWaitingState()` 中检测其他无人机位置
2. **优先级调度**：Waiting 时根据优先级决定何时恢复
3. **状态可视化**：根据状态改变无人机颜色或显示图标
4. **路径规划**：Moving 前计算最优路径避免碰撞
