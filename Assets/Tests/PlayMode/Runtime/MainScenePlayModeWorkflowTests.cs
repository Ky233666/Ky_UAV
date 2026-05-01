using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class MainScenePlayModeWorkflowTests
{
    private string exportDirectory;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return SceneManager.LoadSceneAsync("MainScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        exportDirectory = Path.Combine(
            Application.temporaryCachePath,
            "KyUAVPlayModeExports",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(exportDirectory);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (!string.IsNullOrWhiteSpace(exportDirectory) && Directory.Exists(exportDirectory))
        {
            Directory.Delete(exportDirectory, true);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator MainScene_RunCompleteExportAndObstacleCreationWorkflow()
    {
        SimulationManager simulationManager = UnityEngine.Object.FindObjectOfType<SimulationManager>();
        DroneManager droneManager = UnityEngine.Object.FindObjectOfType<DroneManager>();
        TaskPointSpawner spawner = UnityEngine.Object.FindObjectOfType<TaskPointSpawner>();
        TaskPointImporter importer = UnityEngine.Object.FindObjectOfType<TaskPointImporter>();
        SimulationResultExporter exporter = UnityEngine.Object.FindObjectOfType<SimulationResultExporter>();
        RuntimeObstacleEditor obstacleEditor = UnityEngine.Object.FindObjectOfType<RuntimeObstacleEditor>();
        CameraManager cameraManager = UnityEngine.Object.FindObjectOfType<CameraManager>();
        AlgorithmVisualizerManager algorithmVisualizerManager = UnityEngine.Object.FindObjectOfType<AlgorithmVisualizerManager>();

        Assert.NotNull(simulationManager);
        Assert.NotNull(droneManager);
        Assert.NotNull(spawner);
        Assert.NotNull(importer);
        Assert.NotNull(exporter);
        Assert.NotNull(obstacleEditor);
        Assert.NotNull(algorithmVisualizerManager);

        simulationManager.droneManager = droneManager;
        simulationManager.resultExporter = exporter;
        exporter.simulationManager = simulationManager;
        exporter.droneManager = droneManager;
        obstacleEditor.simulationManager = simulationManager;
        obstacleEditor.droneManager = droneManager;
        importer.spawner = spawner;

        SimulationContext context = SimulationContext.GetOrCreate(simulationManager);
        spawner.ClearAll();
        yield return null;
        context.RefreshTaskPoints();

        importer.ImportFromString(
            "taskId,taskName,x,z,priority,description\n" +
            "1,PlayMode A,8,8,2,playmode\n" +
            "2,PlayMode B,14,12,1,playmode");
        yield return null;

        TaskPoint[] importedTasks = context.GetTaskPoints();
        Assert.GreaterOrEqual(importedTasks.Length, 2);

        droneManager.RespawnDrones(2);
        yield return null;

        simulationManager.OnStartClicked();
        yield return null;
        yield return null;

        Assert.AreEqual(SimulationState.Running, simulationManager.currentState);
        AssertPlannedPathVisuals(droneManager);
        Assert.IsTrue(algorithmVisualizerManager.HasPlayableTrace());

        if (cameraManager != null)
        {
            cameraManager.SwitchToTopDown2D();
            yield return null;
            AssertPlannedPathVisualsProjected(droneManager);
        }

        CompleteAllTasks(context.GetTaskPoints());
        yield return null;

        AssertAllTasksCompleted(context.GetTaskPoints());

        exporter.organizeByDate = false;
        exporter.organizeBySession = false;
        Assert.IsTrue(exporter.SetCustomExportDirectory(exportDirectory, out string exportMessage), exportMessage);

        Assert.IsTrue(exporter.ExportCurrentResult("playmode-csv", false), exporter.LastExportMessage);
        string csvPath = exporter.LastExportPath;
        Assert.IsTrue(File.Exists(csvPath), csvPath);

        Assert.IsTrue(exporter.ExportCurrentResultAsJson("playmode-json", false), exporter.LastExportMessage);
        string jsonPath = exporter.LastExportPath;
        Assert.IsTrue(File.Exists(jsonPath), jsonPath);

        droneManager.pathPlannerType = PathPlannerType.StraightLine;
        InvokeCreateObstacle(obstacleEditor, new Bounds(new Vector3(32f, 5f, 32f), new Vector3(4f, 10f, 4f)));
        yield return null;

        Assert.AreEqual(PathPlannerType.AStar, droneManager.pathPlannerType);
        Assert.GreaterOrEqual(context.GetRuntimeObstacleMarkers().Length, 1);
    }

    private static void CompleteAllTasks(TaskPoint[] tasks)
    {
        foreach (TaskPoint task in tasks)
        {
            if (task == null || task.currentState == TaskState.Completed)
            {
                continue;
            }

            if (task.currentState == TaskState.Pending)
            {
                task.StartTask(null);
            }

            task.CompleteTask();
        }
    }

    private static void AssertAllTasksCompleted(TaskPoint[] tasks)
    {
        Assert.GreaterOrEqual(tasks.Length, 1);
        foreach (TaskPoint task in tasks)
        {
            Assert.NotNull(task);
            Assert.AreEqual(TaskState.Completed, task.currentState);
        }
    }

    private static void InvokeCreateObstacle(RuntimeObstacleEditor obstacleEditor, Bounds bounds)
    {
        MethodInfo createObstacleMethod = typeof(RuntimeObstacleEditor).GetMethod(
            "CreateObstacle",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(createObstacleMethod);
        createObstacleMethod.Invoke(obstacleEditor, new object[] { bounds });
    }

    private static void AssertPlannedPathVisuals(DroneManager droneManager)
    {
        bool foundVisualizedPath = false;
        foreach (DroneController drone in droneManager.drones)
        {
            if (drone == null)
            {
                continue;
            }

            DroneData data = droneManager.GetDroneData(drone.droneId);
            if (data == null || data.plannedPath == null || data.plannedPath.Count < 2)
            {
                continue;
            }

            DronePathVisualizer visualizer = drone.GetComponent<DronePathVisualizer>();
            Assert.NotNull(visualizer, $"Drone {drone.droneId} missing DronePathVisualizer");

            LineRenderer plannedPathRenderer = drone.transform.Find("PlannedPath")?.GetComponent<LineRenderer>();
            Assert.NotNull(plannedPathRenderer, $"Drone {drone.droneId} missing PlannedPath renderer");
            Assert.IsTrue(plannedPathRenderer.enabled, $"Drone {drone.droneId} PlannedPath renderer is disabled");
            Assert.GreaterOrEqual(plannedPathRenderer.positionCount, 2, $"Drone {drone.droneId} planned path is not rendered");
            foundVisualizedPath = true;
        }

        Assert.IsTrue(foundVisualizedPath, "No planned path renderer produced visible path points");
    }

    private static void AssertPlannedPathVisualsProjected(DroneManager droneManager)
    {
        float projectionHeight = droneManager.CalculatePathProjectionHeight();
        bool checkedProjectedPath = false;
        foreach (DroneController drone in droneManager.drones)
        {
            if (drone == null)
            {
                continue;
            }

            LineRenderer plannedPathRenderer = drone.transform.Find("PlannedPath")?.GetComponent<LineRenderer>();
            if (plannedPathRenderer == null || plannedPathRenderer.positionCount == 0)
            {
                continue;
            }

            Assert.GreaterOrEqual(
                plannedPathRenderer.GetPosition(0).y,
                projectionHeight,
                $"Drone {drone.droneId} planned path was not projected above the scene");
            checkedProjectedPath = true;
        }

        Assert.IsTrue(checkedProjectedPath, "No planned path renderer was available for projection validation");
    }
}
