using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 任务点 UI 管理器
/// </summary>
public class TaskPointUIManager : MonoBehaviour
{
    [Header("任务点生成器")]
    public TaskPointSpawner spawner;

    [Header("任务点导入器")]
    public TaskPointImporter importer;

    [Header("UI 按钮")]
    public Button addTaskButton;
    public Button clearButton;
    public Button importButton;

    [Header("创建设置")]
    public float spawnRadius = 20f;

    void Start()
    {
        // 绑定按钮事件
        if (addTaskButton != null)
            addTaskButton.onClick.AddListener(OnAddTaskClicked);

        if (clearButton != null)
            clearButton.onClick.AddListener(OnClearClicked);

        if (importButton != null)
            importButton.onClick.AddListener(OnImportClicked);
    }

    /// <summary>
    /// 添加任务点按钮回调
    /// </summary>
    public void OnAddTaskClicked()
    {
        if (spawner == null)
        {
            Debug.LogWarning("[TaskPointUIManager] 未设置 Spawner");
            return;
        }

        // 在随机位置创建
        Vector3 randomPos = new Vector3(
            Random.Range(-spawnRadius, spawnRadius),
            0,
            Random.Range(-spawnRadius, spawnRadius)
        );

        spawner.SpawnTaskPoint(randomPos);
    }

    /// <summary>
    /// 清除所有任务点按钮回调
    /// </summary>
    public void OnClearClicked()
    {
        if (spawner != null)
            spawner.ClearAll();

        // 同时重置任务管理器
        if (TaskManager.Instance != null)
            TaskManager.Instance.ResetAllTasks();
    }

    /// <summary>
    /// 从文件导入任务点
    /// </summary>
    public void OnImportClicked()
    {
        if (importer == null)
        {
            Debug.LogWarning("[TaskPointUIManager] 未设置 Importer");
            return;
        }

        importer.ImportFromResources();
    }
}
