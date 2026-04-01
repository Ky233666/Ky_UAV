using UnityEngine;
using TMPro;

/// <summary>
/// 仿真状态管理器（单例模式）
/// </summary>
public class SimulationManager : MonoBehaviour
{
    [Header("状态设置")]
    [Tooltip("当前仿真状态")]
    public SimulationState currentState = SimulationState.Idle;

    [Header("无人机")]
    [Tooltip("无人机控制器（单个，用于兼容旧场景）")]
    public DroneController droneController;

    [Header("无人机管理器")]
    [Tooltip("无人机管理器（多无人机）")]
    public DroneManager droneManager;

    [Header("任务设置")]
    [Tooltip("当场景中没有任务点时，自动尝试从 Resources 导入默认任务点")]
    public bool autoImportTasksWhenMissing = true;

    [Header("UI 组件")]
    [Tooltip("状态文本")]
    public TMP_Text statusText;

    [Header("UI 按钮")]
    [Tooltip("开始按钮")]
    public UnityEngine.UI.Button startButton;
    [Tooltip("暂停按钮")]
    public UnityEngine.UI.Button pauseButton;
    [Tooltip("重置按钮")]
    public UnityEngine.UI.Button resetButton;

    /// <summary>
    /// 单例实例（方便其他脚本访问）
    /// </summary>
    public static SimulationManager Instance { get; private set; }

    void Awake()
    {
        // 单例模式：确保只有一个实例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // 绑定按钮事件
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
        
        if (pauseButton != null)
            pauseButton.onClick.AddListener(OnPauseClicked);
        
        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);

        EnsureRuntimeControlPanel();

        // 初始化状态
        SetState(SimulationState.Idle);
    }

    /// <summary>
    /// 设置仿真状态
    /// </summary>
    public void SetState(SimulationState newState)
    {
        currentState = newState;

        // 根据状态控制无人机
        if (droneController != null)
        {
            switch (currentState)
            {
                case SimulationState.Idle:
                    // 不禁用组件，只让无人机保持静止
                    break;
                case SimulationState.Running:
                    // 确保组件启用
                    droneController.enabled = true;
                    break;
                case SimulationState.Paused:
                    // 不禁用组件，让 Update 中检测状态后停止
                    break;
            }
        }

        // 更新 UI 文本
        UpdateStatusText();
        
        Debug.Log($"[SimulationManager] 状态切换: {newState}");
    }

    /// <summary>
    /// 更新状态文本显示
    /// </summary>
    private void UpdateStatusText()
    {
        if (statusText == null) return;

        string stateName = currentState switch
        {
            SimulationState.Idle => "状态：就绪",
            SimulationState.Running => "状态：运行中",
            SimulationState.Paused => "状态：已暂停",
            _ => "状态：未知"
        };

        statusText.text = stateName;
    }

    private TaskPoint[] GetAvailableTasks()
    {
        TaskPoint[] allTasks = FindObjectsOfType<TaskPoint>();
        if (allTasks.Length > 0 || !autoImportTasksWhenMissing)
        {
            return allTasks;
        }

        TaskPointImporter importer = FindObjectOfType<TaskPointImporter>();
        if (importer == null)
        {
            Debug.LogWarning("[SimulationManager] 未找到 TaskPointImporter，无法自动导入默认任务点");
            return allTasks;
        }

        importer.ImportFromResources();
        allTasks = FindObjectsOfType<TaskPoint>();

        if (allTasks.Length > 0)
        {
            Debug.Log($"[SimulationManager] 自动导入了 {allTasks.Length} 个默认任务点");
        }

        return allTasks;
    }

    private void ShowStatusMessage(string message)
    {
        if (statusText != null && !string.IsNullOrWhiteSpace(message))
        {
            statusText.text = message;
        }
    }

    // ========== 按钮事件处理 ==========

    /// <summary>
    /// 开始按钮点击
    /// </summary>
    public void OnStartClicked()
    {
        // 如果有 DroneManager，给无人机分配任务点
        if (droneManager != null)
        {
            var allTasks = GetAvailableTasks();
            if (allTasks.Length > 0)
            {
                droneManager.AutoAssignTasks(allTasks);
                Debug.Log($"[SimulationManager] 已分配 {allTasks.Length} 个任务点给无人机");
            }
            else
            {
                Debug.LogWarning("[SimulationManager] 场景中没有任务点！");
                ShowStatusMessage("状态：无任务点");
                SetState(SimulationState.Idle);
                return;
            }
        }
        // 兼容旧场景：单个无人机
        else if (droneController != null)
        {
            var firstTask = FindObjectOfType<TaskPoint>();
            if (firstTask != null)
            {
                droneController.SetTarget(firstTask.transform);
                Debug.Log($"[SimulationManager] 无人机已设置目标: {firstTask.name}");
            }
            else
            {
                Debug.LogWarning("[SimulationManager] 场景中没有任务点！");
                ShowStatusMessage("状态：无任务点");
                SetState(SimulationState.Idle);
                return;
            }
        }

        SetState(SimulationState.Running);
    }

    /// <summary>
    /// 暂停按钮点击
    /// </summary>
    public void OnPauseClicked()
    {
        SetState(SimulationState.Paused);
    }

    /// <summary>
    /// 重置按钮点击
    /// </summary>
    public void OnResetClicked()
    {
        // 优先使用 DroneManager 重置所有无人机
        if (droneManager != null)
        {
            droneManager.ResetAllDrones();
        }
        // 兼容旧场景：单个无人机
        else if (droneController != null)
        {
            droneController.Reset();
        }

        SetState(SimulationState.Idle);
    }

    private void EnsureRuntimeControlPanel()
    {
        SimulationRuntimeControlPanel runtimeControlPanel = FindObjectOfType<SimulationRuntimeControlPanel>();
        if (runtimeControlPanel == null)
        {
            runtimeControlPanel = gameObject.AddComponent<SimulationRuntimeControlPanel>();
        }

        runtimeControlPanel.simulationManager = this;
        if (runtimeControlPanel.droneManager == null)
        {
            runtimeControlPanel.droneManager = droneManager;
        }
    }
}
