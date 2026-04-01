using UnityEngine;

/// <summary>
/// 编辑器辅助：快速在场景中创建任务点
/// </summary>
public class TaskPointEditorHelper : MonoBehaviour
{
    [Header("任务点 Prefab")]
    public GameObject taskPointPrefab;

    [Header("创建设置")]
    public int startId = 1;
    public int count = 5;
    public float spacing = 10f;

    /// <summary>
    /// 在编辑器中创建一排任务点（仅用于开发调试）
    /// </summary>
    [ContextMenu("Create Task Points")]
    public void CreateTaskPoints()
    {
        if (taskPointPrefab == null)
        {
            Debug.LogError("[TaskPointEditorHelper] 请先拖入 TaskPoint Prefab");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 position = transform.position + new Vector3(i * spacing, 0, 0);
            GameObject go = Instantiate(taskPointPrefab, position, Quaternion.identity);
            go.name = $"TaskPoint_{startId + i}";

            var taskPoint = go.GetComponent<TaskPoint>();
            if (taskPoint != null)
            {
                taskPoint.taskId = startId + i;
                taskPoint.taskName = $"巡检点 {startId + i}";
            }
        }

        Debug.Log($"[TaskPointEditorHelper] 已创建 {count} 个任务点");
    }

    /// <summary>
    /// 清除所有子任务点
    /// </summary>
    [ContextMenu("Clear All Task Points")]
    public void ClearAllTaskPoints()
    {
        var allTaskPoints = FindObjectsOfType<TaskPoint>();
        foreach (var tp in allTaskPoints)
        {
            DestroyImmediate(tp.gameObject);
        }
        Debug.Log($"[TaskPointEditorHelper] 已清除 {allTaskPoints.Length} 个任务点");
    }
}
