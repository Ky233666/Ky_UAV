using NUnit.Framework;

public class AlgorithmNameMappingTests
{
    [Test]
    public void SchedulerIdentifiers_AreStable()
    {
        Assert.AreEqual("EvenSplit", UAVAlgorithmNames.GetSchedulerIdentifier(SchedulerAlgorithmType.EvenSplit));
        Assert.AreEqual("GreedyNearest", UAVAlgorithmNames.GetSchedulerIdentifier(SchedulerAlgorithmType.GreedyNearest));
        Assert.AreEqual("PriorityGreedy", UAVAlgorithmNames.GetSchedulerIdentifier(SchedulerAlgorithmType.PriorityGreedy));
    }

    [Test]
    public void PlannerDisplayNames_AreStable()
    {
        Assert.AreEqual("直线", UAVAlgorithmNames.GetPlannerDisplayName(PathPlannerType.StraightLine));
        Assert.AreEqual("A*", UAVAlgorithmNames.GetPlannerDisplayName(PathPlannerType.AStar));
        Assert.AreEqual("RRT", UAVAlgorithmNames.GetPlannerDisplayName(PathPlannerType.RRT));
    }
}
