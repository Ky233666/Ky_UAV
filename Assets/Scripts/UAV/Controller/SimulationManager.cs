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

    [Header("结果导出")]
    [Tooltip("实验结果导出器")]
    public SimulationResultExporter resultExporter;

    [Header("批量实验")]
    [Tooltip("批量实验执行器")]
    public BatchExperimentRunner batchExperimentRunner;

    [Header("算法过程可视化")]
    [Tooltip("算法过程可视化管理器")]
    public AlgorithmVisualizerManager algorithmVisualizerManager;

    [Header("调度结果可视化")]
    [Tooltip("任务队列可视化管理器")]
    public TaskQueueVisualizer taskQueueVisualizer;

    [Tooltip("路径规划地图边界与占用格子预览器")]
    public PlanningMapVisualizer planningMapVisualizer;

    [Header("强化学习路径规划")]
    [Tooltip("Q-learning 离线路径规划地图导出器")]
    public RLMapExporter rlMapExporter;

    [Tooltip("Q-learning 离线路径结果读取器")]
    public RLPathResultImporter rlPathResultImporter;

    [Tooltip("Q-learning 训练地图边界显示器")]
    public RLTrainingMapBoundsVisualizer rlTrainingMapBoundsVisualizer;

    [Tooltip("Q-learning 训练场景运行时初始化器")]
    public RLTrainingSceneBootstrap rlTrainingSceneBootstrap;

    [Tooltip("Q-learning Python 后台训练执行器")]
    public RLQlearningTrainingRunner rlTrainingRunner;

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

    /// <summary>
    /// 当前这轮仿真的累计运行时长（秒），暂停时不会继续累加。
    /// </summary>
    public float ElapsedSimulationTime => elapsedSimulationTime;

    private float elapsedSimulationTime;

    void Awake()
    {
        // 单例模式：确保只有一个实例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        RuntimeSceneRegistry.Register(this);
        SimulationContext.GetOrCreate(this);
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

    void Update()
    {
        if (currentState == SimulationState.Running)
        {
            elapsedSimulationTime += Time.deltaTime;
        }
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
        SimulationContext context = SimulationContext.GetOrCreate(this);
        TaskPoint[] allTasks = context.GetTaskPoints();
        if (allTasks.Length > 0 || !autoImportTasksWhenMissing)
        {
            return allTasks;
        }

        TaskPointImporter importer = RuntimeSceneRegistry.Get<TaskPointImporter>(this);
        if (importer == null)
        {
            Debug.LogWarning("[SimulationManager] 未找到 TaskPointImporter，无法自动导入默认任务点");
            return allTasks;
        }

        importer.ImportFromResources();
        allTasks = context.GetTaskPoints();

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
        if (currentState == SimulationState.Running)
        {
            return;
        }

        if (currentState == SimulationState.Paused)
        {
            SetState(SimulationState.Running);
            return;
        }

        if (currentState != SimulationState.Idle)
        {
            OnResetClicked();
        }

        ResetAllTaskPointsInScene();
        elapsedSimulationTime = 0f;
        if (algorithmVisualizerManager != null)
        {
            algorithmVisualizerManager.ClearAllTraces();
        }

        // 如果有 DroneManager，给无人机分配任务点
        if (droneManager != null)
        {
            var allTasks = GetAvailableTasks();
            if (allTasks.Length > 0)
            {
                SchedulingResult schedulingResult = droneManager.AutoAssignTasks(allTasks);
                Debug.Log($"[SimulationManager] 已分配 {allTasks.Length} 个任务点给无人机");
                if (!schedulingResult.success)
                {
                    ShowStatusMessage($"状态：{schedulingResult.message}");
                    SetState(SimulationState.Idle);
                    return;
                }
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
            TaskPoint[] availableTasks = SimulationContext.GetOrCreate(this).GetTaskPoints();
            var firstTask = availableTasks.Length > 0 ? availableTasks[0] : null;
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

        if (resultExporter != null)
        {
            resultExporter.BeginRun();
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

        ResetAllTaskPointsInScene();
        elapsedSimulationTime = 0f;
        if (algorithmVisualizerManager != null)
        {
            algorithmVisualizerManager.ClearAllTraces();
        }
        if (resultExporter != null)
        {
            resultExporter.ResetRunTracking();
        }
        SetState(SimulationState.Idle);
    }

    private void EnsureRuntimeControlPanel()
    {
        SimulationRuntimeControlPanel runtimeControlPanel = RuntimeSceneRegistry.Get<SimulationRuntimeControlPanel>(this);
        if (runtimeControlPanel == null)
        {
            runtimeControlPanel = gameObject.AddComponent<SimulationRuntimeControlPanel>();
        }
        RuntimeSceneRegistry.Register(runtimeControlPanel);

        DroneSpawnPointUIManager spawnPointManager = RuntimeSceneRegistry.Get<DroneSpawnPointUIManager>(this);
        if (spawnPointManager == null)
        {
            spawnPointManager = gameObject.AddComponent<DroneSpawnPointUIManager>();
        }
        RuntimeSceneRegistry.Register(spawnPointManager);

        RuntimeObstacleEditor obstacleEditor = RuntimeSceneRegistry.Get<RuntimeObstacleEditor>(this);
        if (obstacleEditor == null)
        {
            obstacleEditor = gameObject.AddComponent<RuntimeObstacleEditor>();
        }
        RuntimeSceneRegistry.Register(obstacleEditor);

        if (taskQueueVisualizer == null)
        {
            taskQueueVisualizer = GetComponent<TaskQueueVisualizer>();
            if (taskQueueVisualizer == null)
            {
                taskQueueVisualizer = gameObject.AddComponent<TaskQueueVisualizer>();
            }
        }
        RuntimeSceneRegistry.Register(taskQueueVisualizer);

        if (algorithmVisualizerManager == null)
        {
            algorithmVisualizerManager = GetComponent<AlgorithmVisualizerManager>();
            if (algorithmVisualizerManager == null)
            {
                algorithmVisualizerManager = gameObject.AddComponent<AlgorithmVisualizerManager>();
            }
        }
        RuntimeSceneRegistry.Register(algorithmVisualizerManager);

        if (planningMapVisualizer == null)
        {
            planningMapVisualizer = GetComponent<PlanningMapVisualizer>();
            if (planningMapVisualizer == null)
            {
                planningMapVisualizer = gameObject.AddComponent<PlanningMapVisualizer>();
            }
        }
        RuntimeSceneRegistry.Register(planningMapVisualizer);
        if (planningMapVisualizer.droneManager == null)
        {
            planningMapVisualizer.droneManager = droneManager;
        }

        runtimeControlPanel.simulationManager = this;
        if (runtimeControlPanel.droneManager == null)
        {
            runtimeControlPanel.droneManager = droneManager;
        }
        runtimeControlPanel.spawnPointManager = spawnPointManager;
        runtimeControlPanel.obstacleEditor = obstacleEditor;
        runtimeControlPanel.algorithmVisualizerManager = algorithmVisualizerManager;
        runtimeControlPanel.taskQueueVisualizer = taskQueueVisualizer;
        runtimeControlPanel.planningMapVisualizer = planningMapVisualizer;

        if (resultExporter == null)
        {
            resultExporter = GetComponent<SimulationResultExporter>();
            if (resultExporter == null)
            {
                resultExporter = gameObject.AddComponent<SimulationResultExporter>();
            }
        }
        RuntimeSceneRegistry.Register(resultExporter);

        resultExporter.simulationManager = this;
        if (resultExporter.droneManager == null)
        {
            resultExporter.droneManager = droneManager;
        }

        if (batchExperimentRunner == null)
        {
            batchExperimentRunner = GetComponent<BatchExperimentRunner>();
            if (batchExperimentRunner == null)
            {
                batchExperimentRunner = gameObject.AddComponent<BatchExperimentRunner>();
            }
        }
        RuntimeSceneRegistry.Register(batchExperimentRunner);

        batchExperimentRunner.simulationManager = this;
        batchExperimentRunner.resultExporter = resultExporter;

        if (rlMapExporter == null)
        {
            rlMapExporter = GetComponent<RLMapExporter>();
            if (rlMapExporter == null)
            {
                rlMapExporter = gameObject.AddComponent<RLMapExporter>();
            }
        }
        RuntimeSceneRegistry.Register(rlMapExporter);
        if (rlMapExporter.droneManager == null)
        {
            rlMapExporter.droneManager = droneManager;
        }

        if (rlPathResultImporter == null)
        {
            rlPathResultImporter = GetComponent<RLPathResultImporter>();
            if (rlPathResultImporter == null)
            {
                rlPathResultImporter = gameObject.AddComponent<RLPathResultImporter>();
            }
        }
        RuntimeSceneRegistry.Register(rlPathResultImporter);
        if (rlPathResultImporter.droneManager == null)
        {
            rlPathResultImporter.droneManager = droneManager;
        }

        if (rlTrainingMapBoundsVisualizer == null)
        {
            rlTrainingMapBoundsVisualizer = GetComponent<RLTrainingMapBoundsVisualizer>();
            if (rlTrainingMapBoundsVisualizer == null)
            {
                rlTrainingMapBoundsVisualizer = gameObject.AddComponent<RLTrainingMapBoundsVisualizer>();
            }
        }
        RuntimeSceneRegistry.Register(rlTrainingMapBoundsVisualizer);
        if (rlTrainingMapBoundsVisualizer.droneManager == null)
        {
            rlTrainingMapBoundsVisualizer.droneManager = droneManager;
        }

        if (rlTrainingSceneBootstrap == null)
        {
            rlTrainingSceneBootstrap = GetComponent<RLTrainingSceneBootstrap>();
            if (rlTrainingSceneBootstrap == null)
            {
                rlTrainingSceneBootstrap = gameObject.AddComponent<RLTrainingSceneBootstrap>();
            }
        }
        RuntimeSceneRegistry.Register(rlTrainingSceneBootstrap);
        rlTrainingSceneBootstrap.simulationManager = this;
        if (rlTrainingSceneBootstrap.droneManager == null)
        {
            rlTrainingSceneBootstrap.droneManager = droneManager;
        }
        if (rlTrainingSceneBootstrap.cameraManager == null)
        {
            rlTrainingSceneBootstrap.cameraManager = RuntimeSceneRegistry.Get<CameraManager>(this);
        }

        if (rlTrainingRunner == null)
        {
            rlTrainingRunner = GetComponent<RLQlearningTrainingRunner>();
            if (rlTrainingRunner == null)
            {
                rlTrainingRunner = gameObject.AddComponent<RLQlearningTrainingRunner>();
            }
        }
        RuntimeSceneRegistry.Register(rlTrainingRunner);

        runtimeControlPanel.rlMapExporter = rlMapExporter;
        runtimeControlPanel.rlPathResultImporter = rlPathResultImporter;
        runtimeControlPanel.rlTrainingRunner = rlTrainingRunner;

        spawnPointManager.simulationManager = this;
        if (spawnPointManager.droneManager == null)
        {
            spawnPointManager.droneManager = droneManager;
        }

        obstacleEditor.simulationManager = this;
        if (obstacleEditor.droneManager == null)
        {
            obstacleEditor.droneManager = droneManager;
        }
        if (obstacleEditor.cameraManager == null)
        {
            obstacleEditor.cameraManager = RuntimeSceneRegistry.Get<CameraManager>(this);
        }

        runtimeControlPanel.resultExporter = resultExporter;
        runtimeControlPanel.batchExperimentRunner = batchExperimentRunner;

        taskQueueVisualizer.simulationManager = this;
        if (taskQueueVisualizer.droneManager == null)
        {
            taskQueueVisualizer.droneManager = droneManager;
        }
        if (taskQueueVisualizer.cameraManager == null)
        {
            taskQueueVisualizer.cameraManager = RuntimeSceneRegistry.Get<CameraManager>(this);
        }

        algorithmVisualizerManager.simulationManager = this;
        if (algorithmVisualizerManager.droneManager == null)
        {
            algorithmVisualizerManager.droneManager = droneManager;
        }
        if (algorithmVisualizerManager.cameraManager == null)
        {
            algorithmVisualizerManager.cameraManager = RuntimeSceneRegistry.Get<CameraManager>(this);
        }
    }

    private void ResetAllTaskPointsInScene()
    {
        TaskPoint[] allTasks = SimulationContext.GetOrCreate(this).GetTaskPoints();
        foreach (TaskPoint taskPoint in allTasks)
        {
            if (taskPoint != null)
            {
                taskPoint.ResetTask();
            }
        }
    }
}
