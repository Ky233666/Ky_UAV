using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SimpleCitySceneImporter
{
    private const string MainScenePath = "Assets/Scenes/Main/MainScene.unity";
    private const string SourceScenePath = "Assets/SimpleCityPackage/Scene/Scene 01.unity";
    private const string BackupScenePath = "Assets/Scenes/Main/MainScene_Backup_PreSimpleCityImport.unity";

    [MenuItem("Tools/KY UAV/Import Simple City Into Main Scene")]
    public static void ImportSimpleCityIntoMainScene()
    {
        RunImport();
    }

    public static void ImportSimpleCityIntoMainSceneBatchMode()
    {
        RunImport();
    }

    private static void RunImport()
    {
        EnsureBackupSceneExists();

        Scene mainScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject sourceRoot = sourceScene.GetRootGameObjects().FirstOrDefault(root => root.name == "Static Game Object");
            if (sourceRoot == null)
            {
                throw new System.InvalidOperationException("未在 SimpleCityPackage 场景中找到 Static Game Object 根节点。");
            }

            RemoveExistingImportedRoots(mainScene);
            RenameAndDisableLegacyBuildings(mainScene);
            DisableLegacyGroundRenderer(mainScene);
            DisableSpawnAreaRenderer(mainScene);

            GameObject importedCity = Object.Instantiate(sourceRoot);
            importedCity.name = "CityEnvironment";
            SceneManager.MoveGameObjectToScene(importedCity, mainScene);
            importedCity.transform.SetPositionAndRotation(sourceRoot.transform.position, sourceRoot.transform.rotation);
            importedCity.transform.localScale = sourceRoot.transform.localScale;

            GameObject buildingsRoot = new GameObject("Buildings");
            SceneManager.MoveGameObjectToScene(buildingsRoot, mainScene);

            List<Transform> buildingCandidates = new List<Transform>();
            CollectBuildingCandidates(importedCity.transform, buildingCandidates);
            for (int i = 0; i < buildingCandidates.Count; i++)
            {
                buildingCandidates[i].SetParent(buildingsRoot.transform, true);
            }

            int buildingLayer = LayerMask.NameToLayer("Building");
            if (buildingLayer >= 0)
            {
                ApplyLayerRecursively(buildingsRoot, buildingLayer);
            }

            DroneManager droneManager = Object.FindObjectOfType<DroneManager>();
            if (droneManager != null)
            {
                droneManager.obstacleRoot = buildingsRoot.transform;
                droneManager.assignBuildingLayerRecursively = true;
                droneManager.generateObstacleProxyColliders = true;
                droneManager.autoConfigurePlanningObstacles = true;
                EditorUtility.SetDirty(droneManager);
            }

            EditorSceneManager.MarkSceneDirty(mainScene);
            EditorSceneManager.SaveScene(mainScene);

            Debug.Log($"[SimpleCitySceneImporter] 已将城市内容导入主场景。建筑根节点数: {buildingCandidates.Count}");
        }
        finally
        {
            EditorSceneManager.CloseScene(sourceScene, true);
        }
    }

    private static void EnsureBackupSceneExists()
    {
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupScenePath))
        {
            if (!AssetDatabase.CopyAsset(MainScenePath, BackupScenePath))
            {
                throw new System.InvalidOperationException("主场景备份失败。");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static void RemoveExistingImportedRoots(Scene mainScene)
    {
        foreach (GameObject root in mainScene.GetRootGameObjects())
        {
            if (root.name == "CityEnvironment" || root.name == "Buildings")
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static void RenameAndDisableLegacyBuildings(Scene mainScene)
    {
        GameObject legacyBuildings = mainScene.GetRootGameObjects().FirstOrDefault(root => root.name == "Buildings");
        if (legacyBuildings != null)
        {
            legacyBuildings.name = "LegacyBuildings";
            legacyBuildings.SetActive(false);
            EditorUtility.SetDirty(legacyBuildings);
        }
    }

    private static void DisableLegacyGroundRenderer(Scene mainScene)
    {
        GameObject ground = mainScene.GetRootGameObjects().FirstOrDefault(root => root.name == "Ground");
        if (ground == null)
        {
            return;
        }

        MeshRenderer renderer = ground.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void DisableSpawnAreaRenderer(Scene mainScene)
    {
        GameObject spawnArea = mainScene.GetRootGameObjects().FirstOrDefault(root => root.name == "SpawnArea");
        if (spawnArea == null)
        {
            return;
        }

        MeshRenderer[] renderers = spawnArea.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
            EditorUtility.SetDirty(renderers[i]);
        }
    }

    private static void CollectBuildingCandidates(Transform current, List<Transform> results)
    {
        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            if (ShouldTreatAsBuildingRoot(child.gameObject))
            {
                results.Add(child);
                continue;
            }

            CollectBuildingCandidates(child, results);
        }
    }

    private static bool ShouldTreatAsBuildingRoot(GameObject candidate)
    {
        string name = candidate.name.ToLowerInvariant();

        string[] explicitBuildingKeywords =
        {
            "building", "modular", "tower", "office", "hotel", "apartment", "mall", "shop", "store", "house", "block"
        };

        string[] explicitNonBuildingKeywords =
        {
            "road", "street", "sidewalk", "crosswalk", "lane", "park", "tree", "vegetation", "plant", "grass",
            "bush", "lamp", "light", "sign", "banner", "bench", "hydrant", "barrier", "fence", "pole", "billboard",
            "floor", "ground", "terrain", "camera", "flare", "sky"
        };

        if (explicitNonBuildingKeywords.Any(name.Contains))
        {
            return false;
        }

        if (explicitBuildingKeywords.Any(name.Contains))
        {
            return true;
        }

        Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.size.y >= 6f && bounds.size.x >= 3f && bounds.size.z >= 3f;
    }

    private static void ApplyLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            ApplyLayerRecursively(child.gameObject, layer);
        }
    }
}
