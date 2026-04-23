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
}
