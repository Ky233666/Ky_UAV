using UnityEngine;

/// <summary>
/// 任务点生成器：运行时从代码/UI 创建任务点
/// </summary>
public class TaskPointSpawner : MonoBehaviour
{
    [Header("任务点 Prefab")]
    public GameObject taskPointPrefab;

    [Header("设置")]
    public Transform parentContainer;

    [Header("安全生成")]
    [Tooltip("生成任务点时自动避开建筑等障碍物")]
    public bool avoidObstaclesOnSpawn = true;

    [Tooltip("任务点安全检测半径")]
    public float safetyCheckRadius = 1.25f;

    [Tooltip("在原始目标点附近重新采样的最大尝试次数")]
    public int maxSpawnAttempts = 24;

    [Tooltip("当目标点无效时，围绕目标点重新采样的半径")]
    public float relocationRadius = 8f;

    [Tooltip("任务点生成时离地高度偏移")]
    public float spawnHeightOffset = 0.25f;

    [Header("自动编号")]
    private int currentId = 1;

    /// <summary>
    /// 在指定位置创建任务点
    /// </summary>
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
            go.transform.SetParent(parentContainer);

        go.name = $"TaskPoint_{currentId}";

        var taskPoint = go.GetComponent<TaskPoint>();
        if (taskPoint != null)
        {
            taskPoint.taskId = currentId;
            taskPoint.taskName = $"巡检点 {currentId}";
            currentId++;
        }

        Debug.Log($"[TaskPointSpawner] 创建任务点: {go.name} at {finalPosition}");
        return taskPoint;
    }

    /// <summary>
    /// 创建一行任务点（测试用）
    /// </summary>
    public void SpawnTaskPoints(int count, float spacing)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 position = transform.position + new Vector3(i * spacing, 0, 0);
            SpawnTaskPoint(position);
        }
    }

    /// <summary>
    /// 清除所有任务点
    /// </summary>
    public void ClearAll()
    {
        // 通过 parentContainer 清除
        if (parentContainer != null)
        {
            foreach (Transform child in parentContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // 清除场景中所有 TaskPoint 实例，但不要销毁 Prefab 引用本身
        var allTaskPoints = FindObjectsOfType<TaskPoint>();
        int cleared = 0;
        foreach (var tp in allTaskPoints)
        {
            // 若 Prefab 槽位拖的是场景里的对象，不要销毁它，否则引用会丢失
            if (taskPointPrefab != null && tp.gameObject == taskPointPrefab)
                continue;
            Destroy(tp.gameObject);
            cleared++;
        }

        Debug.Log($"[TaskPointSpawner] 已清除 {cleared} 个任务点");
    }

    private Vector3 ResolveSpawnPosition(Vector3 desiredPosition)
    {
        Vector3 groundedDesiredPosition = new Vector3(desiredPosition.x, GetSpawnHeight(), desiredPosition.z);
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
                desiredPosition.z + offset.y
            );

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
