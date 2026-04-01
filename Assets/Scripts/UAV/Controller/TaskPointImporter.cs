using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 任务点导入器：从文件加载任务点数据
/// </summary>
public class TaskPointImporter : MonoBehaviour
{
    [Header("任务点生成器")]
    public TaskPointSpawner spawner;

    [Header("导入设置")]
    [Tooltip("Resources 文件夹中的文件名（不含扩展名）")]
    public string fileName = "taskpoints";

    /// <summary>
    /// 从 Resources 导入任务点（默认方式）
    /// </summary>
    public void ImportFromResources()
    {
        ImportFromResources(fileName);
    }

    /// <summary>
    /// 从指定文件导入
    /// </summary>
    public void ImportFromResources(string name)
    {
        if (spawner == null)
        {
            Debug.LogError("[TaskPointImporter] 未设置 Spawner");
            return;
        }

        TextAsset textAsset = Resources.Load<TextAsset>(name);
        if (textAsset == null)
        {
            Debug.LogError($"[TaskPointImporter] 文件不存在: {name}");
            return;
        }

        string content = textAsset.text;
        ImportFromString(content);
    }

    /// <summary>
    /// 从字符串内容导入（支持 CSV 或 JSON）
    /// </summary>
    public void ImportFromString(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            Debug.LogWarning("[TaskPointImporter] 文件内容为空");
            return;
        }

        content = content.Trim();

        // 自动判断格式
        List<TaskPointData> taskPoints;

        if (content.StartsWith("["))
        {
            // JSON 格式
            taskPoints = ParseJson(content);
        }
        else
        {
            // CSV 格式
            taskPoints = ParseCsv(content);
        }

        if (taskPoints == null || taskPoints.Count == 0)
        {
            Debug.LogWarning("[TaskPointImporter] 未解析到任何任务点");
            return;
        }

        // 生成任务点
        int successCount = 0;
        foreach (var data in taskPoints)
        {
            TaskPoint tp = spawner.SpawnTaskPoint(data.ToPosition());
            if (tp != null)
            {
                tp.taskId = data.taskId;
                tp.taskName = data.taskName;
                tp.priority = data.priority;
                tp.description = data.description;
                successCount++;
            }
        }

        Debug.Log($"[TaskPointImporter] 成功导入 {successCount} 个任务点");
    }

    /// <summary>
    /// 从绝对路径导入（测试用）
    /// </summary>
    public void ImportFromPath(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[TaskPointImporter] 文件不存在: {fullPath}");
            return;
        }

        string content = File.ReadAllText(fullPath);
        ImportFromString(content);
    }

    // ========== CSV 解析 ==========

    private List<TaskPointData> ParseCsv(string content)
    {
        var result = new List<TaskPointData>();
        string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // 跳过表头
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] fields = ParseCsvLine(line);
            if (fields.Length >= 3) // 至少需要 id, name, x
            {
                result.Add(TaskPointData.FromCsvRow(fields));
            }
        }

        return result;
    }

    private string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        string current = "";

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }
        fields.Add(current);

        return fields.ToArray();
    }

    // ========== JSON 解析 ==========

    private List<TaskPointData> ParseJson(string content)
    {
        var result = new List<TaskPointData>();

        try
        {
            // 简单 JSON 解析（不依赖 Newtonsoft）
            // 支持两种格式：
            // 1. { "taskPoints": [ {...}, {...} ] }
            // 2. [ {...}, {...} ]

            if (content.StartsWith("["))
            {
                // 数组格式
                result.AddRange(ParseJsonArray(content));
            }
            else
            {
                // 对象格式
                var root = JsonUtility.FromJson<TaskPointJsonRoot>(content);
                if (root != null && root.taskPoints != null)
                {
                    result.AddRange(root.taskPoints);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TaskPointImporter] JSON 解析失败: {e.Message}");
        }

        return result;
    }

    private List<TaskPointData> ParseJsonArray(string jsonArray)
    {
        var result = new List<TaskPointData>();

        // 简单解析：逐个提取对象
        int start = jsonArray.IndexOf('{');
        while (start >= 0)
        {
            int end = jsonArray.IndexOf('}', start);
            if (end < 0) break;

            string objStr = jsonArray.Substring(start, end - start + 1);
            var data = JsonUtility.FromJson<TaskPointData>(objStr);
            if (data != null)
            {
                result.Add(data);
            }

            start = jsonArray.IndexOf('{', end);
        }

        return result;
    }
}
