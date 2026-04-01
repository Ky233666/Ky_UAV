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

        GameObject go = Instantiate(taskPointPrefab, position, Quaternion.identity);

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

        Debug.Log($"[TaskPointSpawner] 创建任务点: {go.name} at {position}");
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
}
