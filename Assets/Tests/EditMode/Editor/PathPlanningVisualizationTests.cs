using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PathPlanningVisualizationTests
{
    [Test]
    public void AStarPlanner_WithVisualizationRecorder_RecordsSearchFrames()
    {
        PathPlanningRequest request = new PathPlanningRequest
        {
            droneId = 1,
            startPosition = new Vector3(0f, 5f, 0f),
            targetPosition = new Vector3(4f, 5f, 0f),
            gridCellSize = 1f,
            worldMin = new Vector3(-1f, 0f, -1f),
            worldMax = new Vector3(6f, 10f, 1f),
            obstacleLayer = 0,
            allowDiagonal = false
        };

        PathPlanningVisualizationRecorder recorder = new PathPlanningVisualizationRecorder(
            request.droneId,
            "Drone 01",
            PathPlannerType.AStar,
            "AStar",
            "A*",
            Color.cyan,
            request);

        AStarPlanner planner = new AStarPlanner();
        PathPlanningResult result = planner.PlanPath(request, recorder);
        recorder.FinalizeResult(result);
        PathPlanningVisualizationTrace trace = recorder.BuildTrace();

        Assert.IsTrue(result.success);
        Assert.IsTrue(trace.HasSteps());
        Assert.AreEqual(PathPlanningVisualizationStepType.Initialize, trace.steps[0].stepType);
        Assert.IsTrue(trace.steps.Exists(step => step.stepType == PathPlanningVisualizationStepType.NodeExpanded));
        Assert.IsTrue(trace.steps.Exists(step => step.stepType == PathPlanningVisualizationStepType.BacktrackPathUpdated));
        Assert.IsTrue(trace.steps.Exists(step => step.stepType == PathPlanningVisualizationStepType.FinalPathConfirmed));
        Assert.AreEqual(result.waypoints.Count, trace.finalPath.Count);
    }

    [Test]
    public void RrtPlanner_WithVisualizationRecorder_RecordsDirectPathFrames()
    {
        PathPlanningRequest request = new PathPlanningRequest
        {
            droneId = 2,
            startPosition = new Vector3(1f, 5f, 1f),
            targetPosition = new Vector3(9f, 5f, 1f),
            gridCellSize = 1f,
            worldMin = new Vector3(0f, 0f, 0f),
            worldMax = new Vector3(10f, 10f, 10f),
            obstacleLayer = 0,
            allowDiagonal = true
        };

        PathPlanningVisualizationRecorder recorder = new PathPlanningVisualizationRecorder(
            request.droneId,
            "Drone 02",
            PathPlannerType.RRT,
            "RRT",
            "RRT",
            Color.yellow,
            request);

        RRTPlanner planner = new RRTPlanner();
        PathPlanningResult result = planner.PlanPath(request, recorder);
        recorder.FinalizeResult(result);
        PathPlanningVisualizationTrace trace = recorder.BuildTrace();

        Assert.IsTrue(result.success);
        Assert.AreEqual(2, result.waypoints.Count);
        Assert.IsTrue(trace.steps.Exists(step => step.stepType == PathPlanningVisualizationStepType.CandidatePathUpdated));
        Assert.IsTrue(trace.steps.Exists(step => step.stepType == PathPlanningVisualizationStepType.FinalPathConfirmed));
        Assert.IsTrue(trace.steps.Exists(step => step.stepType == PathPlanningVisualizationStepType.SearchFinished));
    }

    [Test]
    public void PlaybackRuntimeState_DemoteCurrentNodesToVisited_ReleasesPreviousHighlight()
    {
        AlgorithmVisualizerManager.PlaybackRuntimeState runtimeState = new AlgorithmVisualizerManager.PlaybackRuntimeState();
        runtimeState.SetNode(new PathPlanningVisualizationNodeState
        {
            position = Vector3.zero,
            role = PathPlanningVisualizationNodeRole.Current,
            label = "#1"
        });

        runtimeState.DemoteCurrentNodesToVisited();

        foreach (PathPlanningVisualizationNodeState node in runtimeState.Nodes)
        {
            Assert.AreEqual(PathPlanningVisualizationNodeRole.Visited, node.role);
            Assert.AreEqual(string.Empty, node.label);
        }
    }

    [Test]
    public void AlgorithmVisualizer_ResumeFromCompleted_RestartsPlayback()
    {
        GameObject managerObject = new GameObject("AlgorithmVisualizer");
        try
        {
            PathPlanningProcessRenderer renderer = managerObject.AddComponent<PathPlanningProcessRenderer>();
            AlgorithmVisualizerManager manager = managerObject.AddComponent<AlgorithmVisualizerManager>();
            manager.processRenderer = renderer;
            manager.visualizationMode = PathPlanningVisualizationMode.FullProcess;

            manager.RegisterPlanningTrace(CreateFinalPathTrace());
            manager.StepForward();
            manager.StepForward();

            Assert.AreEqual(PathPlanningVisualizationPlaybackState.Completed, manager.PlaybackState);

            manager.Resume();

            Assert.AreEqual(PathPlanningVisualizationPlaybackState.Playing, manager.PlaybackState);
            Assert.AreEqual("0 / 2", manager.GetCurrentStepLabel());
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void AlgorithmVisualizer_ReprojectsRenderedTrace_WhenProjectionModeChanges()
    {
        GameObject managerObject = new GameObject("AlgorithmVisualizer");
        GameObject cameraObject = new GameObject("CameraManager");
        try
        {
            PathPlanningProcessRenderer renderer = managerObject.AddComponent<PathPlanningProcessRenderer>();
            AlgorithmVisualizerManager manager = managerObject.AddComponent<AlgorithmVisualizerManager>();
            CameraManager cameraManager = cameraObject.AddComponent<CameraManager>();

            manager.processRenderer = renderer;
            manager.cameraManager = cameraManager;
            manager.visualizationMode = PathPlanningVisualizationMode.FinalResultOnly;

            manager.RegisterPlanningTrace(CreateFinalPathTrace());
            LineRenderer finalPathRenderer = FindRenderer(managerObject.transform, "FinalPath");
            Assert.NotNull(finalPathRenderer);
            Assert.IsTrue(finalPathRenderer.enabled);
            Assert.AreEqual(5f + renderer.markerOffsetY, finalPathRenderer.GetPosition(0).y, 0.001f);

            cameraManager.isTopDown2D = true;
            InvokePrivateUpdate(manager);

            Assert.AreEqual(12f + renderer.markerOffsetY, finalPathRenderer.GetPosition(0).y, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    private static PathPlanningVisualizationTrace CreateFinalPathTrace()
    {
        PathPlanningRequest request = new PathPlanningRequest
        {
            droneId = 7,
            startPosition = new Vector3(0f, 5f, 0f),
            targetPosition = new Vector3(4f, 5f, 0f)
        };

        List<Vector3> finalPath = new List<Vector3>
        {
            request.startPosition,
            request.targetPosition
        };

        PathPlanningVisualizationStep initializeStep = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.Initialize,
            "Initialize");
        initializeStep.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            request.startPosition,
            PathPlanningVisualizationNodeRole.Start));
        initializeStep.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            request.targetPosition,
            PathPlanningVisualizationNodeRole.Goal));

        PathPlanningVisualizationStep finalStep = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.FinalPathConfirmed,
            "Final path");
        finalStep.replaceFinalPath = true;
        finalStep.finalPath = finalPath;
        finalStep.markSearchComplete = true;
        finalStep.markSearchSucceeded = true;

        return new PathPlanningVisualizationTrace
        {
            droneId = request.droneId,
            droneName = "Drone 07",
            plannerType = PathPlannerType.AStar,
            plannerName = "AStar",
            plannerDisplayName = "A*",
            accentColor = Color.cyan,
            request = request,
            finalPath = finalPath,
            success = true,
            steps = new List<PathPlanningVisualizationStep>
            {
                initializeStep,
                finalStep
            }
        };
    }

    private static LineRenderer FindRenderer(Transform root, string childName)
    {
        Transform renderRoot = root.Find("__PathPlanningProcessRenderer");
        Assert.NotNull(renderRoot);
        Transform child = renderRoot.Find(childName);
        Assert.NotNull(child);
        return child.GetComponent<LineRenderer>();
    }

    private static void InvokePrivateUpdate(AlgorithmVisualizerManager manager)
    {
        typeof(AlgorithmVisualizerManager)
            .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, null);
    }
}
