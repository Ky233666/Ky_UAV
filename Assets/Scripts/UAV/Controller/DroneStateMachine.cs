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
    [Tooltip("当前状态")]
    public DroneState currentState = DroneState.Idle;

    [Header("等待设置")]
    [Tooltip("等待超时时间（秒）")]
    public float waitTimeout = 10f;

    /// <summary>
    /// 状态进入事件
    /// </summary>
    public event Action<DroneState> OnStateEnter;

    /// <summary>
    /// 状态退出事件
    /// </summary>
    public event Action<DroneState> OnStateExit;

    /// <summary>
    /// 状态更新事件
    /// </summary>
    public event Action<DroneState> OnStateUpdate;

    // 等待计时器
    private float waitTimer = 0f;

    // 上一帧状态
    private DroneState previousState;

    void Awake()
    {
        // 尝试自动获取组件
        if (droneController == null)
            droneController = GetComponent<DroneController>();

        if (droneData == null)
            droneData = new DroneData();
    }

    void Start()
    {
        // 初始状态
        ChangeState(DroneState.Idle);
    }

    void Update()
    {
        // 检查仿真状态
        if (SimulationManager.Instance == null)
            return;

        if (SimulationManager.Instance.currentState != SimulationState.Running)
            return;

        if (droneData == null || !droneData.isOnline)
            return;

        // 触发状态更新事件
        OnStateUpdate?.Invoke(currentState);

        // 状态机逻辑
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

    #region 状态切换

    /// <summary>
    /// 切换状态
    /// </summary>
    public void ChangeState(DroneState newState)
    {
        if (currentState == newState)
            return;

        // 退出旧状态
        OnStateExit?.Invoke(currentState);

        // 记录上一状态
        previousState = currentState;
        currentState = newState;

        // 更新 DroneData
        if (droneData != null)
        {
            droneData.state = newState;
        }

        // 进入新状态
        OnStateEnter?.Invoke(currentState);

        // 处理进入逻辑
        HandleStateEnter(newState);

        Debug.Log($"[DroneStateMachine] {droneController?.droneName} 状态: {previousState} -> {currentState}");
    }

    /// <summary>
    /// 处理状态进入逻辑
    /// </summary>
    private void HandleStateEnter(DroneState state)
    {
        switch (state)
        {
            case DroneState.Idle:
                waitTimer = 0f;
                break;

            case DroneState.Moving:
                waitTimer = 0f;
                // 从任务队列获取第一个目标并设置
                if (droneData != null && droneData.HasPendingTasks())
                {
                    TryPlanAndPrepareCurrentTaskPath();
                }
                break;

            case DroneState.Waiting:
                waitTimer = 0f;
                break;

            case DroneState.Finished:
                waitTimer = 0f;
                break;
        }
    }

    #endregion

    #region 状态更新逻辑

    /// <summary>
    /// 更新空闲状态
    /// </summary>
    private void UpdateIdleState()
    {
        // 如果有待执行任务，切换到 Moving
        if (droneData != null && droneData.HasPendingTasks())
        {
            ChangeState(DroneState.Moving);
        }
        // 兼容旧场景：没有任务队列，但有直接设置的目标
        else if (droneController != null && droneController.targetPoint != null && !droneController.hasArrived)
        {
            ChangeState(DroneState.Moving);
        }
    }

    /// <summary>
    /// 更新移动状态
    /// </summary>
    private void UpdateMovingState()
    {
        if (droneController == null)
            return;

        if (!TryGetActiveTargetPosition(out Vector3 targetPos))
            return;

        // 计算方向和距离
        Vector3 direction = targetPos - droneController.transform.position;
        float distance = direction.magnitude;

        // 到达判定
        if (distance <= droneController.arriveDistance)
        {
            if (TryAdvanceToNextWaypoint())
            {
                droneController.hasArrived = false;
            }
            else
            {
                droneController.hasArrived = true;

                // 标记任务完成
                if (droneData != null && droneData.HasPendingTasks())
                {
                    droneData.MoveToNextTask();

                    // 如果还有任务，继续飞向下一个
                    if (droneData.HasPendingTasks())
                    {
                        TryPlanAndPrepareCurrentTaskPath();
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
            return;
        }

        // 只移动位置，不修改旋转（避免任何翻滚）
        direction.Normalize();
        droneController.transform.position += direction * droneController.speed * Time.deltaTime;
    }

    /// <summary>
    /// 更新等待状态
    /// </summary>
    private void UpdateWaitingState()
    {
        waitTimer += Time.deltaTime;

        // 检查超时
        if (waitTimer >= waitTimeout)
        {
            Debug.LogWarning($"[DroneStateMachine] {droneController?.droneName} 等待超时，强制继续");
            // 超时后可以强制继续，或者切换到其他状态
            ChangeState(DroneState.Moving);
            return;
        }

        // TODO: 这里可以添加冲突检测逻辑
        // 例如：检测前方是否有其他无人机，是否有障碍物等
        // 如果冲突解除，切换回 Moving

        // 示例：手动解除等待（通过外部调用）
    }

    /// <summary>
    /// 更新完成状态
    /// </summary>
    private void UpdateFinishedState()
    {
        // 保持在 Finished 状态
        // TODO: 可以添加重置逻辑，允许重新开始任务
    }

    #endregion

    #region 公共接口

    /// <summary>
    /// 设置为等待状态
    /// </summary>
    public void SetWaiting(string reason = "")
    {
        if (currentState != DroneState.Moving)
            return;

        if (droneData != null)
        {
            droneData.waitReason = reason;
        }

        ChangeState(DroneState.Waiting);
    }

    /// <summary>
    /// 解除等待，继续移动
    /// </summary>
    public void ResumeMoving()
    {
        if (currentState != DroneState.Waiting)
            return;

        if (droneData != null)
        {
            droneData.waitReason = "";
        }

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
            droneData.totalFlightDistance = 0f;
            droneData.waitReason = "";
            droneData.plannedPath.Clear();
            droneData.currentWaypointIndex = 0;
            droneData.currentPlannerName = "";
        }

        if (droneController != null)
        {
            droneController.hasArrived = false;
            droneController.ClearTarget();
        }

        ChangeState(DroneState.Idle);
    }

    /// <summary>
    /// 获取当前状态名称
    /// </summary>
    public string GetStateName()
    {
        return currentState.ToString();
    }

    /// <summary>
    /// 是否可以接受新任务
    /// </summary>
    public bool CanAcceptTask()
    {
        return currentState == DroneState.Idle || currentState == DroneState.Finished;
    }

    private void TryPlanAndPrepareCurrentTaskPath()
    {
        if (droneData == null || droneController == null)
        {
            return;
        }

        TaskPoint currentTask = droneData.GetCurrentTask();
        if (currentTask == null)
        {
            return;
        }

        if (DroneManager.Instance != null)
        {
            PathPlanningResult pathResult = DroneManager.Instance.PlanPathForTask(droneData.droneId, currentTask);
            if (pathResult.HasPath())
            {
                droneData.currentWaypointIndex = pathResult.waypoints.Count > 1 ? 1 : 0;
                droneController.SetTargetPosition(pathResult.waypoints[droneData.currentWaypointIndex]);
                droneController.hasArrived = false;
                return;
            }
        }

        droneData.plannedPath.Clear();
        droneData.currentWaypointIndex = 0;
        droneController.SetTarget(currentTask.transform);
        droneController.hasArrived = false;
    }

    private bool TryGetActiveTargetPosition(out Vector3 targetPosition)
    {
        if (droneData != null &&
            droneData.plannedPath != null &&
            droneData.plannedPath.Count > 0 &&
            droneData.currentWaypointIndex >= 0 &&
            droneData.currentWaypointIndex < droneData.plannedPath.Count)
        {
            targetPosition = droneData.plannedPath[droneData.currentWaypointIndex];
            return true;
        }

        return droneController.TryGetCurrentTargetPosition(out targetPosition);
    }

    private bool TryAdvanceToNextWaypoint()
    {
        if (droneData == null || droneData.plannedPath == null || droneData.plannedPath.Count == 0)
        {
            return false;
        }

        if (droneData.currentWaypointIndex >= droneData.plannedPath.Count - 1)
        {
            return false;
        }

        droneData.currentWaypointIndex++;
        droneController.SetTargetPosition(droneData.plannedPath[droneData.currentWaypointIndex]);
        return true;
    }

    #endregion
}
