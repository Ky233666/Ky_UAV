using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central runtime index for scene objects that are frequently queried by simulation systems.
/// </summary>
public sealed class SimulationContext : MonoBehaviour
{
    private const string ContextObjectName = "__SimulationContext";

    private static SimulationContext current;

    private readonly List<TaskPoint> taskPoints = new List<TaskPoint>();
    private readonly List<DroneSpawnPointMarker> spawnPointMarkers = new List<DroneSpawnPointMarker>();
    private readonly List<RuntimeObstacleMarker> runtimeObstacleMarkers = new List<RuntimeObstacleMarker>();

    private bool taskPointsInitialized;
    private bool spawnPointsInitialized;
    private bool obstaclesInitialized;

    public static SimulationContext Current => current;

    public event Action TasksChanged;
    public event Action SpawnPointsChanged;
    public event Action ObstaclesChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        current = null;
    }

    private void Awake()
    {
        if (current != null && current != this)
        {
            Destroy(gameObject);
            return;
        }

        current = this;
        RuntimeSceneRegistry.Register(this);
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }

    public static SimulationContext GetOrCreate(Component localContext = null)
    {
        SimulationContext context = RuntimeSceneRegistry.Get<SimulationContext>(localContext);
        if (context != null)
        {
            current = context;
            return RuntimeSceneRegistry.Register(context);
        }

        GameObject contextObject = new GameObject(ContextObjectName);
        context = contextObject.AddComponent<SimulationContext>();
        current = context;
        return RuntimeSceneRegistry.Register(context);
    }

    public void RefreshAll()
    {
        RefreshTaskPoints(false);
        RefreshSpawnPoints(false);
        RefreshObstacles(false);
    }

    public TaskPoint[] GetTaskPoints()
    {
        if (!taskPointsInitialized)
        {
            RefreshTaskPoints(false);
        }

        RemoveDestroyed(taskPoints);
        return taskPoints.ToArray();
    }

    public DroneSpawnPointMarker[] GetSpawnPointMarkers()
    {
        if (!spawnPointsInitialized)
        {
            RefreshSpawnPoints(false);
        }

        RemoveDestroyed(spawnPointMarkers);
        SortSpawnPointMarkers();
        return spawnPointMarkers.ToArray();
    }

    public RuntimeObstacleMarker[] GetRuntimeObstacleMarkers()
    {
        if (!obstaclesInitialized)
        {
            RefreshObstacles(false);
        }

        RemoveDestroyed(runtimeObstacleMarkers);
        return runtimeObstacleMarkers.ToArray();
    }

    public void RegisterTaskPoint(TaskPoint taskPoint, bool notify = true)
    {
        if (taskPoint == null)
        {
            return;
        }

        taskPointsInitialized = true;
        AddUnique(taskPoints, taskPoint);

        if (notify)
        {
            NotifyTasksChanged();
        }
    }

    public void UnregisterTaskPoint(TaskPoint taskPoint, bool notify = true)
    {
        if (taskPoint == null)
        {
            return;
        }

        taskPoints.Remove(taskPoint);

        if (notify)
        {
            NotifyTasksChanged();
        }
    }

    public void RegisterSpawnPoint(DroneSpawnPointMarker marker, bool notify = true)
    {
        if (marker == null)
        {
            return;
        }

        spawnPointsInitialized = true;
        AddUnique(spawnPointMarkers, marker);
        SortSpawnPointMarkers();

        if (notify)
        {
            NotifySpawnPointsChanged();
        }
    }

    public void UnregisterSpawnPoint(DroneSpawnPointMarker marker, bool notify = true)
    {
        if (marker == null)
        {
            return;
        }

        spawnPointMarkers.Remove(marker);

        if (notify)
        {
            NotifySpawnPointsChanged();
        }
    }

    public void RegisterObstacle(RuntimeObstacleMarker marker, bool notify = true)
    {
        if (marker == null)
        {
            return;
        }

        obstaclesInitialized = true;
        AddUnique(runtimeObstacleMarkers, marker);

        if (notify)
        {
            NotifyObstaclesChanged();
        }
    }

    public void UnregisterObstacle(RuntimeObstacleMarker marker, bool notify = true)
    {
        if (marker == null)
        {
            return;
        }

        runtimeObstacleMarkers.Remove(marker);

        if (notify)
        {
            NotifyObstaclesChanged();
        }
    }

    public void NotifyTasksChanged()
    {
        RemoveDestroyed(taskPoints);
        TasksChanged?.Invoke();
    }

    public void NotifySpawnPointsChanged()
    {
        RemoveDestroyed(spawnPointMarkers);
        SortSpawnPointMarkers();
        SpawnPointsChanged?.Invoke();
    }

    public void NotifyObstaclesChanged()
    {
        RemoveDestroyed(runtimeObstacleMarkers);
        ObstaclesChanged?.Invoke();
    }

    public void RefreshTaskPoints(bool notify = true)
    {
        taskPoints.Clear();
        taskPoints.AddRange(FindObjectsOfType<TaskPoint>());
        taskPointsInitialized = true;

        if (notify)
        {
            NotifyTasksChanged();
        }
    }

    public void RefreshSpawnPoints(bool notify = true)
    {
        spawnPointMarkers.Clear();
        spawnPointMarkers.AddRange(FindObjectsOfType<DroneSpawnPointMarker>());
        spawnPointsInitialized = true;
        SortSpawnPointMarkers();

        if (notify)
        {
            NotifySpawnPointsChanged();
        }
    }

    public void RefreshObstacles(bool notify = true)
    {
        runtimeObstacleMarkers.Clear();
        runtimeObstacleMarkers.AddRange(FindObjectsOfType<RuntimeObstacleMarker>());
        obstaclesInitialized = true;

        if (notify)
        {
            NotifyObstaclesChanged();
        }
    }

    private void SortSpawnPointMarkers()
    {
        spawnPointMarkers.Sort((left, right) =>
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int orderCompare = left.orderIndex.CompareTo(right.orderIndex);
            return orderCompare != 0 ? orderCompare : string.CompareOrdinal(left.name, right.name);
        });
    }

    private static void AddUnique<T>(List<T> items, T item) where T : UnityEngine.Object
    {
        RemoveDestroyed(items);
        if (!items.Contains(item))
        {
            items.Add(item);
        }
    }

    private static void RemoveDestroyed<T>(List<T> items) where T : UnityEngine.Object
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == null)
            {
                items.RemoveAt(i);
            }
        }
    }
}
