using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class KyUavSandboxSceneTools
{
    private const string MainScenePath = "Assets/Scenes/Main/MainScene.unity";
    private const string SandboxSceneDirectory = "Assets/Scenes/Sandbox";
    private const string SandboxScenePath = SandboxSceneDirectory + "/CustomObstacleSandbox.unity";
    private const float SandboxGroundScale = 24f;

    [MenuItem("Tools/KY UAV/Create Or Refresh Custom Obstacle Sandbox Scene")]
    public static void CreateOrRefreshCustomObstacleSandboxSceneMenu()
    {
        CreateOrRefreshCustomObstacleSandboxScene(promptToSaveCurrentScenes: true);
    }

    [MenuItem("Tools/KY UAV/Open Custom Obstacle Sandbox Scene")]
    public static void OpenCustomObstacleSandboxSceneMenu()
    {
        if (!File.Exists(SandboxScenePath))
        {
            CreateOrRefreshCustomObstacleSandboxScene();
            return;
        }

        EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Single);
    }

    public static void CreateOrRefreshCustomObstacleSandboxSceneBatch()
    {
        CreateOrRefreshCustomObstacleSandboxScene(promptToSaveCurrentScenes: false);
    }

    public static void CreateOrRefreshCustomObstacleSandboxScene()
    {
        CreateOrRefreshCustomObstacleSandboxScene(promptToSaveCurrentScenes: true);
    }

    private static void CreateOrRefreshCustomObstacleSandboxScene(bool promptToSaveCurrentScenes)
    {
        if (!File.Exists(MainScenePath))
        {
            throw new FileNotFoundException("MainScene 不存在。", MainScenePath);
        }

        if (promptToSaveCurrentScenes && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EnsureSandboxFolderExists();

        if (File.Exists(SandboxScenePath) && !AssetDatabase.DeleteAsset(SandboxScenePath))
        {
            throw new IOException($"无法删除已有场景：{SandboxScenePath}");
        }

        if (!AssetDatabase.CopyAsset(MainScenePath, SandboxScenePath))
        {
            throw new IOException($"无法复制场景：{MainScenePath} -> {SandboxScenePath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Scene sandboxScene = EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Single);
        ConfigureSandboxScene(sandboxScene);
        EditorSceneManager.MarkSceneDirty(sandboxScene);
        EditorSceneManager.SaveScene(sandboxScene);

        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SandboxScenePath);
        if (sceneAsset != null)
        {
            Selection.activeObject = sceneAsset;
        }

        Debug.Log($"[KyUavSandboxSceneTools] 已生成自定义障碍物实验场景：{SandboxScenePath}");
    }

    private static void EnsureSandboxFolderExists()
    {
        if (AssetDatabase.IsValidFolder(SandboxSceneDirectory))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        AssetDatabase.CreateFolder("Assets/Scenes", "Sandbox");
    }

    private static void ConfigureSandboxScene(Scene sandboxScene)
    {
        if (!sandboxScene.IsValid())
        {
            throw new IOException("Sandbox scene 无效，无法继续配置。");
        }

        GameObject obstacleRoot = GameObject.Find("Buildings");
        if (obstacleRoot == null)
        {
            obstacleRoot = new GameObject("Buildings");
            SceneManager.MoveGameObjectToScene(obstacleRoot, sandboxScene);
        }

        ClearChildrenImmediate(obstacleRoot.transform);
        EnsureGroundPlaneExists(sandboxScene);
        ClearRuntimeTaskPoints();

        DroneManager droneManager = Object.FindObjectOfType<DroneManager>();
        if (droneManager != null)
        {
            droneManager.obstacleRoot = obstacleRoot.transform;
            droneManager.autoConfigurePlanningObstacles = true;
            droneManager.pathPlannerType = PathPlannerType.AStar;
            droneManager.planningGridCellSize = 1.5f;
            droneManager.planningWorldMin = new Vector3(-100f, 0f, -100f);
            droneManager.planningWorldMax = new Vector3(100f, 30f, 100f);
            droneManager.RefreshObstacleConfiguration();
        }
    }

    private static void ClearChildrenImmediate(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static void EnsureGroundPlaneExists(Scene sandboxScene)
    {
        GameObject groundPlane =
            GameObject.Find("Plane001") ??
            GameObject.Find("Plane") ??
            GameObject.Find("Ground");

        if (groundPlane == null)
        {
            groundPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundPlane.name = "Plane001";
            groundPlane.transform.position = Vector3.zero;
            SceneManager.MoveGameObjectToScene(groundPlane, sandboxScene);
        }

        Vector3 planeScale = groundPlane.transform.localScale;
        groundPlane.transform.localScale = new Vector3(
            Mathf.Max(planeScale.x, SandboxGroundScale),
            planeScale.y <= 0f ? 1f : planeScale.y,
            Mathf.Max(planeScale.z, SandboxGroundScale));

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
        {
            groundPlane.layer = groundLayer;
        }
    }

    private static void ClearRuntimeTaskPoints()
    {
        TaskPoint[] taskPoints = Object.FindObjectsOfType<TaskPoint>();
        for (int i = taskPoints.Length - 1; i >= 0; i--)
        {
            if (taskPoints[i] != null)
            {
                Object.DestroyImmediate(taskPoints[i].gameObject);
            }
        }
    }
}
