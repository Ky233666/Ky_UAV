using UnityEngine;

/// <summary>
/// 任务点状态
/// </summary>
public enum TaskState
{
    Pending,      // 未执行/待执行
    InProgress,   // 执行中
    Completed    // 已完成
}

/// <summary>
/// 巡检任务点
/// </summary>
public class TaskPoint : MonoBehaviour
{
    [Header("任务信息")]
    [Tooltip("任务点 ID（唯一标识）")]
    public int taskId;

    [Tooltip("任务点名称")]
    public string taskName = "巡检点";

    [Tooltip("任务描述")]
    public string description = "";

    [Header("任务设置")]
    [Tooltip("优先级（数值越大越优先）")]
    public int priority = 0;

    [Tooltip("预计耗时（秒）")]
    public float estimatedDuration = 5f;

    [Header("状态")]
    [Tooltip("当前状态")]
    public TaskState currentState = TaskState.Pending;

    [Header("可视化")]
    [Tooltip("未执行时的颜色")]
    public Color pendingColor = Color.yellow;

    [Tooltip("执行中时的颜色")]
    public Color inProgressColor = Color.blue;

    [Tooltip("已完成时的颜色")]
    public Color completedColor = Color.green;

    // 运行时数据
    [HideInInspector]
    public DroneController assignedDrone;

    [HideInInspector]
    public float startTime;

    [HideInInspector]
    public float completionTime;

    private Renderer markerRenderer;

    void Awake()
    {
        var marker = transform.Find("Marker");
        if (marker != null)
            markerRenderer = marker.GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        SimulationContext.GetOrCreate(this).RegisterTaskPoint(this);
    }

    private void OnDestroy()
    {
        SimulationContext context = SimulationContext.Current;
        if (context != null)
        {
            context.UnregisterTaskPoint(this);
        }
    }

    void Start()
    {
        UpdateVisualState();
    }

    /// <summary>
    /// 开始执行任务
    /// </summary>
    public void StartTask(DroneController drone)
    {
        if (currentState != TaskState.Pending) return;

        assignedDrone = drone;
        currentState = TaskState.InProgress;
        startTime = Time.time;

        UpdateVisualState();
        SimulationContext.Current?.NotifyTasksChanged();
        Debug.Log($"[TaskPoint] {taskName} 开始执行，分配给无人机");
    }

    /// <summary>
    /// 完成任务
    /// </summary>
    public void CompleteTask()
    {
        if (currentState != TaskState.InProgress) return;

        currentState = TaskState.Completed;
        completionTime = Time.time - startTime;
        assignedDrone = null;

        UpdateVisualState();
        SimulationContext.Current?.NotifyTasksChanged();
        Debug.Log($"[TaskPoint] {taskName} 已完成，耗时 {completionTime:F1} 秒");
    }

    /// <summary>
    /// 重置任务点
    /// </summary>
    public void ResetTask()
    {
        currentState = TaskState.Pending;
        assignedDrone = null;
        startTime = 0;
        completionTime = 0;

        UpdateVisualState();
        SimulationContext.Current?.NotifyTasksChanged();
    }

    /// <summary>
    /// 更新可视化颜色
    /// </summary>
    private void UpdateVisualState()
    {
        if (markerRenderer == null) return;

        Color targetColor = currentState switch
        {
            TaskState.Pending => pendingColor,
            TaskState.InProgress => inProgressColor,
            TaskState.Completed => completedColor,
            _ => Color.white
        };

        markerRenderer.material.color = targetColor;
    }
}
