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

    [Header("简单冲突规避")]
    [Tooltip("是否启用近距离让行。")]
    public bool enableLocalAvoidance = true;

    [Tooltip("当两机距离小于该值时，开始判断让行。")]
    public float avoidanceTriggerDistance = 2.2f;

    [Tooltip("当两机目标点接近到该范围内，也认为存在潜在冲突。")]
    public float sharedTargetConflictDistance = 2.5f;

    [Tooltip("判断其他无人机是否位于前方冲突区域的点积阈值。")]
    [Range(-1f, 1f)]
    public float forwardConflictDotThreshold = 0.15f;

    [Tooltip("无人机之间希望保持的最小安全间距。")]
    public float minimumSeparationDistance = 1.35f;

    [Tooltip("等待恢复前至少拉开的距离。")]
    public float avoidanceResumeDistance = 1.8f;

    [Tooltip("等待时用于轻微后退拉开距离的速度。")]
    public float avoidanceRetreatSpeed = 1.8f;

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

    // 当前冲突片段标识，用于避免同一轮持续阻塞重复计数
    private string activeConflictKey = string.Empty;

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

        Vector3 frameStartPosition = droneController != null
            ? droneController.transform.position
            : transform.position;

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

        UpdateRuntimeStats(frameStartPosition);
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
                ClearActiveConflict();
                break;

            case DroneState.Moving:
                waitTimer = 0f;
                // 从任务队列获取第一个目标并设置
                if (droneData != null && droneData.HasPendingTasks())
                {
                    StartCurrentTaskIfNeeded();
                    TryPlanAndPrepareCurrentTaskPath();
                }
                break;

            case DroneState.Waiting:
                waitTimer = 0f;
                break;

            case DroneState.Finished:
                waitTimer = 0f;
                ClearActiveConflict();
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
                    CompleteCurrentTaskIfNeeded();
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

        if (TryFindAvoidanceBlocker(targetPos, out DroneController blocker, out string waitReason))
        {
            RegisterConflict(
                blocker != null ? $"avoidance:{blocker.droneId}" : "avoidance:unknown",
                waitReason);
            SetWaiting(waitReason);
            return;
        }

        // 只移动位置，不修改旋转（避免任何翻滚）
        direction.Normalize();
        float desiredStep = droneController.speed * Time.deltaTime;
        float safeStep = ComputeSafeMovementStep(direction, desiredStep, out DroneController separationBlocker);
        if (safeStep <= 0.001f)
        {
            string blockerName = separationBlocker != null ? separationBlocker.droneName : "前方无人机";
            string reason = $"前方占用 {blockerName}";
            RegisterConflict(
                separationBlocker != null ? $"occupied:{separationBlocker.droneId}" : "occupied:unknown",
                reason);
            SetWaiting(reason);
            return;
        }

        ClearActiveConflict();

        droneController.transform.position += direction * safeStep;
    }

    /// <summary>
    /// 更新等待状态
    /// </summary>
    private void UpdateWaitingState()
    {
        waitTimer += Time.deltaTime;

        ApplyWaitingRetreatIfNeeded();

        if (!enableLocalAvoidance)
        {
            ResumeMoving();
            return;
        }

        if (!TryGetActiveTargetPosition(out Vector3 targetPos))
        {
            ResumeMoving();
            return;
        }

        if (!TryFindAvoidanceBlocker(targetPos, out _, out _))
        {
            if (!TryFindNearestDroneWithinDistance(minimumSeparationDistance, out _))
            {
                ClearActiveConflict();
                ResumeMoving();
                return;
            }
        }

        // 检查超时
        if (waitTimer >= waitTimeout)
        {
            Debug.LogWarning($"[DroneStateMachine] {droneController?.droneName} 等待超时，强制继续");
            // 超时后可以强制继续，或者切换到其他状态
            ClearActiveConflict();
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
            droneData.waitCount++;
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

        ClearActiveConflict();
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
            droneData.waitCount = 0;
            droneData.conflictCount = 0;
            droneData.lastConflictReason = "";
            droneData.plannedPath.Clear();
            droneData.currentWaypointIndex = 0;
            droneData.currentPlannerName = "";
        }

        if (droneController != null)
        {
            droneController.hasArrived = false;
            droneController.ClearTarget();
        }

        ClearActiveConflict();
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

    private void StartCurrentTaskIfNeeded()
    {
        if (droneData == null || droneController == null)
        {
            return;
        }

        TaskPoint currentTask = droneData.GetCurrentTask();
        if (currentTask != null && currentTask.currentState == TaskState.Pending)
        {
            currentTask.StartTask(droneController);
        }
    }

    private void CompleteCurrentTaskIfNeeded()
    {
        if (droneData == null)
        {
            return;
        }

        TaskPoint currentTask = droneData.GetCurrentTask();
        if (currentTask != null)
        {
            currentTask.CompleteTask();
        }
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

    private void UpdateRuntimeStats(Vector3 frameStartPosition)
    {
        if (droneData == null || droneController == null)
        {
            return;
        }

        float deltaDistance = Vector3.Distance(frameStartPosition, droneController.transform.position);
        if (deltaDistance > 0.0001f)
        {
            droneData.totalFlightDistance += deltaDistance;
        }

        droneData.lastKnownPosition = droneController.transform.position;
    }

    private float ComputeSafeMovementStep(
        Vector3 direction,
        float desiredStep,
        out DroneController blockingDrone)
    {
        blockingDrone = null;

        if (droneController == null || DroneManager.Instance == null)
        {
            return desiredStep;
        }

        float safeStep = desiredStep;
        Vector3 myPosition = droneController.transform.position;

        foreach (DroneController otherDrone in DroneManager.Instance.drones)
        {
            if (!IsCandidateDroneForAvoidance(otherDrone))
            {
                continue;
            }

            Vector3 toOther = otherDrone.transform.position - myPosition;
            float projectedDistance = Vector3.Dot(direction, toOther);
            if (projectedDistance <= 0f)
            {
                continue;
            }

            Vector3 closestOffset = toOther - direction * projectedDistance;
            float lateralDistance = closestOffset.magnitude;
            if (lateralDistance >= minimumSeparationDistance)
            {
                continue;
            }

            float separationAlongPath = Mathf.Sqrt(
                Mathf.Max(0f, minimumSeparationDistance * minimumSeparationDistance - lateralDistance * lateralDistance));
            float maxAllowedStep = projectedDistance - separationAlongPath;

            if (maxAllowedStep < safeStep)
            {
                safeStep = Mathf.Max(0f, maxAllowedStep);
                blockingDrone = otherDrone;
            }
        }

        return safeStep;
    }

    private void ApplyWaitingRetreatIfNeeded()
    {
        if (droneController == null || !enableLocalAvoidance)
        {
            return;
        }

        if (!TryFindNearestDroneWithinDistance(avoidanceResumeDistance, out DroneController nearbyDrone))
        {
            return;
        }

        Vector3 retreatDirection = droneController.transform.position - nearbyDrone.transform.position;
        if (retreatDirection.sqrMagnitude <= 0.0001f)
        {
            retreatDirection = Vector3.right;
        }

        retreatDirection.Normalize();
        droneController.transform.position += retreatDirection * avoidanceRetreatSpeed * Time.deltaTime;
    }

    private bool TryFindNearestDroneWithinDistance(float distanceThreshold, out DroneController nearbyDrone)
    {
        nearbyDrone = null;

        if (droneController == null || DroneManager.Instance == null)
        {
            return false;
        }

        float bestDistance = float.MaxValue;
        Vector3 myPosition = droneController.transform.position;

        foreach (DroneController otherDrone in DroneManager.Instance.drones)
        {
            if (!IsCandidateDroneForAvoidance(otherDrone))
            {
                continue;
            }

            float distance = Vector3.Distance(myPosition, otherDrone.transform.position);
            if (distance > distanceThreshold || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            nearbyDrone = otherDrone;
        }

        return nearbyDrone != null;
    }

    private bool TryFindAvoidanceBlocker(
        Vector3 myTargetPosition,
        out DroneController blocker,
        out string waitReason)
    {
        blocker = null;
        waitReason = string.Empty;

        if (!enableLocalAvoidance || droneController == null || DroneManager.Instance == null)
        {
            return false;
        }

        Vector3 myPosition = droneController.transform.position;
        Vector3 myDirection = myTargetPosition - myPosition;
        if (myDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        myDirection.Normalize();

        foreach (DroneController otherDrone in DroneManager.Instance.drones)
        {
            if (!IsCandidateDroneForAvoidance(otherDrone))
            {
                continue;
            }

            DroneStateMachine otherStateMachine = otherDrone.stateMachine;
            if (otherStateMachine == null)
            {
                continue;
            }

            if (!ShouldYieldTo(otherStateMachine))
            {
                continue;
            }

            Vector3 otherPosition = otherDrone.transform.position;
            float separation = Vector3.Distance(myPosition, otherPosition);
            if (separation > avoidanceTriggerDistance)
            {
                continue;
            }

            Vector3 toOther = otherPosition - myPosition;
            Vector3 toSelf = myPosition - otherPosition;
            Vector3 otherDirection = GetMovementDirection(otherDrone);

            bool otherInFront = toOther.sqrMagnitude > 0.0001f &&
                Vector3.Dot(myDirection, toOther.normalized) >= forwardConflictDotThreshold;
            bool selfInFrontOfOther = toSelf.sqrMagnitude > 0.0001f &&
                Vector3.Dot(otherDirection, toSelf.normalized) >= forwardConflictDotThreshold;
            bool sharedTargetConflict =
                TryGetOtherTargetPosition(otherDrone, out Vector3 otherTargetPosition) &&
                Vector3.Distance(myTargetPosition, otherTargetPosition) <= sharedTargetConflictDistance;
            bool tooClose = separation <= minimumSeparationDistance;

            if (!otherInFront && !selfInFrontOfOther && !sharedTargetConflict && !tooClose)
            {
                continue;
            }

            blocker = otherDrone;
            waitReason = $"避让 {otherDrone.droneName}";
            return true;
        }

        return false;
    }

    private bool IsCandidateDroneForAvoidance(DroneController otherDrone)
    {
        if (otherDrone == null || otherDrone == droneController)
        {
            return false;
        }

        DroneStateMachine otherStateMachine = otherDrone.stateMachine;
        if (otherStateMachine == null || otherStateMachine.droneData == null)
        {
            return false;
        }

        if (!otherStateMachine.droneData.isOnline)
        {
            return false;
        }

        return otherStateMachine.currentState == DroneState.Moving ||
               otherStateMachine.currentState == DroneState.Waiting;
    }

    private bool ShouldYieldTo(DroneStateMachine otherStateMachine)
    {
        if (otherStateMachine == null || otherStateMachine.droneController == null || droneController == null)
        {
            return false;
        }

        if (otherStateMachine.droneController.droneId == droneController.droneId)
        {
            return false;
        }

        // 先用编号稳定打破对称，避免双向同时等待。
        return droneController.droneId > otherStateMachine.droneController.droneId;
    }

    private Vector3 GetMovementDirection(DroneController controller)
    {
        if (controller == null)
        {
            return Vector3.zero;
        }

        if (!controller.TryGetCurrentTargetPosition(out Vector3 targetPosition))
        {
            return Vector3.zero;
        }

        Vector3 direction = targetPosition - controller.transform.position;
        return direction.sqrMagnitude <= 0.0001f ? Vector3.zero : direction.normalized;
    }

    private bool TryGetOtherTargetPosition(DroneController controller, out Vector3 targetPosition)
    {
        targetPosition = Vector3.zero;
        if (controller == null)
        {
            return false;
        }

        return controller.TryGetCurrentTargetPosition(out targetPosition);
    }

    private void RegisterConflict(string conflictKey, string reason)
    {
        if (droneData == null)
        {
            return;
        }

        string normalizedKey = string.IsNullOrWhiteSpace(conflictKey) ? "conflict:unknown" : conflictKey;
        if (string.Equals(activeConflictKey, normalizedKey, StringComparison.Ordinal))
        {
            return;
        }

        activeConflictKey = normalizedKey;
        droneData.conflictCount++;
        droneData.lastConflictReason = reason ?? string.Empty;
    }

    private void ClearActiveConflict()
    {
        activeConflictKey = string.Empty;
    }

    #endregion
}
