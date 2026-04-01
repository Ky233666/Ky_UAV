using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 任务管理器：统一管理所有任务点
/// </summary>
public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance { get; private set; }

    [Header("任务点列表")]
    public List<TaskPoint> allTasks = new List<TaskPoint>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        var foundTasks = FindObjectsOfType<TaskPoint>();
        allTasks = foundTasks.ToList();
        allTasks = allTasks.OrderByDescending(t => t.priority).ToList();

        Debug.Log($"[TaskManager] 发现 {allTasks.Count} 个任务点");
    }

    /// <summary>
    /// 获取下一个待执行的任务
    /// </summary>
    public TaskPoint GetNextPendingTask()
    {
        return allTasks.FirstOrDefault(t => t.currentState == TaskState.Pending);
    }

    /// <summary>
    /// 获取所有待执行任务
    /// </summary>
    public List<TaskPoint> GetAllPendingTasks()
    {
        return allTasks.Where(t => t.currentState == TaskState.Pending).ToList();
    }

    /// <summary>
    /// 分配任务给无人机
    /// </summary>
    public bool AssignTask(DroneController drone)
    {
        var task = GetNextPendingTask();
        if (task == null) return false;

        task.StartTask(drone);
        return true;
    }

    /// <summary>
    /// 重置所有任务
    /// </summary>
    public void ResetAllTasks()
    {
        foreach (var task in allTasks)
        {
            task.ResetTask();
        }
    }
}
