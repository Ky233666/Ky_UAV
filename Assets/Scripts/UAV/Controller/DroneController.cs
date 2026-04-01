using UnityEngine;

/// <summary>
/// 无人机飞控：自动飞向目标点
/// </summary>
public class DroneController : MonoBehaviour
{
    [Header("无人机信息")]
    [Tooltip("无人机唯一编号")]
    public int droneId;

    [Tooltip("无人机名称")]
    public string droneName = "无人机";

    [Header("飞行设置")]
    [Tooltip("飞行速度（米/秒）")]
    public float speed = 5f;

    [Tooltip("到达判定距离（米）")]
    public float arriveDistance = 0.5f;

    [Header("目标点")]
    [Tooltip("目标点对象")]
    public Transform targetPoint;

    [Header("状态")]
    [Tooltip("是否已到达目标")]
    public bool hasArrived = false;

    [Header("状态机")]
    [Tooltip("状态机组件（可选，由 DroneManager 自动关联）")]
    public DroneStateMachine stateMachine;

    // 记录初始位置
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        // 若有 Rigidbody，冻结旋转，避免物理导致翻滚
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.useGravity = false;
        }

        // 尝试获取状态机
        if (stateMachine == null)
            stateMachine = GetComponent<DroneStateMachine>();
    }

    /// <summary>
    /// 初始化无人机信息
    /// </summary>
    public void Initialize(int id, string name)
    {
        droneId = id;
        droneName = name;
        gameObject.name = $"Drone_{id:D2}_{name}";
    }

    /// <summary>
    /// 设置状态机引用
    /// </summary>
    public void SetStateMachine(DroneStateMachine sm)
    {
        stateMachine = sm;
        if (sm != null)
        {
            sm.droneController = this;
        }
    }

    /// <summary>
    /// 设置新的目标点
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        targetPoint = newTarget;
        hasArrived = false;
    }

    /// <summary>
    /// 设置新的目标点（通过位置）
    /// </summary>
    public void SetTargetPosition(Vector3 position)
    {
        GameObject tempTarget = new GameObject("TempTarget");
        tempTarget.transform.position = position;
        SetTarget(tempTarget.transform);
    }

    /// <summary>
    /// 重置无人机到初始位置
    /// </summary>
    public void ResetToInitial()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        hasArrived = false;

        Debug.Log($"[{gameObject.name}] 已重置到初始位置");
    }

    /// <summary>
    /// 重置无人机（包含状态机）
    /// </summary>
    public void Reset()
    {
        ResetToInitial();
        if (stateMachine != null)
        {
            stateMachine.Reset();
        }
    }

    void Update()
    {
        // 检查仿真状态
        if (SimulationManager.Instance == null)
            return;

        if (SimulationManager.Instance.currentState != SimulationState.Running)
            return;

        // 如果有状态机，委托给状态机处理移动逻辑
        if (stateMachine != null)
        {
            // 状态机在 Update 中会处理移动
            // 这里只处理没有状态机时的兼容逻辑
            return;
        }

        // === 以下为无状态机时的兼容逻辑 ===

        // 没有目标点或已到达则不移动
        if (targetPoint == null || hasArrived)
            return;

        // 计算方向和距离
        Vector3 direction = targetPoint.position - transform.position;
        float distance = direction.magnitude;

        // 到达判定
        if (distance <= arriveDistance)
        {
            hasArrived = true;
            Debug.Log($"[{gameObject.name}] 已到达目标点！");
            return;
        }

        // 标准化方向
        direction.Normalize();

        // 只移动位置，不旋转（避免翻滚）
        transform.position += direction * speed * Time.deltaTime;
    }

    void OnDrawGizmosSelected()
    {
        if (targetPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, targetPoint.position);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPoint.position, arriveDistance);
        }
    }
}
