using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SchedulingAlgorithmTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();

        if (SimulationContext.Current != null)
        {
            Object.DestroyImmediate(SimulationContext.Current.gameObject);
        }
    }

    [Test]
    public void PriorityGreedy_RespectsMaxTaskCapacity()
    {
        SchedulingRequest request = new SchedulingRequest
        {
            drones = new List<DroneData>
            {
                new DroneData { droneId = 1, droneName = "D1", lastKnownPosition = Vector3.zero, isOnline = true },
                new DroneData { droneId = 2, droneName = "D2", lastKnownPosition = Vector3.right * 10f, isOnline = true }
            },
            tasks = new List<TaskPoint>
            {
                CreateTaskPoint("T1", new Vector3(0f, 0f, 0f), 3),
                CreateTaskPoint("T2", new Vector3(2f, 0f, 0f), 2),
                CreateTaskPoint("T3", new Vector3(12f, 0f, 0f), 1)
            },
            maxTaskCapacity = 1,
            priorityWeight = 6f,
            distanceWeight = 1f,
            loadWeight = 4f
        };

        PriorityGreedyScheduler scheduler = new PriorityGreedyScheduler();
        SchedulingResult result = scheduler.ScheduleTasks(request);

        Assert.IsFalse(result.success);
        Assert.AreEqual(2, CountAssignedTasks(result));
    }

    private TaskPoint CreateTaskPoint(string name, Vector3 position, int priority)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        gameObject.transform.position = position;

        TaskPoint taskPoint = gameObject.AddComponent<TaskPoint>();
        taskPoint.taskName = name;
        taskPoint.priority = priority;
        return taskPoint;
    }

    private static int CountAssignedTasks(SchedulingResult result)
    {
        int count = 0;
        foreach (DroneTaskAssignment assignment in result.assignments)
        {
            if (assignment != null && assignment.assignedTasks != null)
            {
                count += assignment.assignedTasks.Count;
            }
        }

        return count;
    }
}
