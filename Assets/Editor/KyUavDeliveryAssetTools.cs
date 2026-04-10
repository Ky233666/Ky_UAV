using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class KyUavDeliveryAssetTools
{
    private const string MainScenePath = "Assets/Scenes/Main/MainScene.unity";
    private const string DroneConfigAssetPath = "Assets/Resources/Configs/DroneConfig_Default.asset";
    private const string PresetRootDirectory = "Assets/Resources/ExperimentPresets";

    [MenuItem("Tools/KY UAV/Bootstrap Delivery Assets")]
    public static void BootstrapDeliveryAssetsMenu()
    {
        BootstrapDeliveryAssets();
    }

    public static void BootstrapDeliveryAssets()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Configs");
        EnsureFolder(PresetRootDirectory);

        DroneConfig droneConfig = EnsureDroneConfig();
        GenerateExperimentPresets(droneConfig);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[KyUavDeliveryAssetTools] 已完成交付资产初始化。");
    }

    private static DroneConfig EnsureDroneConfig()
    {
        DroneConfig config = AssetDatabase.LoadAssetAtPath<DroneConfig>(DroneConfigAssetPath);
        if (config != null)
        {
            return config;
        }

        config = ScriptableObject.CreateInstance<DroneConfig>();
        AssetDatabase.CreateAsset(config, DroneConfigAssetPath);
        EditorUtility.SetDirty(config);
        return config;
    }

    private static void GenerateExperimentPresets(DroneConfig droneConfig)
    {
        SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            DroneManager droneManager = Object.FindObjectOfType<DroneManager>();
            GameObject buildings = GameObject.Find("Buildings");

            Vector3 mediumMin = droneManager != null ? droneManager.planningWorldMin : new Vector3(-20f, 0f, -20f);
            Vector3 mediumMax = droneManager != null ? droneManager.planningWorldMax : new Vector3(80f, 10f, 80f);
            ComputeDensityBounds(buildings, mediumMin, mediumMax, out Vector3 sparseMin, out Vector3 sparseMax, out Vector3 denseMin, out Vector3 denseMax);

            List<ExperimentPresetDefinition> definitions = new List<ExperimentPresetDefinition>
            {
                new ExperimentPresetDefinition("Scheduling/EvenSplit_AStar_4", "Scheduling EvenSplit", "scheduling", "schedule-even", 5, 4, SchedulerAlgorithmType.EvenSplit, PathPlannerType.AStar, mediumMin, mediumMax),
                new ExperimentPresetDefinition("Scheduling/GreedyNearest_AStar_4", "Scheduling GreedyNearest", "scheduling", "schedule-greedy", 5, 4, SchedulerAlgorithmType.GreedyNearest, PathPlannerType.AStar, mediumMin, mediumMax),
                new ExperimentPresetDefinition("Scheduling/PriorityGreedy_AStar_4", "Scheduling PriorityGreedy", "scheduling", "schedule-priority", 5, 4, SchedulerAlgorithmType.PriorityGreedy, PathPlannerType.AStar, mediumMin, mediumMax),
                new ExperimentPresetDefinition("Planning/StraightLine_PriorityGreedy_4", "Planning StraightLine", "planning", "planner-straight", 5, 4, SchedulerAlgorithmType.PriorityGreedy, PathPlannerType.StraightLine, mediumMin, mediumMax),
                new ExperimentPresetDefinition("Planning/AStar_PriorityGreedy_4", "Planning AStar", "planning", "planner-astar", 5, 4, SchedulerAlgorithmType.PriorityGreedy, PathPlannerType.AStar, mediumMin, mediumMax),
                new ExperimentPresetDefinition("Planning/RRT_PriorityGreedy_4", "Planning RRT", "planning", "planner-rrt", 5, 4, SchedulerAlgorithmType.PriorityGreedy, PathPlannerType.RRT, mediumMin, mediumMax),
                new ExperimentPresetDefinition("Scaling/Fleet_2", "Scaling 2 UAV", "scaling", "fleet-02", 5, 2, SchedulerAlgorithmType.PriorityGreedy, PathPlannerType.AStar, mediumMin, mediumMax),
                new ExperimentPresetDefinition("Scaling/Fleet_4", "Scaling 4 UAV", "scaling", "fleet-04", 5, 4, SchedulerAlgorithmType.PriorityGreedy, PathPlannerType.AStar, mediumMin, mediumMax),
                new ExperimentPresetDefinition("Scaling/Fleet_6", "Scaling 6 UAV", "scaling", "fleet-06", 5, 6, SchedulerAlgorithmType.PriorityGreedy, PathPlannerType.AStar, mediumMin, mediumMax),
                new ExperimentPresetDefinition("Scaling/Fleet_8", "Scaling 8 UAV", "scaling", "fleet-08", 5, 8, SchedulerAlgorithmType.PriorityGreedy, PathPlannerType.AStar, mediumMin, mediumMax),
                new ExperimentPresetDefinition("Density/Sparse", "Density Sparse", "density", "density-sparse", 5, 4, SchedulerAlgorithmType.PriorityGreedy, PathPlannerType.AStar, sparseMin, sparseMax),
                new ExperimentPresetDefinition("Density/Medium", "Density Medium", "density", "density-medium", 5, 4, SchedulerAlgorithmType.PriorityGreedy, PathPlannerType.AStar, mediumMin, mediumMax),
                new ExperimentPresetDefinition("Density/Dense", "Density Dense", "density", "density-dense", 5, 4, SchedulerAlgorithmType.PriorityGreedy, PathPlannerType.AStar, denseMin, denseMax)
            };

            foreach (ExperimentPresetDefinition definition in definitions)
            {
                EnsurePreset(definition);
            }
        }
        finally
        {
            if (HasRestorableSceneSetup(previousScenes))
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }
    }

    private static void ComputeDensityBounds(
        GameObject buildings,
        Vector3 mediumMin,
        Vector3 mediumMax,
        out Vector3 sparseMin,
        out Vector3 sparseMax,
        out Vector3 denseMin,
        out Vector3 denseMax)
    {
        Vector3 mediumCenter = (mediumMin + mediumMax) * 0.5f;
        Vector3 mediumSize = mediumMax - mediumMin;

        Bounds referenceBounds = new Bounds(mediumCenter, mediumSize);
        if (buildings != null)
        {
            Renderer[] renderers = buildings.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                referenceBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    referenceBounds.Encapsulate(renderers[i].bounds);
                }
            }
        }

        Vector3 center = new Vector3(referenceBounds.center.x, mediumCenter.y, referenceBounds.center.z);
        Vector3 sparseSize = new Vector3(
            Mathf.Clamp(Mathf.Max(mediumSize.x * 1.35f, referenceBounds.size.x + 12f), mediumSize.x, 180f),
            mediumSize.y,
            Mathf.Clamp(Mathf.Max(mediumSize.z * 1.35f, referenceBounds.size.z + 12f), mediumSize.z, 180f));
        Vector3 denseSize = new Vector3(
            Mathf.Clamp(mediumSize.x * 0.65f, 28f, mediumSize.x),
            mediumSize.y,
            Mathf.Clamp(mediumSize.z * 0.65f, 28f, mediumSize.z));

        sparseMin = center - sparseSize * 0.5f;
        sparseMax = center + sparseSize * 0.5f;
        denseMin = center - denseSize * 0.5f;
        denseMax = center + denseSize * 0.5f;

        sparseMin.y = mediumMin.y;
        sparseMax.y = mediumMax.y;
        denseMin.y = mediumMin.y;
        denseMax.y = mediumMax.y;
    }

    private static ExperimentPreset EnsurePreset(ExperimentPresetDefinition definition)
    {
        string directory = Path.Combine(PresetRootDirectory, definition.relativeDirectory).Replace("\\", "/");
        EnsureFolder(directory);

        string assetPath = $"{directory}/{definition.assetName}.asset";
        ExperimentPreset preset = AssetDatabase.LoadAssetAtPath<ExperimentPreset>(assetPath);
        if (preset == null)
        {
            preset = ScriptableObject.CreateInstance<ExperimentPreset>();
            AssetDatabase.CreateAsset(preset, assetPath);
        }

        preset.presetName = definition.presetName;
        preset.groupName = definition.groupName;
        preset.notePrefix = definition.notePrefix;
        preset.batchRuns = definition.batchRuns;
        preset.droneCount = definition.droneCount;
        preset.scheduler = definition.scheduler;
        preset.planner = definition.planner;
        preset.planningWorldMin = definition.worldMin;
        preset.planningWorldMax = definition.worldMax;
        EditorUtility.SetDirty(preset);
        return preset;
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

    private static bool HasRestorableSceneSetup(SceneSetup[] sceneSetup)
    {
        if (sceneSetup == null || sceneSetup.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < sceneSetup.Length; i++)
        {
            if (sceneSetup[i].isLoaded)
            {
                return true;
            }
        }

        return false;
    }

    private readonly struct ExperimentPresetDefinition
    {
        public readonly string relativePath;
        public readonly string presetName;
        public readonly string groupName;
        public readonly string notePrefix;
        public readonly int batchRuns;
        public readonly int droneCount;
        public readonly SchedulerAlgorithmType scheduler;
        public readonly PathPlannerType planner;
        public readonly Vector3 worldMin;
        public readonly Vector3 worldMax;

        public string relativeDirectory => Path.GetDirectoryName(relativePath)?.Replace("\\", "/") ?? string.Empty;
        public string assetName => Path.GetFileName(relativePath);

        public ExperimentPresetDefinition(
            string relativePath,
            string presetName,
            string groupName,
            string notePrefix,
            int batchRuns,
            int droneCount,
            SchedulerAlgorithmType scheduler,
            PathPlannerType planner,
            Vector3 worldMin,
            Vector3 worldMax)
        {
            this.relativePath = relativePath;
            this.presetName = presetName;
            this.groupName = groupName;
            this.notePrefix = notePrefix;
            this.batchRuns = batchRuns;
            this.droneCount = droneCount;
            this.scheduler = scheduler;
            this.planner = planner;
            this.worldMin = worldMin;
            this.worldMax = worldMax;
        }
    }
}
