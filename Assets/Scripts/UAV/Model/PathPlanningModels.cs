using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 路径规划请求。
/// </summary>
[System.Serializable]
public class PathPlanningRequest
{
    /// <summary>
    /// 发起规划的无人机编号。
    /// </summary>
    public int droneId;

    /// <summary>
    /// 起点坐标。
    /// </summary>
    public Vector3 startPosition;

    /// <summary>
    /// 终点坐标。
    /// </summary>
    public Vector3 targetPosition;

    /// <summary>
    /// 规划网格尺寸，后续 A* 会直接使用。
    /// </summary>
    public float gridCellSize = 1f;

    /// <summary>
    /// 规划区域最小边界。
    /// </summary>
    public Vector3 worldMin = new Vector3(-50f, 0f, -50f);

    /// <summary>
    /// 规划区域最大边界。
    /// </summary>
    public Vector3 worldMax = new Vector3(50f, 0f, 50f);

    /// <summary>
    /// 静态障碍物层，用于后续碰撞检测。
    /// </summary>
    public LayerMask obstacleLayer;

    /// <summary>
    /// Cached occupancy grid used by path planners for fast obstacle queries.
    /// </summary>
    public PlanningGridMap planningMap;

    /// <summary>
    /// Horizontal safety margin applied around obstacle footprints.
    /// </summary>
    public float obstacleSafetyPadding = 0.75f;

    /// <summary>
    /// 是否允许对角线移动。
    /// </summary>
    public bool allowDiagonal = true;
}

/// <summary>
/// 路径规划结果。
/// </summary>
[System.Serializable]
public class PathPlanningResult
{
    /// <summary>
    /// 规划是否成功。
    /// </summary>
    public bool success;

    /// <summary>
    /// 使用的规划器名称。
    /// </summary>
    public string plannerName = "";

    /// <summary>
    /// 给调用方的附加说明。
    /// </summary>
    public string message = "";

    /// <summary>
    /// 最终路径点列表。
    /// </summary>
    public List<Vector3> waypoints = new List<Vector3>();

    /// <summary>
    /// 路径总代价。
    /// </summary>
    public float totalCost;

    /// <summary>
    /// 是否得到了一条可执行路径。
    /// </summary>
    public bool HasPath()
    {
        return success && waypoints != null && waypoints.Count > 0;
    }
}
