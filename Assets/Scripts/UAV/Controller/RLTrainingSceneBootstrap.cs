using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RLTrainingSceneBootstrap : MonoBehaviour
{
    public const string SceneName = "RLTrainingSandbox";
    public const string ScenePath = "Assets/Scenes/Sandbox/RLTrainingSandbox.unity";

    private static readonly Vector3 RuntimePlanningMin = new Vector3(-30f, 0f, -30f);
    private static readonly Vector3 RuntimePlanningMax = new Vector3(30f, 12f, 30f);
    private static readonly Vector3 RuntimeSpawnPosition = new Vector3(-20f, 0.06f, -20f);
    private static readonly Vector3 RuntimeTaskPosition = new Vector3(20f, 0.25f, 20f);

    [Header("References")]
    public SimulationManager simulationManager;
    public DroneManager droneManager;
    public CameraManager cameraManager;

    [Header("Defaults")]
    public bool autoConfigureInTrainingScene = true;
    public Vector3 defaultSpawnPosition = new Vector3(-20f, 0.06f, -20f);
    public Vector3 defaultTaskPosition = new Vector3(20f, 0.25f, 20f);
    public Vector3 planningWorldMin = new Vector3(-30f, 0f, -30f);
    public Vector3 planningWorldMax = new Vector3(30f, 12f, 30f);
    public float planningGridCellSize = 2f;

    public string LastSetupMessage { get; private set; } = "RL training scene not configured";

    public static void LoadRuntimeTrainingScene(
        SimulationManager sourceSimulationManager,
        DroneManager sourceDroneManager,
        CameraManager sourceCameraManager)
    {
        Scene previousScene = SceneManager.GetActiveScene();
        if (string.Equals(previousScene.name, SceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            RLTrainingSceneBootstrap existingBootstrap = RuntimeSceneRegistry.Get<RLTrainingSceneBootstrap>();
            existingBootstrap?.ConfigureScene();
            return;
        }

        SimulationManager simulationManager = RuntimeSceneRegistry.Resolve(sourceSimulationManager);
        DroneManager droneManager = RuntimeSceneRegistry.Resolve(
            sourceDroneManager,
            simulationManager != null ? simulationManager.droneManager : null);
        CameraManager cameraManager = RuntimeSceneRegistry.Resolve(sourceCameraManager);

        DroneController dronePrefab = droneManager != null ? droneManager.dronePrefab : null;
        DroneConfig droneConfig = droneManager != null ? droneManager.droneConfig : null;
        string droneConfigResourcePath = droneManager != null ? droneManager.droneConfigResourcePath : "Configs/DroneConfig_Default";
        TMP_FontAsset runtimeFont = simulationManager != null && simulationManager.statusText != null
            ? simulationManager.statusText.font
            : null;

        Scene trainingScene = SceneManager.CreateScene(SceneName);
        SceneManager.SetActiveScene(trainingScene);

        MoveRootToScene(simulationManager, trainingScene);
        MoveRootToScene(droneManager, trainingScene);
        MoveRootToScene(cameraManager, trainingScene);
        SimulationContext existingContext = SimulationContext.Current ?? RuntimeSceneRegistry.Get<SimulationContext>();
        MoveRootToScene(existingContext, trainingScene);

        if (simulationManager == null)
        {
            GameObject simulationObject = new GameObject("SimulationManager");
            SceneManager.MoveGameObjectToScene(simulationObject, trainingScene);
            simulationManager = simulationObject.AddComponent<SimulationManager>();
        }

        if (droneManager == null)
        {
            droneManager = simulationManager.GetComponent<DroneManager>();
            if (droneManager == null)
            {
                droneManager = simulationManager.gameObject.AddComponent<DroneManager>();
            }
        }

        if (cameraManager == null)
        {
            cameraManager = simulationManager.GetComponent<CameraManager>();
            if (cameraManager == null)
            {
                cameraManager = simulationManager.gameObject.AddComponent<CameraManager>();
            }
        }

        RuntimeSceneRegistry.Register(simulationManager);
        RuntimeSceneRegistry.Register(droneManager);
        RuntimeSceneRegistry.Register(cameraManager);
        MoveRootToScene(SimulationContext.GetOrCreate(simulationManager), trainingScene);

        Transform obstacleRoot = CreateRuntimeObstacleRoot(trainingScene).transform;
        CreateRuntimeGround(trainingScene);
        CreateRuntimeLight(trainingScene);
        Camera overviewCamera = CreateRuntimeOverviewCamera(trainingScene);
        Camera followCamera = CreateRuntimeFollowCamera(trainingScene);
        TMP_Text statusText = CreateRuntimeCanvas(trainingScene, runtimeFont);
        CreateRuntimeEventSystem(trainingScene);
        DisableUnmanagedCameras(overviewCamera, followCamera);

        droneManager.dronePrefab = dronePrefab;
        droneManager.droneConfig = droneConfig;
        droneManager.droneConfigResourcePath = droneConfigResourcePath;
        droneManager.obstacleRoot = obstacleRoot;
        droneManager.droneCount = 1;
        droneManager.useSceneSpawnPoints = true;
        droneManager.pathPlannerType = PathPlannerType.QLearningOffline;
        droneManager.ApplyPlanningSettings(2f, false, true, RuntimePlanningMin, RuntimePlanningMax);

        cameraManager.overviewCamera = overviewCamera;
        cameraManager.followCamera = followCamera;
        cameraManager.isOverview = true;
        cameraManager.isTopDown2D = false;
        cameraManager.topDownHeight = 64f;
        cameraManager.SwitchToOverview();

        simulationManager.droneManager = droneManager;
        simulationManager.statusText = statusText;

        RLMapExporter mapExporter = simulationManager.GetComponent<RLMapExporter>();
        if (mapExporter == null)
        {
            mapExporter = simulationManager.gameObject.AddComponent<RLMapExporter>();
        }
        mapExporter.droneManager = droneManager;
        simulationManager.rlMapExporter = mapExporter;
        RuntimeSceneRegistry.Register(mapExporter);

        RLPathResultImporter pathResultImporter = simulationManager.GetComponent<RLPathResultImporter>();
        if (pathResultImporter == null)
        {
            pathResultImporter = simulationManager.gameObject.AddComponent<RLPathResultImporter>();
        }
        pathResultImporter.droneManager = droneManager;
        simulationManager.rlPathResultImporter = pathResultImporter;
        RuntimeSceneRegistry.Register(pathResultImporter);

        RLQlearningTrainingRunner trainingRunner = simulationManager.GetComponent<RLQlearningTrainingRunner>();
        if (trainingRunner == null)
        {
            trainingRunner = simulationManager.gameObject.AddComponent<RLQlearningTrainingRunner>();
        }
        simulationManager.rlTrainingRunner = trainingRunner;
        RuntimeSceneRegistry.Register(trainingRunner);

        RLTrainingMapBoundsVisualizer boundsVisualizer = simulationManager.GetComponent<RLTrainingMapBoundsVisualizer>();
        if (boundsVisualizer == null)
        {
            boundsVisualizer = simulationManager.gameObject.AddComponent<RLTrainingMapBoundsVisualizer>();
        }
        boundsVisualizer.droneManager = droneManager;
        simulationManager.rlTrainingMapBoundsVisualizer = boundsVisualizer;
        RuntimeSceneRegistry.Register(boundsVisualizer);

        RLTrainingSceneBootstrap bootstrap = simulationManager.GetComponent<RLTrainingSceneBootstrap>();
        if (bootstrap == null)
        {
            bootstrap = simulationManager.gameObject.AddComponent<RLTrainingSceneBootstrap>();
        }
        bootstrap.simulationManager = simulationManager;
        bootstrap.droneManager = droneManager;
        bootstrap.cameraManager = cameraManager;
        bootstrap.defaultSpawnPosition = RuntimeSpawnPosition;
        bootstrap.defaultTaskPosition = RuntimeTaskPosition;
        bootstrap.planningWorldMin = RuntimePlanningMin;
        bootstrap.planningWorldMax = RuntimePlanningMax;
        bootstrap.planningGridCellSize = 2f;
        simulationManager.rlTrainingSceneBootstrap = bootstrap;
        RuntimeSceneRegistry.Register(bootstrap);

        bootstrap.ConfigureScene();

        if (previousScene.IsValid() && previousScene != trainingScene)
        {
            SceneManager.UnloadSceneAsync(previousScene);
        }
    }

    private void Start()
    {
        if (!autoConfigureInTrainingScene)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, SceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ConfigureScene();
    }

    [ContextMenu("Configure RL Training Scene")]
    public void ConfigureScene()
    {
        CacheReferences();
        EnsureGroundPlane();
        ClearRuntimeObstacles();
        EnsureDefaultSpawnPoint();
        EnsureDefaultTaskPoint();
        ConfigureDroneManager();
        cameraManager?.SwitchToOverview();

        LastSetupMessage = "RL training scene ready: one drone, one task, blank obstacle area";
        Debug.Log($"[RLTrainingSceneBootstrap] {LastSetupMessage}");
    }

    private void CacheReferences()
    {
        simulationManager = RuntimeSceneRegistry.Resolve(simulationManager, this);
        droneManager = RuntimeSceneRegistry.Resolve(
            droneManager,
            simulationManager != null ? simulationManager.droneManager : null,
            this);
        cameraManager = RuntimeSceneRegistry.Resolve(cameraManager, this);
    }

    private void EnsureGroundPlane()
    {
        GameObject ground =
            GameObject.Find("Plane001") ??
            GameObject.Find("Plane") ??
            GameObject.Find("Ground");

        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Plane001";
            ground.transform.position = Vector3.zero;
        }

        ground.transform.localScale = new Vector3(12f, 1f, 12f);
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
        {
            ground.layer = groundLayer;
        }
    }

    private void ClearRuntimeObstacles()
    {
        if (droneManager == null)
        {
            return;
        }

        Transform obstacleRoot = droneManager.EnsureObstacleRootExists();
        for (int i = obstacleRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(obstacleRoot.GetChild(i).gameObject);
        }

        SimulationContext.Current?.NotifyObstaclesChanged();
    }

    private void EnsureDefaultSpawnPoint()
    {
        SimulationContext context = SimulationContext.GetOrCreate(this);
        DroneSpawnPointMarker[] existingMarkers = context.GetSpawnPointMarkers();
        for (int i = 0; i < existingMarkers.Length; i++)
        {
            if (existingMarkers[i] != null)
            {
                context.UnregisterSpawnPoint(existingMarkers[i], false);
                Destroy(existingMarkers[i].gameObject);
            }
        }

        GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        markerObject.name = "RL_DefaultSpawnPoint";
        markerObject.transform.position = defaultSpawnPosition;
        markerObject.transform.localScale = new Vector3(1f, 0.06f, 1f);

        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer >= 0)
        {
            markerObject.layer = ignoreRaycastLayer;
        }

        Collider collider = markerObject.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        Renderer renderer = markerObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.16f, 0.82f, 0.94f, 0.92f);
        }

        DroneSpawnPointMarker marker = markerObject.AddComponent<DroneSpawnPointMarker>();
        marker.orderIndex = 0;
        context.RegisterSpawnPoint(marker);
        context.NotifySpawnPointsChanged();
    }

    private void EnsureDefaultTaskPoint()
    {
        SimulationContext context = SimulationContext.GetOrCreate(this);
        TaskPoint[] existingTasks = context.GetTaskPoints();
        for (int i = 0; i < existingTasks.Length; i++)
        {
            if (existingTasks[i] != null)
            {
                context.UnregisterTaskPoint(existingTasks[i], false);
                Destroy(existingTasks[i].gameObject);
            }
        }

        GameObject taskObject = new GameObject("RL_DefaultTaskPoint");
        taskObject.transform.position = defaultTaskPosition;

        GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        markerObject.name = "Marker";
        markerObject.transform.SetParent(taskObject.transform, false);
        markerObject.transform.localPosition = Vector3.zero;
        markerObject.transform.localScale = Vector3.one * 1.2f;

        Renderer renderer = markerObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.yellow;
        }

        TaskPoint taskPoint = taskObject.AddComponent<TaskPoint>();
        taskPoint.taskId = 1;
        taskPoint.taskName = "RL_Target_01";
        taskPoint.priority = 1;
        taskPoint.description = "Default RL training target";

        context.RegisterTaskPoint(taskPoint);
        context.NotifyTasksChanged();
    }

    private void ConfigureDroneManager()
    {
        if (droneManager == null)
        {
            return;
        }

        droneManager.droneCount = 1;
        droneManager.useSceneSpawnPoints = true;
        droneManager.pathPlannerType = PathPlannerType.QLearningOffline;
        droneManager.ApplyPlanningSettings(
            planningGridCellSize,
            false,
            true,
            planningWorldMin,
            planningWorldMax);
        droneManager.RespawnDrones(1);
        droneManager.ApplyPathVisibilityToAll(true, true);
    }

    private static void MoveRootToScene(Component component, Scene scene)
    {
        if (component == null || !scene.IsValid())
        {
            return;
        }

        GameObject root = component.transform.root.gameObject;
        if (root.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(root, scene);
        }
    }

    private static GameObject CreateRuntimeObstacleRoot(Scene scene)
    {
        GameObject obstacleRoot = new GameObject("Buildings");
        SceneManager.MoveGameObjectToScene(obstacleRoot, scene);
        SetLayerIfExists(obstacleRoot, "Building");
        return obstacleRoot;
    }

    private static void CreateRuntimeGround(Scene scene)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(12f, 1f, 12f);
        SceneManager.MoveGameObjectToScene(ground, scene);
        SetLayerIfExists(ground, "Ground");

        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.66f, 0.70f, 0.73f, 1f);
        }
    }

    private static void CreateRuntimeLight(Scene scene)
    {
        GameObject lightObject = new GameObject("Directional Light");
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        RenderSettings.ambientLight = new Color(0.58f, 0.62f, 0.66f, 1f);
    }

    private static Camera CreateRuntimeOverviewCamera(Scene scene)
    {
        GameObject cameraObject = new GameObject("OverviewCamera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 48f, -48f);
        cameraObject.transform.rotation = Quaternion.Euler(58f, 0f, 0f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 50f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 500f;
        cameraObject.AddComponent<AudioListener>();
        return camera;
    }

    private static Camera CreateRuntimeFollowCamera(Scene scene)
    {
        GameObject cameraObject = new GameObject("FollowCamera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        cameraObject.transform.position = new Vector3(-20f, 8f, -30f);
        cameraObject.transform.rotation = Quaternion.Euler(25f, 0f, 0f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = 58f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 500f;
        return camera;
    }

    private static TMP_Text CreateRuntimeCanvas(Scene scene, TMP_FontAsset runtimeFont)
    {
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject statusObject = new GameObject(
            "StatusText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        statusObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = statusObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(32f, -28f);
        rect.sizeDelta = new Vector2(560f, 40f);

        TextMeshProUGUI statusText = statusObject.GetComponent<TextMeshProUGUI>();
        if (runtimeFont != null)
        {
            statusText.font = runtimeFont;
        }

        statusText.text = "Status: RL training sandbox";
        statusText.fontSize = 20f;
        statusText.color = new Color(0.88f, 0.96f, 1f, 1f);
        statusText.raycastTarget = false;
        return statusText;
    }

    private static void CreateRuntimeEventSystem(Scene scene)
    {
        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
    }

    private static void DisableUnmanagedCameras(Camera overviewCamera, Camera followCamera)
    {
        Camera[] cameras = FindObjectsOfType<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera == overviewCamera || camera == followCamera)
            {
                continue;
            }

            camera.enabled = false;
            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = false;
            }
        }
    }

    private static void SetLayerIfExists(GameObject gameObject, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (gameObject != null && layer >= 0)
        {
            gameObject.layer = layer;
        }
    }
}
