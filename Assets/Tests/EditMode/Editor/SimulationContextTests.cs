using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SimulationContextTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();

        if (SimulationContext.Current != null)
        {
            Object.DestroyImmediate(SimulationContext.Current.gameObject);
        }
    }

    [Test]
    public void TaskPointRegistration_RaisesEventAndUpdatesSnapshot()
    {
        SimulationContext context = CreateContext();
        int changeCount = 0;
        context.TasksChanged += () => changeCount++;

        TaskPoint taskPoint = CreateGameObject("TaskPoint").AddComponent<TaskPoint>();
        context.RegisterTaskPoint(taskPoint);

        CollectionAssert.Contains(context.GetTaskPoints(), taskPoint);
        Assert.GreaterOrEqual(changeCount, 1);
    }

    [Test]
    public void UnregisterSpawnPoint_RemovesMarkerFromSnapshot()
    {
        SimulationContext context = CreateContext();
        int changeCount = 0;
        context.SpawnPointsChanged += () => changeCount++;

        GameObject markerObject = CreateGameObject("SpawnPoint");
        DroneSpawnPointMarker marker = markerObject.AddComponent<DroneSpawnPointMarker>();
        context.RegisterSpawnPoint(marker);

        CollectionAssert.Contains(context.GetSpawnPointMarkers(), marker);

        context.UnregisterSpawnPoint(marker);

        CollectionAssert.DoesNotContain(context.GetSpawnPointMarkers(), marker);
        Assert.GreaterOrEqual(changeCount, 2);
    }

    [Test]
    public void RuntimeObstacleRegistration_RaisesEventAndKeepsMarker()
    {
        SimulationContext context = CreateContext();
        int changeCount = 0;
        context.ObstaclesChanged += () => changeCount++;

        RuntimeObstacleMarker marker = CreateGameObject("RuntimeObstacle").AddComponent<RuntimeObstacleMarker>();
        marker.obstacleId = 7;
        context.RegisterObstacle(marker);

        RuntimeObstacleMarker[] markers = context.GetRuntimeObstacleMarkers();

        CollectionAssert.Contains(markers, marker);
        Assert.AreEqual(7, markers[0].obstacleId);
        Assert.GreaterOrEqual(changeCount, 1);
    }

    private SimulationContext CreateContext()
    {
        return CreateGameObject("SimulationContext").AddComponent<SimulationContext>();
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }
}
