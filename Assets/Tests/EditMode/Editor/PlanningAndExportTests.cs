using NUnit.Framework;
using UnityEngine;

public class PlanningAndExportTests
{
    [Test]
    public void StraightLinePlanner_ReturnsStartAndTarget()
    {
        StraightLinePlanner planner = new StraightLinePlanner();
        PathPlanningResult result = planner.PlanPath(new PathPlanningRequest
        {
            startPosition = new Vector3(1f, 5f, 1f),
            targetPosition = new Vector3(5f, 5f, 9f)
        });

        Assert.IsTrue(result.success);
        Assert.AreEqual(2, result.waypoints.Count);
        Assert.AreEqual(new Vector3(1f, 5f, 1f), result.waypoints[0]);
        Assert.AreEqual(new Vector3(5f, 5f, 9f), result.waypoints[1]);
    }

    [Test]
    public void AStarPlanner_RejectsInvalidBounds()
    {
        AStarPlanner planner = new AStarPlanner();
        PathPlanningResult result = planner.PlanPath(new PathPlanningRequest
        {
            startPosition = Vector3.zero,
            targetPosition = Vector3.one,
            gridCellSize = 1f,
            worldMin = new Vector3(5f, 0f, 5f),
            worldMax = new Vector3(1f, 10f, 1f)
        });

        Assert.IsFalse(result.success);
        StringAssert.Contains("边界", result.message);
    }

    [Test]
    public void SimulationExperimentRecord_QuotesCsvFields()
    {
        SimulationExperimentRecord record = new SimulationExperimentRecord
        {
            experimentTime = "2026-04-10 10:00:00",
            exportTrigger = "manual",
            simulationState = "Running",
            notes = "alpha,beta",
            droneBreakdown = "[01]Moving tasks 1/2"
        };

        string row = record.ToCsvRow();

        StringAssert.Contains("\"alpha,beta\"", row);
        StringAssert.Contains("[01]Moving tasks 1/2", row);
    }
}
