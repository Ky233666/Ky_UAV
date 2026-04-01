using UnityEngine;

/// <summary>
/// 任务点数据（用于导入/导出）
/// </summary>
[System.Serializable]
public class TaskPointData
{
    /// <summary>
    /// 任务点 ID
    /// </summary>
    public int taskId;

    /// <summary>
    /// 任务点名称
    /// </summary>
    public string taskName = "巡检点";

    /// <summary>
    /// X 坐标
    /// </summary>
    public float x;

    /// <summary>
    /// Z 坐标（Y 默认为 0）
    /// </summary>
    public float z;

    /// <summary>
    /// 优先级
    /// </summary>
    public int priority;

    /// <summary>
    /// 任务描述
    /// </summary>
    public string description = "";

    /// <summary>
    /// 从字符串数组构造（CSV 解析用）
    /// </summary>
    public static TaskPointData FromCsvRow(string[] fields)
    {
        var data = new TaskPointData();

        if (fields.Length >= 1) int.TryParse(fields[0], out data.taskId);
        if (fields.Length >= 2) data.taskName = fields[1].Trim();
        if (fields.Length >= 3) float.TryParse(fields[2], out data.x);
        if (fields.Length >= 4) float.TryParse(fields[3], out data.z);
        if (fields.Length >= 5) int.TryParse(fields[4], out data.priority);
        if (fields.Length >= 6) data.description = fields[5].Trim();

        return data;
    }

    /// <summary>
    /// 转换为世界坐标
    /// </summary>
    public Vector3 ToPosition()
    {
        return new Vector3(x, 0, z);
    }
}

/// <summary>
/// JSON 格式根对象
/// </summary>
[System.Serializable]
public class TaskPointJsonRoot
{
    public TaskPointData[] taskPoints;
}
