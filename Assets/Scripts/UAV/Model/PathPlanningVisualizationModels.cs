using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 播放模式：仅最终结果或完整过程。
/// </summary>
public enum PathPlanningVisualizationMode
{
    FinalResultOnly = 0,
    FullProcess = 1,
    // Legacy value kept so older scene data can deserialize; runtime UI no longer exposes it.
    KeyMoments = 2
}

/// <summary>
/// 过程演示播放器状态。
/// </summary>
public enum PathPlanningVisualizationPlaybackState
{
    Ready = 0,
    Playing = 1,
    Paused = 2,
    Completed = 3
}

/// <summary>
/// 统一的可视化步骤事件类型。
/// </summary>
public enum PathPlanningVisualizationStepType
{
    Initialize = 0,
    NodeVisited = 1,
    NodeExpanded = 2,
    FrontierUpdated = 3,
    NodeClosed = 4,
    NodeRejected = 5,
    CandidatePathUpdated = 6,
    BacktrackPathUpdated = 7,
    FinalPathConfirmed = 8,
    SearchFinished = 9
}

/// <summary>
/// 节点在过程可视化中的角色。
/// </summary>
public enum PathPlanningVisualizationNodeRole
{
    Start = 0,
    Goal = 1,
    Frontier = 2,
    Visited = 3,
    Closed = 4,
    Current = 5,
    Rejected = 6,
    Blocked = 7
}

/// <summary>
/// 边/线段在过程可视化中的角色。
/// </summary>
public enum PathPlanningVisualizationEdgeRole
{
    Tree = 0,
    Candidate = 1,
    Rejected = 2
}

/// <summary>
/// 单个节点的可视化更新。
/// </summary>
[System.Serializable]
public class PathPlanningVisualizationNodeState
{
    public Vector3 position;
    public PathPlanningVisualizationNodeRole role;
    public int order;
    public float cost;
    public string label = string.Empty;

    public PathPlanningVisualizationNodeState Clone()
    {
        return new PathPlanningVisualizationNodeState
        {
            position = position,
            role = role,
            order = order,
            cost = cost,
            label = label
        };
    }
}

/// <summary>
/// 单条边的可视化更新。
/// </summary>
[System.Serializable]
public class PathPlanningVisualizationEdgeState
{
    public Vector3 from;
    public Vector3 to;
    public PathPlanningVisualizationEdgeRole role;
    public int order;
    public float weight;
    public string label = string.Empty;

    public PathPlanningVisualizationEdgeState Clone()
    {
        return new PathPlanningVisualizationEdgeState
        {
            from = from,
            to = to,
            role = role,
            order = order,
            weight = weight,
            label = label
        };
    }
}

/// <summary>
/// 路径规划过程中的一步。
/// </summary>
[System.Serializable]
public class PathPlanningVisualizationStep
{
    public int stepIndex;
    public PathPlanningVisualizationStepType stepType;
    public string description = string.Empty;
    public List<PathPlanningVisualizationNodeState> nodeUpdates = new List<PathPlanningVisualizationNodeState>();
    public List<PathPlanningVisualizationEdgeState> edgeUpdates = new List<PathPlanningVisualizationEdgeState>();
    public List<Vector3> candidatePath = new List<Vector3>();
    public List<Vector3> backtrackPath = new List<Vector3>();
    public List<Vector3> finalPath = new List<Vector3>();
    public bool replaceCandidatePath;
    public bool replaceBacktrackPath;
    public bool replaceFinalPath;
    public bool clearRejectedEdges;
    public bool markSearchComplete;
    public bool markSearchSucceeded;

    public PathPlanningVisualizationStep Clone()
    {
        PathPlanningVisualizationStep clone = new PathPlanningVisualizationStep
        {
            stepIndex = stepIndex,
            stepType = stepType,
            description = description,
            replaceCandidatePath = replaceCandidatePath,
            replaceBacktrackPath = replaceBacktrackPath,
            replaceFinalPath = replaceFinalPath,
            clearRejectedEdges = clearRejectedEdges,
            markSearchComplete = markSearchComplete,
            markSearchSucceeded = markSearchSucceeded
        };

        for (int i = 0; i < nodeUpdates.Count; i++)
        {
            clone.nodeUpdates.Add(nodeUpdates[i].Clone());
        }

        for (int i = 0; i < edgeUpdates.Count; i++)
        {
            clone.edgeUpdates.Add(edgeUpdates[i].Clone());
        }

        clone.candidatePath = new List<Vector3>(candidatePath);
        clone.backtrackPath = new List<Vector3>(backtrackPath);
        clone.finalPath = new List<Vector3>(finalPath);
        return clone;
    }
}

/// <summary>
/// 单次路径规划的完整可视化轨迹。
/// </summary>
[System.Serializable]
public class PathPlanningVisualizationTrace
{
    public int droneId;
    public string droneName = string.Empty;
    public PathPlannerType plannerType = PathPlannerType.StraightLine;
    public string plannerName = string.Empty;
    public string plannerDisplayName = string.Empty;
    public Color accentColor = Color.cyan;
    public PathPlanningRequest request;
    public List<PathPlanningVisualizationStep> steps = new List<PathPlanningVisualizationStep>();
    public List<Vector3> finalPath = new List<Vector3>();
    public bool success;
    public bool truncated;
    public string resultMessage = string.Empty;
    public int recordedStepLimit;

    public bool HasSteps()
    {
        return steps != null && steps.Count > 0;
    }
}
