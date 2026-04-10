using UnityEngine;

/// <summary>
/// 无人机仿真默认配置。
/// 运行时高频参数仍可通过面板覆盖；该资产负责提供默认值来源。
/// </summary>
[CreateAssetMenu(fileName = "DroneConfig", menuName = "KY UAV/Drone Config")]
public class DroneConfig : ScriptableObject
{
    [Header("Flight")]
    [Tooltip("默认飞行速度（米/秒）。")]
    public float defaultSpeed = 5f;

    [Tooltip("默认巡航高度（世界坐标 Y）。路径规划和目标点会使用该高度。")]
    public float cruiseHeight = 5f;

    [Header("Safety")]
    [Tooltip("无人机之间默认最小安全间距。")]
    public float minimumSeparation = 1.6f;

    [Tooltip("等待超时时间（秒）。")]
    public float waitTimeout = 10f;

    [Tooltip("触发局部避让的距离阈值。")]
    public float avoidanceTriggerDistance = 2.2f;

    [Tooltip("允许恢复移动前需要拉开的距离。")]
    public float avoidanceResumeDistance = 1.8f;

    [Header("Scheduling")]
    [Tooltip("单机最大任务容量。0 表示不限制。")]
    public int maxTaskCapacity = 0;

    [Tooltip("优先级权重。")]
    public float priorityWeight = 6f;

    [Tooltip("距离权重。")]
    public float distanceWeight = 1f;

    [Tooltip("负载权重。")]
    public float loadWeight = 4f;
}
