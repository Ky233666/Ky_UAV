using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单次仿真的 JSON 明细导出根对象。
/// </summary>
[Serializable]
public class SimulationExperimentDetailExport
{
    public string experimentTime = "";
    public string exportTrigger = "";
    public string simulationState = "";
    public string schedulerAlgorithm = "";
    public string pathPlanner = "";
    public string cameraMode = "";
    public string cameraTarget = "";
    public string notes = "";
    public float elapsedSeconds;
    public int droneCount;
    public int onlineDroneCount;
    public int taskCount;
    public int completedTaskCount;
    public int pendingTaskCount;
    public int inProgressTaskCount;
    public float totalFlightDistance;
    public int totalWaitCount;
    public int totalConflictCount;
    public PlanningSettingsSnapshot planning = new PlanningSettingsSnapshot();
    public List<DroneExperimentDetail> drones = new List<DroneExperimentDetail>();
    public List<TaskPointExperimentDetail> tasks = new List<TaskPointExperimentDetail>();
}

[Serializable]
public class PlanningSettingsSnapshot
{
    public float gridCellSize;
    public bool allowDiagonal;
    public bool autoConfigureObstacles;
    public Vector3 worldMin;
    public Vector3 worldMax;
}

[Serializable]
public class DroneExperimentDetail
{
    public int droneId;
    public string droneName = "";
    public string state = "";
    public bool isOnline;
    public float speed;
    public int assignedTaskCount;
    public int currentTaskIndex;
    public int completedTasks;
    public float totalFlightDistance;
    public int waitCount;
    public int conflictCount;
    public string waitReason = "";
    public string lastConflictReason = "";
    public string plannerName = "";
    public Vector3 currentPosition;
    public Vector3 lastKnownPosition;
    public int currentWaypointIndex;
    public List<Vector3> plannedPath = new List<Vector3>();
}

[Serializable]
public class TaskPointExperimentDetail
{
    public int taskId;
    public string taskName = "";
    public string description = "";
    public int priority;
    public float estimatedDuration;
    public string state = "";
    public int assignedDroneId;
    public string assignedDroneName = "";
    public float startTime;
    public float completionTime;
    public Vector3 position;
}
