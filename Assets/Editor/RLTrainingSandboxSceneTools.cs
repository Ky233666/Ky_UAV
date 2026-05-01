using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RLTrainingSandboxSceneTools
{
    private const string SceneDirectory = "Assets/Scenes/Sandbox";
    private const string ScenePath = "Assets/Scenes/Sandbox/RLTrainingSandbox.unity";
    private const string DronePrefabPath = "Assets/Prefabs/UAV/Drone.prefab";
    private const string DroneConfigPath = "Assets/Resources/Configs/DroneConfig_Default.asset";

    private static readonly Vector3 PlanningWorldMin = new Vector3(-30f, 0f, -30f);
    private static readonly Vector3 PlanningWorldMax = new Vector3(30f, 12f, 30f);
    private static readonly Vector3 DefaultSpawnPosition = new Vector3(-20f, 0.06f, -20f);
    private static readonly Vector3 DefaultTaskPosition = new Vector3(20f, 0.25f, 20f);

    [MenuItem("Tools/KY UAV/Create Or Refresh RL Training Sandbox Scene")]
    public static void CreateOrRefreshRLTrainingSandboxSceneMenu()
    {
        CreateOrRefreshRLTrainingSandboxScene(promptToSaveCurrentScenes: true);
    }

    [MenuItem("Tools/KY UAV/Open RL Training Sandbox Scene")]
    public static void OpenRLTrainingSandboxSceneMenu()
    {
        if (!File.Exists(ScenePath))
        {
            CreateOrRefreshRLTrainingSandboxScene(promptToSaveCurrentScenes: true);
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    public static void CreateOrRefreshRLTrainingSandboxSceneBatch()
    {
        CreateOrRefreshRLTrainingSandboxScene(promptToSaveCurrentScenes: false);
    }

    public static void CreateOrRefreshRLTrainingSandboxScene()
    {
        CreateOrRefreshRLTrainingSandboxScene(promptToSaveCurrentScenes: true);
    }

    private static void CreateOrRefreshRLTrainingSandboxScene(bool promptToSaveCurrentScenes)
    {
        if (promptToSaveCurrentScenes && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EnsureFolder(SceneDirectory);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject obstacleRoot = CreateObstacleRoot(scene);
        CreateGround(scene);
        CreateLighting(scene);
        Camera overviewCamera = CreateOverviewCamera(scene);
        Camera followCamera = CreateFollowCamera(scene);
        TMP_Text statusText = CreateRuntimeCanvas(scene);
        CreateEventSystem(scene);
        CreateDefaultSpawnPoint(scene);
        CreateDefaultTaskPoint(scene);
        CreateRuntimeManagers(scene, obstacleRoot.transform, overviewCamera, followCamera, statusText);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureSceneInBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (sceneAsset != null)
        {
            Selection.activeObject = sceneAsset;
        }

        Debug.Log($"[RLTrainingSandboxSceneTools] Created blank RL training sandbox scene: {ScenePath}");
    }

    private static GameObject CreateObstacleRoot(Scene scene)
    {
        GameObject obstacleRoot = new GameObject("Buildings");
        MoveToScene(obstacleRoot, scene);
        SetLayerIfExists(obstacleRoot, "Building");
        return obstacleRoot;
    }

    private static void CreateGround(Scene scene)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(12f, 1f, 12f);
        MoveToScene(ground, scene);
        SetLayerIfExists(ground, "Ground");

        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                Material material = new Material(shader)
                {
                    color = new Color(0.66f, 0.70f, 0.73f, 1f)
                };
                renderer.sharedMaterial = material;
            }
        }
    }

    private static void CreateLighting(Scene scene)
    {
        GameObject lightObject = new GameObject("Directional Light");
        MoveToScene(lightObject, scene);
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = Color.white;

        RenderSettings.ambientLight = new Color(0.58f, 0.62f, 0.66f, 1f);
    }

    private static Camera CreateOverviewCamera(Scene scene)
    {
        GameObject cameraObject = new GameObject("OverviewCamera");
        MoveToScene(cameraObject, scene);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 48f, -48f);
        cameraObject.transform.rotation = Quaternion.Euler(58f, 0f, 0f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 50f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 500f;
        cameraObject.AddComponent<AudioListener>();
        return camera;
    }

    private static Camera CreateFollowCamera(Scene scene)
    {
        GameObject cameraObject = new GameObject("FollowCamera");
        MoveToScene(cameraObject, scene);
        cameraObject.transform.position = new Vector3(-20f, 8f, -30f);
        cameraObject.transform.rotation = Quaternion.Euler(25f, 0f, 0f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 58f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 500f;
        return camera;
    }

    private static TMP_Text CreateRuntimeCanvas(Scene scene)
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        MoveToScene(canvasObject, scene);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject statusObject = new GameObject("StatusText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        statusObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = statusObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(32f, -28f);
        rect.sizeDelta = new Vector2(560f, 40f);

        TextMeshProUGUI text = statusObject.GetComponent<TextMeshProUGUI>();
        text.text = "Status: RL training sandbox";
        text.fontSize = 20f;
        text.color = new Color(0.88f, 0.96f, 1f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static void CreateEventSystem(Scene scene)
    {
        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        MoveToScene(eventSystemObject, scene);
    }

    private static void CreateDefaultSpawnPoint(Scene scene)
    {
        GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        markerObject.name = "RL_DefaultSpawnPoint";
        markerObject.transform.position = DefaultSpawnPosition;
        markerObject.transform.localScale = new Vector3(1.2f, 0.08f, 1.2f);
        MoveToScene(markerObject, scene);
        SetLayerIfExists(markerObject, "Ignore Raycast");

        Collider collider = markerObject.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        Renderer renderer = markerObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateRuntimeColorMaterial(new Color(0.16f, 0.82f, 0.94f, 0.92f));
        }

        DroneSpawnPointMarker marker = markerObject.AddComponent<DroneSpawnPointMarker>();
        marker.orderIndex = 0;
    }

    private static void CreateDefaultTaskPoint(Scene scene)
    {
        GameObject taskObject = new GameObject("RL_DefaultTaskPoint");
        taskObject.transform.position = DefaultTaskPosition;
        MoveToScene(taskObject, scene);

        GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        markerObject.name = "Marker";
        markerObject.transform.SetParent(taskObject.transform, false);
        markerObject.transform.localPosition = Vector3.zero;
        markerObject.transform.localScale = Vector3.one * 1.2f;

        Renderer renderer = markerObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateRuntimeColorMaterial(Color.yellow);
        }

        TaskPoint taskPoint = taskObject.AddComponent<TaskPoint>();
        taskPoint.taskId = 1;
        taskPoint.taskName = "RL_Target_01";
        taskPoint.priority = 1;
        taskPoint.description = "Default RL training target";
    }

    private static void CreateRuntimeManagers(
        Scene scene,
        Transform obstacleRoot,
        Camera overviewCamera,
        Camera followCamera,
        TMP_Text statusText)
    {
        GameObject managerObject = new GameObject("SimulationManager");
        MoveToScene(managerObject, scene);

        SimulationManager simulationManager = managerObject.AddComponent<SimulationManager>();
        DroneManager droneManager = managerObject.AddComponent<DroneManager>();
        CameraManager cameraManager = managerObject.AddComponent<CameraManager>();
        RLMapExporter mapExporter = managerObject.AddComponent<RLMapExporter>();
        RLPathResultImporter pathResultImporter = managerObject.AddComponent<RLPathResultImporter>();
        RLTrainingMapBoundsVisualizer boundsVisualizer = managerObject.AddComponent<RLTrainingMapBoundsVisualizer>();
        RLTrainingSceneBootstrap bootstrap = managerObject.AddComponent<RLTrainingSceneBootstrap>();

        GameObject dronePrefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrefabPath);
        droneManager.dronePrefab = dronePrefabObject != null ? dronePrefabObject.GetComponent<DroneController>() : null;
        droneManager.droneConfig = AssetDatabase.LoadAssetAtPath<DroneConfig>(DroneConfigPath);
        droneManager.droneCount = 1;
        droneManager.useSceneSpawnPoints = true;
        droneManager.spawnOrigin = DefaultSpawnPosition;
        droneManager.pathPlannerType = PathPlannerType.QLearningOffline;
        droneManager.planningGridCellSize = 2f;
        droneManager.planningWorldMin = PlanningWorldMin;
        droneManager.planningWorldMax = PlanningWorldMax;
        droneManager.allowDiagonalPlanning = false;
        droneManager.autoConfigurePlanningObstacles = true;
        droneManager.obstacleRoot = obstacleRoot;
        int buildingLayer = LayerMask.NameToLayer("Building");
        if (buildingLayer >= 0)
        {
            droneManager.planningObstacleLayer = 1 << buildingLayer;
        }

        cameraManager.overviewCamera = overviewCamera;
        cameraManager.followCamera = followCamera;
        cameraManager.isOverview = true;
        cameraManager.isTopDown2D = false;
        cameraManager.topDownHeight = 64f;
        cameraManager.topDownMinOrthographicSize = 12f;
        cameraManager.topDownMaxOrthographicSize = 90f;

        mapExporter.droneManager = droneManager;
        pathResultImporter.droneManager = droneManager;
        boundsVisualizer.droneManager = droneManager;

        bootstrap.simulationManager = simulationManager;
        bootstrap.droneManager = droneManager;
        bootstrap.cameraManager = cameraManager;
        bootstrap.defaultSpawnPosition = DefaultSpawnPosition;
        bootstrap.defaultTaskPosition = DefaultTaskPosition;
        bootstrap.planningWorldMin = PlanningWorldMin;
        bootstrap.planningWorldMax = PlanningWorldMax;
        bootstrap.planningGridCellSize = 2f;

        simulationManager.droneManager = droneManager;
        simulationManager.statusText = statusText;
        simulationManager.rlMapExporter = mapExporter;
        simulationManager.rlPathResultImporter = pathResultImporter;
        simulationManager.rlTrainingMapBoundsVisualizer = boundsVisualizer;
        simulationManager.rlTrainingSceneBootstrap = bootstrap;

        if (droneManager.dronePrefab == null)
        {
            Debug.LogWarning($"[RLTrainingSandboxSceneTools] Drone prefab missing or invalid: {DronePrefabPath}");
        }
    }

    private static Material CreateRuntimeColorMaterial(Color color)
    {
        Shader shader =
            Shader.Find("Standard") ??
            Shader.Find("Sprites/Default") ??
            Shader.Find("Unlit/Color");

        Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Diffuse"));
        material.color = color;
        return material;
    }

    private static void EnsureSceneInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (!string.Equals(scenes[i].path, ScenePath, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            scenes[i] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes.ToArray();
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        string folderName = Path.GetFileName(assetPath);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static void MoveToScene(GameObject gameObject, Scene scene)
    {
        if (gameObject != null && scene.IsValid())
        {
            SceneManager.MoveGameObjectToScene(gameObject, scene);
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
