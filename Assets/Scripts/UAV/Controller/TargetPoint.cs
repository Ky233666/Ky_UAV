using UnityEngine;

/// <summary>
/// 目标点标记，用于被无人机识别
/// </summary>
public class TargetPoint : MonoBehaviour
{
    // 目标点编号（可选，用于多目标任务）
    public int pointId = 0;
    
    // 目标点名称（可选）
    public string pointName = "Target";
}
