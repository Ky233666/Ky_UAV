using UnityEngine;

/// <summary>
/// Spawns task points during runtime.
/// </summary>
public class TaskPointSpawner : MonoBehaviour
{
    [Header("Task Point Prefab")]
    public GameObject taskPointPrefab;

    [Header("Hierarchy")]
    public Transform parentContainer;

    [Header("Safe Spawn")]
    [Tooltip("Automatically avoid buildings and other obstacles when spawning task points.")]
    public bool avoidObstaclesOnSpawn = true;

    [Tooltip("Safety check radius for each task point.")]
    public float safetyCheckRadius = 1.25f;

    [Tooltip("Maximum retry count when the original location is blocked.")]
    public int maxSpawnAttempts = 24;

    [Tooltip("Retry radius around the blocked target point.")]
    public float relocationRadius = 8f;

    [Tooltip("Height offset above ground for spawned task points.")]
    public float spawnHeightOffset = 0.25f;

    private int currentId = 1;

    public TaskPoint SpawnTaskPoint(Vector3 position)
    {
        if (taskPointPrefab == null)
        {
            Debug.LogError("[TaskPointSpawner] 未设置 TaskPoint Prefab");
            return null;
        }

        Vector3 finalPosition = ResolveSpawnPosition(position);
        GameObject go = Instantiate(taskPointPrefab, finalPosition, Quaternion.identity);

        if (parentContainer != null)
        {
            go.transform.SetParent(parentContainer);
        }

        go.name = $"TaskPoint_{currentId}";

        TaskPoint taskPoint = go.GetComponent<TaskPoint>();
        if (taskPoint != null)
        {
            taskPoint.taskId = currentId;
            taskPoint.taskName = $"巡检点 {currentId}";
            currentId++;
            SimulationContext.GetOrCreate(this).RegisterTaskPoint(taskPoint);
        }

        Debug.Log($"[TaskPointSpawner] 创建任务点: {go.name} at {finalPosition}");
        return taskPoint;
    }

    public void SpawnTaskPoints(int count, float spacing)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 position = transform.position + new Vector3(i * spacing, 0f, 0f);
            SpawnTaskPoint(position);
        }
    }

    public void ClearAll()
    {
        SimulationContext context = SimulationContext.GetOrCreate(this);

        if (parentContainer != null)
        {
            foreach (Transform child in parentContainer)
            {
                TaskPoint taskPoint = child != null ? child.GetComponent<TaskPoint>() : null;
                if (taskPoint != null)
                {
                    context.UnregisterTaskPoint(taskPoint, false);
                }

                Destroy(child.gameObject);
            }
        }

        TaskPoint[] allTaskPoints = context.GetTaskPoints();
        int cleared = 0;
        foreach (TaskPoint taskPoint in allTaskPoints)
        {
            if (taskPointPrefab != null && taskPoint.gameObject == taskPointPrefab)
            {
                continue;
            }

            context.UnregisterTaskPoint(taskPoint, false);
            Destroy(taskPoint.gameObject);
            cleared++;
        }

        context.NotifyTasksChanged();
        Debug.Log($"[TaskPointSpawner] 已清除 {cleared} 个任务点");
    }

    public bool IsPlacementSafe(Vector3 position)
    {
        LayerMask obstacleLayer = GetObstacleLayer();
        if (obstacleLayer.value == 0)
        {
            return true;
        }

        return IsPositionSafe(GetGroundedPosition(position), obstacleLayer);
    }

    public Vector3 GetGroundedPosition(Vector3 position)
    {
        return new Vector3(position.x, GetSpawnHeight(), position.z);
    }

    private Vector3 ResolveSpawnPosition(Vector3 desiredPosition)
    {
        Vector3 groundedDesiredPosition = GetGroundedPosition(desiredPosition);
        if (!avoidObstaclesOnSpawn)
        {
            return groundedDesiredPosition;
        }

        LayerMask obstacleLayer = GetObstacleLayer();
        if (obstacleLayer.value == 0)
        {
            return groundedDesiredPosition;
        }

        if (IsPositionSafe(groundedDesiredPosition, obstacleLayer))
        {
            return groundedDesiredPosition;
        }

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle * relocationRadius;
            Vector3 candidate = new Vector3(
                desiredPosition.x + offset.x,
                GetSpawnHeight(),
                desiredPosition.z + offset.y);

            if (IsPositionSafe(candidate, obstacleLayer))
            {
                Debug.Log($"[TaskPointSpawner] 原始任务点位置位于障碍区域，已重定位到安全位置: {candidate}");
                return candidate;
            }
        }

        Debug.LogWarning("[TaskPointSpawner] 未找到安全任务点位置，本次创建将保留原始位置，请检查场景障碍物布局");
        return groundedDesiredPosition;
    }

    private LayerMask GetObstacleLayer()
    {
        if (DroneManager.Instance != null && DroneManager.Instance.planningObstacleLayer.value != 0)
        {
            return DroneManager.Instance.planningObstacleLayer;
        }

        int buildingLayer = LayerMask.NameToLayer("Building");
        if (buildingLayer < 0)
        {
            return 0;
        }

        return 1 << buildingLayer;
    }

    private bool IsPositionSafe(Vector3 position, LayerMask obstacleLayer)
    {
        Vector3 checkCenter = new Vector3(position.x, position.y + safetyCheckRadius, position.z);
        return !Physics.CheckSphere(checkCenter, safetyCheckRadius, obstacleLayer, QueryTriggerInteraction.Ignore);
    }

    private float GetSpawnHeight()
    {
        return spawnHeightOffset;
    }
}
