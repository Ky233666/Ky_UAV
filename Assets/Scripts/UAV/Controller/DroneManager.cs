using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 无人机管理器：生成并管理多架无人机
/// </summary>
public class DroneManager : MonoBehaviour
{
    public static DroneManager Instance { get; private set; }

    private bool projectPathsToTopDown;
    private float pathProjectionHeight = 12f;
    private readonly List<Bounds> obstacleFootprintBounds = new List<Bounds>();
    private bool obstacleFootprintCacheInitialized;
    private AlgorithmVisualizerManager algorithmVisualizerManager;
    private SimulationContext simulationContext;

    [Header("无人机 Prefab")]
    [Tooltip("无人机 Prefab")]
    public DroneController dronePrefab;

    [Header("配置")]
    [Tooltip("默认配置资产；为空时将尝试从 Resources/Configs/DroneConfig_Default 加载。")]
    public DroneConfig droneConfig;

    [Tooltip("默认配置的 Resources 路径。")]
    public string droneConfigResourcePath = "Configs/DroneConfig_Default";

    [Header("生成设置")]
    [Tooltip("无人机数量")]
    public int droneCount = 4;

    [Tooltip("生成起始位置")]
    public Vector3 spawnOrigin = new Vector3(0, 0, 0);

    [Tooltip("生成间距")]
    public float spawnSpacing = 3f;

    [Tooltip("排列方向")]
    public Vector3 spawnDirection = new Vector3(1, 0, 0);

    [Tooltip("优先使用场景中的 SpawnPoint 作为起飞位置；不足时再回退到 spawnOrigin 阵列")]
    public bool useSceneSpawnPoints = true;

    [Tooltip("默认巡航高度（世界坐标 Y）。")]
    public float cruiseHeight = 5f;

    [Header("管理")]
    [Tooltip("所有无人机列表")]
    public List<DroneController> drones = new List<DroneController>();

    [Tooltip("无人机数据列表")]
    public List<DroneData> droneDataList = new List<DroneData>();

    [Header("机体分离")]
    [Tooltip("是否在运行时强制维持无人机之间的最小间距，避免模型重叠。")]
    public bool enforceDroneSeparation = true;

    [Tooltip("无人机之间的最小中心距离。")]
    public float minimumDroneSeparation = 1.6f;

    [Tooltip("单机最大任务容量。0 表示不限制。")]
    public int maxTaskCapacity = 0;

    [Tooltip("分离修正强度，越大越快把重叠机体推开。")]
    [Range(0.1f, 1f)]
    public float separationResolveFactor = 0.85f;

    [Header("调度算法")]
    [Tooltip("当前使用的任务调度算法")]
    public SchedulerAlgorithmType schedulerAlgorithm = SchedulerAlgorithmType.EvenSplit;

    [Header("路径规划算法")]
    [Tooltip("当前使用的路径规划算法")]
    public PathPlannerType pathPlannerType = PathPlannerType.StraightLine;

    [Header("路径规划配置")]
    [Tooltip("路径规划网格尺寸")]
    public float planningGridCellSize = 2f;

    [Tooltip("路径规划区域最小边界")]
    public Vector3 planningWorldMin = new Vector3(-20f, 0f, -20f);

    [Tooltip("路径规划区域最大边界")]
    public Vector3 planningWorldMax = new Vector3(80f, 10f, 80f);

    [Tooltip("静态障碍物层，用于 A* 栅格阻挡检测")]
    public LayerMask planningObstacleLayer;

    [Tooltip("是否允许对角线移动")]
    public bool allowDiagonalPlanning = true;

    [Header("障碍自动配置")]
    [Tooltip("运行时自动把场景中的建筑区域纳入路径规划障碍物")]
    public bool autoConfigurePlanningObstacles = true;

    [Tooltip("场景中的障碍物根节点；为空时会自动查找名为 Buildings 的对象")]
    public Transform obstacleRoot;

    [Tooltip("是否把障碍物根节点下的所有子物体递归设置到 Building 层")]
    public bool assignBuildingLayerRecursively = true;

    [Tooltip("如果建筑模型本身没有碰撞体，则自动为每个建筑根节点生成一个包围盒碰撞体")]
    public bool generateObstacleProxyColliders = true;

    [Tooltip("自动生成障碍代理碰撞体时附加的尺寸余量")]
    public Vector3 obstacleColliderPadding = new Vector3(0.8f, 0.5f, 0.8f);

    [Tooltip("自动生成障碍代理碰撞体时的最小尺寸")]
    public Vector3 minimumObstacleColliderSize = new Vector3(2f, 2f, 2f);

    void Awake()
    {
        // 单例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        RuntimeSceneRegistry.Register(this);
    }

    private void OnEnable()
    {
        simulationContext = SimulationContext.GetOrCreate(this);
        simulationContext.ObstaclesChanged += HandleRuntimeObstaclesChanged;
    }

    private void OnDisable()
    {
        if (simulationContext == null)
        {
            return;
        }

        simulationContext.ObstaclesChanged -= HandleRuntimeObstaclesChanged;
        simulationContext = null;
    }

    void Start()
    {
        ApplyConfiguredDefaults();
        RefreshObstacleConfiguration();

        // 自动生成无人机（如果需要）
        if (drones.Count == 0 && dronePrefab != null)
        {
            SpawnDrones(droneCount);
        }
    }

    public DroneConfig ResolveDroneConfig()
    {
        if (droneConfig == null && !string.IsNullOrWhiteSpace(droneConfigResourcePath))
        {
            droneConfig = Resources.Load<DroneConfig>(droneConfigResourcePath);
        }

        return droneConfig;
    }

    public void ApplyConfiguredDefaults()
    {
        DroneConfig config = ResolveDroneConfig();
        if (config == null)
        {
            return;
        }

        cruiseHeight = Mathf.Max(0f, config.cruiseHeight);
        minimumDroneSeparation = Mathf.Max(0.1f, config.minimumSeparation);
        maxTaskCapacity = Mathf.Max(0, config.maxTaskCapacity);

        if (dronePrefab != null)
        {
            dronePrefab.speed = Mathf.Max(0.1f, config.defaultSpeed);
        }

        ApplyDroneSpeedToAll(config.defaultSpeed);
        ApplyConfigToRuntimeObjects(config);
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
        List<Vector3> spawnPositions = BuildSpawnPositions(count);

        for (int i = 0; i < count; i++)
        {
            Vector3 position = AdjustSpawnHeight(spawnPositions[i]);

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
                    lastKnownPosition = ToCruisePosition(drone.transform.position)
                };
                droneDataList.Add(data);

                // 创建并关联状态机
                DroneStateMachine sm = go.GetComponent<DroneStateMachine>();
                if (sm == null)
                    sm = go.AddComponent<DroneStateMachine>();

                sm.droneController = drone;
                sm.droneData = data;
                drone.stateMachine = sm;

                DronePathVisualizer pathVisualizer = go.GetComponent<DronePathVisualizer>();
                if (pathVisualizer == null)
                    pathVisualizer = go.AddComponent<DronePathVisualizer>();

                pathVisualizer.droneController = drone;
                pathVisualizer.droneData = data;

                Debug.Log($"[DroneManager] 无人机 {drone.droneId} 状态机已创建");
            }
        }

        ApplyConfiguredDefaults();
        BindCamerasToManagedDrones();
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
                SnapDroneToCruiseHeight(drone);

                DronePathVisualizer pathVisualizer = drone.GetComponent<DronePathVisualizer>();
                if (pathVisualizer != null)
                {
                    pathVisualizer.ResetVisuals();
                }
            }
        }

        SyncDronePositionsToData();
        Debug.Log("[DroneManager] 所有无人机已重置");
    }

    /// <summary>
    /// Rebuilds the managed drone fleet with a new count.
    /// </summary>
    public void RespawnDrones(int count)
    {
        droneCount = Mathf.Max(1, count);
        SpawnDrones(droneCount);
    }

    /// <summary>
    /// Applies a shared flight speed to the prefab and all active drones.
    /// </summary>
    public void ApplyDroneSpeedToAll(float newSpeed)
    {
        float clampedSpeed = Mathf.Max(0.1f, newSpeed);

        if (dronePrefab != null)
        {
            dronePrefab.speed = clampedSpeed;
        }

        foreach (DroneController drone in drones)
        {
            if (drone != null)
            {
                drone.speed = clampedSpeed;
            }
        }

        foreach (DroneData data in droneDataList)
        {
            if (data != null)
            {
                data.speed = clampedSpeed;
            }
        }
    }

    /// <summary>
    /// Applies shared runtime planning settings.
    /// </summary>
    public void ApplyPlanningSettings(
        float gridCellSize,
        bool allowDiagonal,
        bool autoConfigureObstacles,
        Vector3 worldMin,
        Vector3 worldMax)
    {
        planningGridCellSize = Mathf.Clamp(gridCellSize, 0.5f, 10f);
        allowDiagonalPlanning = allowDiagonal;
        autoConfigurePlanningObstacles = autoConfigureObstacles;
        planningWorldMin = worldMin;
        planningWorldMax = worldMax;

        RefreshObstacleConfiguration();
    }

    public Vector3 ToCruisePosition(Vector3 position)
    {
        return new Vector3(position.x, cruiseHeight, position.z);
    }

    public List<Vector3> NormalizePathToCruiseHeight(List<Vector3> path)
    {
        if (path == null)
        {
            return new List<Vector3>();
        }

        List<Vector3> normalizedPath = new List<Vector3>(path.Count);
        for (int i = 0; i < path.Count; i++)
        {
            normalizedPath.Add(ToCruisePosition(path[i]));
        }

        return normalizedPath;
    }

    public void SnapDroneToCruiseHeight(DroneController drone)
    {
        if (drone == null)
        {
            return;
        }

        drone.transform.position = ToCruisePosition(drone.transform.position);
    }

    void LateUpdate()
    {
        if (!enforceDroneSeparation || SimulationManager.Instance == null)
        {
            return;
        }

        if (SimulationManager.Instance.currentState != SimulationState.Running)
        {
            return;
        }

        ResolveDroneOverlaps();
    }

    /// <summary>
    /// Applies path visibility settings to all active drones.
    /// </summary>
    public void ApplyPathVisibilityToAll(bool showPlannedPath, bool showTrail)
    {
        foreach (DroneController drone in drones)
        {
            if (drone == null)
            {
                continue;
            }

            DronePathVisualizer pathVisualizer = drone.GetComponent<DronePathVisualizer>();
            if (pathVisualizer == null)
            {
                pathVisualizer = drone.gameObject.AddComponent<DronePathVisualizer>();
                pathVisualizer.droneController = drone;
                pathVisualizer.droneData = GetDroneData(drone.droneId);
            }

            pathVisualizer.SetVisibility(showPlannedPath, showTrail);
            pathVisualizer.SetProjectionMode(projectPathsToTopDown, pathProjectionHeight);
        }
    }

    public void SetPathProjectionMode(bool projectToTopDown, float projectionHeight)
    {
        projectPathsToTopDown = projectToTopDown;
        pathProjectionHeight = projectionHeight;

        foreach (DroneController drone in drones)
        {
            if (drone == null)
            {
                continue;
            }

            DronePathVisualizer pathVisualizer = drone.GetComponent<DronePathVisualizer>();
            if (pathVisualizer == null)
            {
                pathVisualizer = drone.gameObject.AddComponent<DronePathVisualizer>();
                pathVisualizer.droneController = drone;
                pathVisualizer.droneData = GetDroneData(drone.droneId);
            }

            pathVisualizer.SetProjectionMode(projectPathsToTopDown, pathProjectionHeight);
        }
    }

    public float CalculatePathProjectionHeight()
    {
        float maxHeight = Mathf.Max(cruiseHeight, planningWorldMax.y);
        Transform root = ResolveObstacleRoot();
        if (root != null)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    maxHeight = Mathf.Max(maxHeight, renderers[i].bounds.max.y);
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    maxHeight = Mathf.Max(maxHeight, colliders[i].bounds.max.y);
                }
            }
        }

        return maxHeight + 0.35f;
    }

    public void RefreshObstacleConfiguration()
    {
        EnsurePlanningObstacleLayerConfigured();

        if (autoConfigurePlanningObstacles)
        {
            ConfigurePlanningObstacles();
        }
        else
        {
            RefreshObstacleFootprintCache();
        }
    }

    private void HandleRuntimeObstaclesChanged()
    {
        obstacleFootprintCacheInitialized = false;
        RefreshObstacleConfiguration();
    }

    public Transform EnsureObstacleRootExists()
    {
        Transform root = ResolveObstacleRoot();
        if (root != null)
        {
            return root;
        }

        GameObject obstacleRootObject = new GameObject("Buildings");
        obstacleRoot = obstacleRootObject.transform;
        Debug.Log("[DroneManager] 已自动创建障碍物根节点 Buildings");
        return obstacleRoot;
    }

    public Transform EnsureRuntimeObstacleContainer()
    {
        Transform root = EnsureObstacleRootExists();
        Transform runtimeObstacleContainer = root.Find("RuntimeObstacles");
        if (runtimeObstacleContainer != null)
        {
            return runtimeObstacleContainer;
        }

        GameObject containerObject = new GameObject("RuntimeObstacles");
        runtimeObstacleContainer = containerObject.transform;
        runtimeObstacleContainer.SetParent(root, false);

        int buildingLayer = LayerMask.NameToLayer("Building");
        if (buildingLayer >= 0)
        {
            containerObject.layer = buildingLayer;
        }

        return runtimeObstacleContainer;
    }

    public bool IsMovementSegmentBlockedByObstacle(
        Vector3 from,
        Vector3 to,
        float horizontalPadding,
        float verticalPadding,
        out RaycastHit hitInfo)
    {
        hitInfo = default;
        EnsurePlanningObstacleLayerConfigured();

        if (planningObstacleLayer.value == 0)
        {
            return false;
        }

        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
        {
            return false;
        }

        Vector3 normalizedDirection = direction / distance;
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.15f, horizontalPadding),
            Mathf.Max(0.15f, verticalPadding),
            Mathf.Max(0.15f, horizontalPadding));

        return Physics.BoxCast(
            from,
            halfExtents,
            normalizedDirection,
            out hitInfo,
            Quaternion.identity,
            distance,
            planningObstacleLayer,
            QueryTriggerInteraction.Ignore);
    }

    public int GetBuildingWarningCount()
    {
        int count = 0;
        foreach (DroneController drone in drones)
        {
            if (drone == null)
            {
                continue;
            }

            DronePathVisualizer pathVisualizer = drone.GetComponent<DronePathVisualizer>();
            if (pathVisualizer != null && pathVisualizer.HasBuildingAlert)
            {
                count++;
            }
        }

        return count;
    }

    public bool IsPointInsideObstacleFootprint(Vector3 worldPoint, float padding = 0.15f)
    {
        EnsureObstacleFootprintCache();

        for (int i = 0; i < obstacleFootprintBounds.Count; i++)
        {
            if (IsPointInsideBoundsXZ(worldPoint, obstacleFootprintBounds[i], padding))
            {
                return true;
            }
        }

        return false;
    }

    public bool DoesSegmentCrossObstacleFootprint(Vector3 startWorldPoint, Vector3 endWorldPoint, float padding = 0.15f)
    {
        EnsureObstacleFootprintCache();

        for (int i = 0; i < obstacleFootprintBounds.Count; i++)
        {
            if (SegmentIntersectsBoundsXZ(startWorldPoint, endWorldPoint, obstacleFootprintBounds[i], padding))
            {
                return true;
            }
        }

        return false;
    }

    public bool DoesPolylineCrossObstacleFootprint(IList<Vector3> points, float padding = 0.15f)
    {
        if (points == null || points.Count == 0)
        {
            return false;
        }

        if (IsPointInsideObstacleFootprint(points[0], padding))
        {
            return true;
        }

        for (int i = 1; i < points.Count; i++)
        {
            if (DoesSegmentCrossObstacleFootprint(points[i - 1], points[i], padding))
            {
                return true;
            }
        }

        return false;
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
            priorityWeight = ResolveDroneConfig() != null ? ResolveDroneConfig().priorityWeight : 6f,
            distanceWeight = ResolveDroneConfig() != null ? ResolveDroneConfig().distanceWeight : 1f,
            loadWeight = ResolveDroneConfig() != null ? ResolveDroneConfig().loadWeight : 4f,
            maxTaskCapacity = maxTaskCapacity
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
            case SchedulerAlgorithmType.PriorityGreedy:
                return new PriorityGreedyScheduler();

            case SchedulerAlgorithmType.GreedyNearest:
                return new GreedyNearestScheduler();

            case SchedulerAlgorithmType.EvenSplit:
            default:
                return new EvenSplitScheduler();
        }
    }

    /// <summary>
    /// 创建当前配置对应的路径规划算法实例。
    /// 当前先接入直线路径，为后续 A* 预留入口。
    /// </summary>
    public IPathPlanner CreatePathPlanner()
    {
        return CreatePathPlanner(pathPlannerType);
    }

    public IPathPlanner CreatePathPlanner(PathPlannerType plannerType)
    {
        switch (plannerType)
        {
            case PathPlannerType.QLearningOffline:
                return new OfflineQlearningPathPlanner();

            case PathPlannerType.RRT:
                return new RRTPlanner();

            case PathPlannerType.AStar:
                return new AStarPlanner();

            case PathPlannerType.StraightLine:
            default:
                return new StraightLinePlanner();
        }
    }

    /// <summary>
    /// 为指定无人机规划到当前任务点的路径，并把结果记录到 DroneData。
    /// </summary>
    public PathPlanningResult PlanPathForTask(int droneId, TaskPoint taskPoint)
    {
        return PlanPathForTask(droneId, taskPoint, pathPlannerType);
    }

    public PathPlanningResult PlanPathForTaskPreferObstacleAware(int droneId, TaskPoint taskPoint)
    {
        PathPlannerType preferredPlanner =
            pathPlannerType == PathPlannerType.StraightLine
                ? PathPlannerType.AStar
                : pathPlannerType;
        return PlanPathForTask(droneId, taskPoint, preferredPlanner);
    }

    public PathPlanningResult PlanPathForTask(int droneId, TaskPoint taskPoint, PathPlannerType plannerType)
    {
        PathPlanningResult failedResult = new PathPlanningResult
        {
            success = false,
            plannerName = plannerType.ToString(),
            message = "路径规划未执行"
        };

        DroneController drone = GetDrone(droneId);
        DroneData data = GetDroneData(droneId);
        if (drone == null || data == null)
        {
            failedResult.message = $"未找到无人机 {droneId} 的控制器或数据";
            return failedResult;
        }

        if (taskPoint == null)
        {
            failedResult.message = "任务点为空";
            return failedResult;
        }

        IPathPlanner planner = CreatePathPlanner(plannerType);
        Vector3 startPosition = ToCruisePosition(drone.transform.position);
        Vector3 targetPosition = ToCruisePosition(taskPoint.transform.position);
        PathPlanningRequest request = new PathPlanningRequest
        {
            droneId = droneId,
            startPosition = startPosition,
            targetPosition = targetPosition,
            gridCellSize = planningGridCellSize,
            worldMin = planningWorldMin,
            worldMax = planningWorldMax,
            obstacleLayer = planningObstacleLayer,
            allowDiagonal = allowDiagonalPlanning
        };

        AlgorithmVisualizerManager visualizer = ResolveAlgorithmVisualizerManager();
        PathPlanningVisualizationRecorder recorder = visualizer != null
            ? visualizer.CreateRecorder(plannerType, droneId, data.droneName, request)
            : null;

        PathPlanningResult result = planner is IPathPlannerWithVisualization visualizationPlanner && recorder != null
            ? visualizationPlanner.PlanPath(request, recorder)
            : planner.PlanPath(request);

        NormalizePlanningResultToCruiseHeight(result);

        if (recorder != null && visualizer != null)
        {
            PathPlanningVisualizationBuilder.RecordFallbackTrace(recorder, request, result);
            recorder.FinalizeResult(result);
            visualizer.RegisterPlanningTrace(recorder.BuildTrace());
        }

        data.currentPlannerName = result.plannerName;
        data.plannedPath = result.waypoints ?? new List<Vector3>();
        data.currentWaypointIndex = 0;

        if (result.success)
        {
            Debug.Log($"[DroneManager] 无人机 {droneId} 已使用 {result.plannerName} 规划路径，waypoints: {data.plannedPath.Count}");
        }
        else
        {
            Debug.LogWarning($"[DroneManager] 无人机 {droneId} 路径规划失败: {result.message}");
        }

        return result;
    }

    private AlgorithmVisualizerManager ResolveAlgorithmVisualizerManager()
    {
        algorithmVisualizerManager = RuntimeSceneRegistry.Resolve(algorithmVisualizerManager, this);
        return algorithmVisualizerManager;
    }

    private void NormalizePlanningResultToCruiseHeight(PathPlanningResult result)
    {
        if (result == null)
        {
            return;
        }

        result.waypoints = NormalizePathToCruiseHeight(result.waypoints);
        result.totalCost = CalculatePathLength(result.waypoints);
    }

    private static float CalculatePathLength(List<Vector3> path)
    {
        if (path == null || path.Count <= 1)
        {
            return 0f;
        }

        float length = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            length += Vector3.Distance(path[i - 1], path[i]);
        }

        return length;
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
                data.lastKnownPosition = ToCruisePosition(drone.transform.position);
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

    private void BindCamerasToManagedDrones()
    {
        CameraManager cameraManager = RuntimeSceneRegistry.Get<CameraManager>(this);
        if (cameraManager == null)
        {
            return;
        }

        cameraManager.RefreshManagedDrones();
    }

    private void EnsurePlanningObstacleLayerConfigured()
    {
        if (planningObstacleLayer.value != 0)
        {
            return;
        }

        int buildingLayer = LayerMask.NameToLayer("Building");
        if (buildingLayer < 0)
        {
            Debug.LogWarning("[DroneManager] 未找到 Building 层，A* 障碍物检测仍需要手动配置");
            return;
        }

        planningObstacleLayer = 1 << buildingLayer;
        Debug.Log("[DroneManager] 已自动将路径规划障碍层设置为 Building");
    }

    private void ConfigurePlanningObstacles()
    {
        Transform root = ResolveObstacleRoot();
        if (root == null)
        {
            Debug.LogWarning("[DroneManager] 未找到障碍物根节点，当前不会自动配置建筑障碍物");
            return;
        }

        int buildingLayer = LayerMask.NameToLayer("Building");
        int configuredCount = 0;

        foreach (Transform child in root)
        {
            if (child == null)
            {
                continue;
            }

            if (assignBuildingLayerRecursively && buildingLayer >= 0)
            {
                ApplyLayerRecursively(child.gameObject, buildingLayer);
            }

            if (generateObstacleProxyColliders)
            {
                if (EnsureObstacleProxyCollider(child))
                {
                    configuredCount++;
                }
            }
        }

        Debug.Log($"[DroneManager] 已完成障碍物自动配置，根节点：{root.name}，代理碰撞体数量：{configuredCount}");
        RefreshObstacleFootprintCache();
    }

    private Transform ResolveObstacleRoot()
    {
        if (obstacleRoot != null)
        {
            return obstacleRoot;
        }

        GameObject root = GameObject.Find("Buildings");
        if (root != null)
        {
            obstacleRoot = root.transform;
        }

        return obstacleRoot;
    }

    private void EnsureObstacleFootprintCache()
    {
        if (!obstacleFootprintCacheInitialized)
        {
            RefreshObstacleFootprintCache();
        }
    }

    private void RefreshObstacleFootprintCache()
    {
        obstacleFootprintBounds.Clear();
        obstacleFootprintCacheInitialized = true;

        Transform root = ResolveObstacleRoot();
        if (root == null)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            if (bounds.size.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            obstacleFootprintBounds.Add(bounds);
        }

        if (obstacleFootprintBounds.Count > 0)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            if (bounds.size.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            obstacleFootprintBounds.Add(bounds);
        }
    }

    private static bool IsPointInsideBoundsXZ(Vector3 point, Bounds bounds, float padding)
    {
        float minX = bounds.min.x - padding;
        float maxX = bounds.max.x + padding;
        float minZ = bounds.min.z - padding;
        float maxZ = bounds.max.z + padding;

        return point.x >= minX &&
               point.x <= maxX &&
               point.z >= minZ &&
               point.z <= maxZ;
    }

    private static bool SegmentIntersectsBoundsXZ(Vector3 start, Vector3 end, Bounds bounds, float padding)
    {
        float minX = bounds.min.x - padding;
        float maxX = bounds.max.x + padding;
        float minZ = bounds.min.z - padding;
        float maxZ = bounds.max.z + padding;

        if ((start.x < minX && end.x < minX) ||
            (start.x > maxX && end.x > maxX) ||
            (start.z < minZ && end.z < minZ) ||
            (start.z > maxZ && end.z > maxZ))
        {
            return false;
        }

        if (IsPointInsideBoundsXZ(start, bounds, padding) || IsPointInsideBoundsXZ(end, bounds, padding))
        {
            return true;
        }

        float deltaX = end.x - start.x;
        float deltaZ = end.z - start.z;
        float tMin = 0f;
        float tMax = 1f;

        return ClipLine(-deltaX, start.x - minX, ref tMin, ref tMax) &&
               ClipLine(deltaX, maxX - start.x, ref tMin, ref tMax) &&
               ClipLine(-deltaZ, start.z - minZ, ref tMin, ref tMax) &&
               ClipLine(deltaZ, maxZ - start.z, ref tMin, ref tMax);
    }

    private static bool ClipLine(float denominator, float numerator, ref float tMin, ref float tMax)
    {
        if (Mathf.Abs(denominator) <= 0.0001f)
        {
            return numerator >= 0f;
        }

        float t = numerator / denominator;
        if (denominator > 0f)
        {
            if (t > tMax)
            {
                return false;
            }

            if (t > tMin)
            {
                tMin = t;
            }
        }
        else
        {
            if (t < tMin)
            {
                return false;
            }

            if (t < tMax)
            {
                tMax = t;
            }
        }

        return true;
    }

    private void ApplyLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            if (child != null)
            {
                ApplyLayerRecursively(child.gameObject, layer);
            }
        }
    }

    private bool EnsureObstacleProxyCollider(Transform obstacleTransform)
    {
        if (obstacleTransform == null)
        {
            return false;
        }

        Collider[] childColliders = obstacleTransform.GetComponentsInChildren<Collider>(true);
        if (childColliders.Any(collider => collider != null && collider.transform != obstacleTransform))
        {
            return false;
        }

        Renderer[] renderers = obstacleTransform.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return false;
        }

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            worldBounds.Encapsulate(renderers[i].bounds);
        }

        if (worldBounds.size == Vector3.zero)
        {
            return false;
        }

        BoxCollider boxCollider = obstacleTransform.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = obstacleTransform.gameObject.AddComponent<BoxCollider>();
        }

        Bounds localBounds = CalculateLocalBounds(obstacleTransform, worldBounds);
        boxCollider.center = localBounds.center;
        boxCollider.size = Vector3.Max(localBounds.size + obstacleColliderPadding, minimumObstacleColliderSize);
        boxCollider.isTrigger = false;
        return true;
    }

    private Bounds CalculateLocalBounds(Transform target, Bounds worldBounds)
    {
        Vector3 extents = worldBounds.extents;
        Vector3 center = worldBounds.center;
        Vector3[] worldCorners =
        {
            center + new Vector3(-extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y, extents.z),
            center + new Vector3(-extents.x, extents.y, -extents.z),
            center + new Vector3(-extents.x, extents.y, extents.z),
            center + new Vector3(extents.x, -extents.y, -extents.z),
            center + new Vector3(extents.x, -extents.y, extents.z),
            center + new Vector3(extents.x, extents.y, -extents.z),
            center + new Vector3(extents.x, extents.y, extents.z)
        };

        Vector3 localCorner = target.InverseTransformPoint(worldCorners[0]);
        Bounds localBounds = new Bounds(localCorner, Vector3.zero);
        for (int i = 1; i < worldCorners.Length; i++)
        {
            localBounds.Encapsulate(target.InverseTransformPoint(worldCorners[i]));
        }

        return localBounds;
    }

    private List<Vector3> BuildSpawnPositions(int count)
    {
        List<Vector3> positions = new List<Vector3>(count);

        if (useSceneSpawnPoints)
        {
            List<Transform> sceneSpawnPoints = GetSceneSpawnPoints();
            for (int i = 0; i < sceneSpawnPoints.Count && positions.Count < count; i++)
            {
                positions.Add(sceneSpawnPoints[i].position);
            }
        }

        while (positions.Count < count)
        {
            positions.Add(spawnOrigin + spawnDirection * (positions.Count * spawnSpacing));
        }

        return positions;
    }

    private Vector3 AdjustSpawnHeight(Vector3 position)
    {
        return ToCruisePosition(position);
    }

    private void ApplyConfigToRuntimeObjects(DroneConfig config)
    {
        if (config == null)
        {
            return;
        }

        foreach (DroneController drone in drones)
        {
            if (drone != null)
            {
                drone.speed = Mathf.Max(0.1f, config.defaultSpeed);
            }
        }

        foreach (DroneStateMachine stateMachine in GetComponentsInChildren<DroneStateMachine>(true))
        {
            if (stateMachine != null)
            {
                stateMachine.ApplyConfigDefaults(config);
            }
        }
    }

    private List<Transform> GetSceneSpawnPoints()
    {
        List<Transform> spawnPoints = new List<Transform>();
        HashSet<Transform> uniqueSpawnPoints = new HashSet<Transform>();
        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");

        foreach (GameObject taggedObject in taggedObjects)
        {
            if (taggedObject != null && uniqueSpawnPoints.Add(taggedObject.transform))
            {
                spawnPoints.Add(taggedObject.transform);
            }
        }

        DroneSpawnPointMarker[] markers = SimulationContext.GetOrCreate(this).GetSpawnPointMarkers();
        System.Array.Sort(markers, (left, right) =>
        {
            int orderCompare = left.orderIndex.CompareTo(right.orderIndex);
            return orderCompare != 0 ? orderCompare : string.CompareOrdinal(left.name, right.name);
        });

        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null && uniqueSpawnPoints.Add(markers[i].transform))
            {
                spawnPoints.Add(markers[i].transform);
            }
        }

        spawnPoints.Sort((left, right) =>
        {
            DroneSpawnPointMarker leftMarker = left != null ? left.GetComponent<DroneSpawnPointMarker>() : null;
            DroneSpawnPointMarker rightMarker = right != null ? right.GetComponent<DroneSpawnPointMarker>() : null;
            if (leftMarker != null || rightMarker != null)
            {
                int leftOrder = leftMarker != null ? leftMarker.orderIndex : int.MaxValue;
                int rightOrder = rightMarker != null ? rightMarker.orderIndex : int.MaxValue;
                int orderCompare = leftOrder.CompareTo(rightOrder);
                if (orderCompare != 0)
                {
                    return orderCompare;
                }
            }

            string leftName = left != null ? left.name : string.Empty;
            string rightName = right != null ? right.name : string.Empty;
            return string.CompareOrdinal(leftName, rightName);
        });
        return spawnPoints;
    }

    private void ResolveDroneOverlaps()
    {
        if (drones == null || drones.Count <= 1)
        {
            return;
        }

        float minDistance = Mathf.Max(0.1f, minimumDroneSeparation);
        float minDistanceSq = minDistance * minDistance;

        for (int i = 0; i < drones.Count; i++)
        {
            DroneController left = drones[i];
            if (left == null)
            {
                continue;
            }

            for (int j = i + 1; j < drones.Count; j++)
            {
                DroneController right = drones[j];
                if (right == null)
                {
                    continue;
                }

                Vector3 delta = right.transform.position - left.transform.position;
                delta.y = 0f;
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq >= minDistanceSq)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(Mathf.Max(distanceSq, 0.000001f));
                Vector3 separationDirection = distance > 0.0001f
                    ? delta / distance
                    : BuildFallbackSeparationDirection(i, j);
                float penetration = minDistance - distance;
                Vector3 correction = separationDirection * (penetration * separationResolveFactor);

                ApplySeparation(left, right, correction);
            }
        }

        SyncDroneDataPositionsFromTransforms();
    }

    private void ApplySeparation(DroneController left, DroneController right, Vector3 correction)
    {
        DroneStateMachine leftStateMachine = left != null ? left.stateMachine : null;
        DroneStateMachine rightStateMachine = right != null ? right.stateMachine : null;

        bool leftWaiting = leftStateMachine != null && leftStateMachine.currentState == DroneState.Waiting;
        bool rightWaiting = rightStateMachine != null && rightStateMachine.currentState == DroneState.Waiting;

        if (leftWaiting && !rightWaiting)
        {
            left.transform.position -= correction;
            SnapDroneToCruiseHeight(left);
            return;
        }

        if (!leftWaiting && rightWaiting)
        {
            right.transform.position += correction;
            SnapDroneToCruiseHeight(right);
            return;
        }

        Vector3 halfCorrection = correction * 0.5f;
        left.transform.position -= halfCorrection;
        right.transform.position += halfCorrection;
        SnapDroneToCruiseHeight(left);
        SnapDroneToCruiseHeight(right);
    }

    private Vector3 BuildFallbackSeparationDirection(int leftIndex, int rightIndex)
    {
        float angle = (leftIndex * 31f + rightIndex * 17f) * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        return direction.sqrMagnitude <= 0.0001f ? Vector3.right : direction.normalized;
    }

    private void SyncDroneDataPositionsFromTransforms()
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
                data.lastKnownPosition = ToCruisePosition(drone.transform.position);
            }
        }
    }
}
