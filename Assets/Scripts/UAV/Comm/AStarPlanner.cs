using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基于二维网格的 A* 路径规划器。
/// 当前仅在 XZ 平面工作，用于静态障碍环境中的无人机航迹规划。
/// </summary>
public class AStarPlanner : IPathPlannerWithVisualization
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private static readonly Vector2Int[] DiagonalDirections =
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    public string PlannerName => "A* Grid";

    public bool SupportsDynamicReplan => true;

    public PathPlanningResult PlanPath(PathPlanningRequest request)
    {
        return PlanPath(request, null);
    }

    public PathPlanningResult PlanPath(
        PathPlanningRequest request,
        PathPlanningVisualizationRecorder recorder)
    {
        PathPlanningResult result = new PathPlanningResult
        {
            success = false,
            plannerName = PlannerName,
            message = "路径规划未执行"
        };

        if (request == null)
        {
            result.message = "路径规划请求为空";
            return result;
        }

        PathPlanningVisualizationBuilder.RecordInitialization(
            recorder,
            request,
            "A* 初始化完成，起点已建立，等待从 open list 选择首个扩展节点。");

        if (request.gridCellSize <= 0f)
        {
            result.message = "网格尺寸必须大于 0";
            PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, false, result.message);
            return result;
        }

        if (request.worldMax.x <= request.worldMin.x || request.worldMax.z <= request.worldMin.z)
        {
            result.message = "规划边界无效";
            PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, false, result.message);
            return result;
        }

        PlanningGridMap grid = request.planningMap != null && request.planningMap.IsValid
            ? request.planningMap
            : new PlanningGridMap(request.worldMin, request.worldMax, request.gridCellSize);
        Vector2Int start = grid.WorldToGrid(request.startPosition);
        Vector2Int goal = grid.WorldToGrid(request.targetPosition);

        if (!grid.IsWorldInside(request.startPosition) || !grid.IsWorldInside(request.targetPosition))
        {
            result.message = "起点或终点超出规划边界";
            PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, false, result.message);
            return result;
        }

        if (grid.IsBlocked(start) || grid.IsBlocked(goal))
        {
            result.message = "起点或终点位于规划障碍区域内";
            PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, false, result.message);
            return result;
        }

        List<NodeRecord> openList = new List<NodeRecord>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, NodeRecord> discovered = new Dictionary<Vector2Int, NodeRecord>();
        HashSet<string> blockedNodeKeys = new HashSet<string>();

        NodeRecord startNode = new NodeRecord(start)
        {
            gCost = 0f,
            hCost = Heuristic(start, goal, request.allowDiagonal)
        };

        openList.Add(startNode);
        discovered[start] = startNode;

        int expansionOrder = 0;
        int frontierOrder = 0;
        while (openList.Count > 0)
        {
            NodeRecord current = GetLowestCostNode(openList);
            expansionOrder++;
            RecordExpandedCurrent(recorder, current, grid, request, expansionOrder, openList.Count, closedSet.Count);

            if (current.position == goal)
            {
                List<Vector3> rawPath = BuildRawWaypoints(current, grid, request);
                result.waypoints = SimplifyWaypoints(
                    new List<Vector3>(rawPath),
                    grid);
                result.totalCost = current.gCost * request.gridCellSize;
                result.success = true;
                result.message = $"A* 规划成功，路径点数量：{result.waypoints.Count}";

                RecordBacktrack(recorder, rawPath, result.waypoints, expansionOrder);
                PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, true, result.message);
                return result;
            }

            openList.Remove(current);
            closedSet.Add(current.position);
            RecordClosedNode(recorder, current, grid, expansionOrder, openList.Count, closedSet.Count);

            foreach (Vector2Int direction in GetDirections(request.allowDiagonal))
            {
                Vector2Int neighborPos = current.position + direction;
                if (!grid.IsInside(neighborPos) || closedSet.Contains(neighborPos))
                {
                    continue;
                }

                Vector3 neighborWorld = grid.GridToWorld(neighborPos);
                if (direction.x != 0 && direction.y != 0 &&
                    IsDiagonalCornerCut(current.position, neighborPos, grid, request, start, goal))
                {
                    RecordRejectedNeighbor(
                        recorder,
                        current,
                        neighborWorld,
                        grid,
                        blockedNodeKeys,
                        "A* 拒绝穿角扩展，该候选点会穿过障碍拐角。",
                        PathPlanningVisualizationNodeRole.Blocked);
                    continue;
                }

                if (grid.IsBlocked(neighborPos))
                {
                    RecordRejectedNeighbor(
                        recorder,
                        current,
                        neighborWorld,
                        grid,
                        blockedNodeKeys,
                        "候选节点命中障碍物，当前扩展被放弃。",
                        PathPlanningVisualizationNodeRole.Blocked);
                    continue;
                }

                float stepCost = direction.x != 0 && direction.y != 0 ? 1.4142135f : 1f;
                float tentativeG = current.gCost + stepCost;

                if (!discovered.TryGetValue(neighborPos, out NodeRecord neighbor))
                {
                    neighbor = new NodeRecord(neighborPos);
                    discovered[neighborPos] = neighbor;
                }
                else if (tentativeG >= neighbor.gCost)
                {
                    continue;
                }

                NodeRecord previousParent = neighbor.parent;
                neighbor.parent = current;
                neighbor.gCost = tentativeG;
                neighbor.hCost = Heuristic(neighborPos, goal, request.allowDiagonal);

                if (!openList.Contains(neighbor))
                {
                    openList.Add(neighbor);
                }

                frontierOrder++;
                RecordFrontierUpdate(
                    recorder,
                    current,
                    previousParent,
                    neighbor,
                    grid,
                    request,
                    frontierOrder,
                    openList.Count,
                    closedSet.Count);
            }
        }

        result.message = "A* 未找到可达路径";
        PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, false, result.message);
        return result;
    }

    private static void RecordExpandedCurrent(
        PathPlanningVisualizationRecorder recorder,
        NodeRecord current,
        PlanningGridMap grid,
        PathPlanningRequest request,
        int expansionOrder,
        int openCount,
        int closedCount)
    {
        if (recorder == null || current == null)
        {
            return;
        }

        PathPlanningVisualizationStep step = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.NodeExpanded,
            $"A* 第 {expansionOrder} 次扩展：从 open list 取出节点 ({current.position.x}, {current.position.y})，" +
            $"f={current.FCost:0.00}，g={current.gCost:0.00}，当前 open={openCount}，closed={closedCount}。");
        step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            grid.GridToWorld(current.position),
            PathPlanningVisualizationNodeRole.Current,
            expansionOrder,
            current.FCost,
            $"#{expansionOrder}"));
        step.replaceCandidatePath = true;
        step.candidatePath = BuildPathToNode(current, grid, request);
        recorder.RecordStep(step);
    }

    private static void RecordClosedNode(
        PathPlanningVisualizationRecorder recorder,
        NodeRecord current,
        PlanningGridMap grid,
        int expansionOrder,
        int openCount,
        int closedCount)
    {
        if (recorder == null || current == null)
        {
            return;
        }

        PathPlanningVisualizationStep step = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.NodeClosed,
            $"节点 ({current.position.x}, {current.position.y}) 已完成扩展并进入 closed list，剩余 open={openCount}，closed={closedCount}。");
        step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            grid.GridToWorld(current.position),
            PathPlanningVisualizationNodeRole.Closed,
            expansionOrder,
            current.FCost));
        recorder.RecordStep(step);
    }

    private static void RecordFrontierUpdate(
        PathPlanningVisualizationRecorder recorder,
        NodeRecord current,
        NodeRecord previousParent,
        NodeRecord neighbor,
        PlanningGridMap grid,
        PathPlanningRequest request,
        int frontierOrder,
        int openCount,
        int closedCount)
    {
        if (recorder == null || current == null || neighbor == null)
        {
            return;
        }

        Vector3 currentWorld = grid.GridToWorld(current.position);
        Vector3 neighborWorld = grid.GridToWorld(neighbor.position);
        PathPlanningVisualizationStep step = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.CandidatePathUpdated,
            $"更新候选节点 ({neighbor.position.x}, {neighbor.position.y})：g={neighbor.gCost:0.00}，" +
            $"h={neighbor.hCost:0.00}，f={neighbor.FCost:0.00}。当前 open={openCount}，closed={closedCount}。");

        if (previousParent != null && previousParent.position != current.position)
        {
            step.edgeUpdates.Add(PathPlanningVisualizationBuilder.CreateEdge(
                grid.GridToWorld(previousParent.position),
                neighborWorld,
                PathPlanningVisualizationEdgeRole.Rejected,
                frontierOrder,
                previousParent.FCost,
                "旧父节点"));
        }

        step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            neighborWorld,
            PathPlanningVisualizationNodeRole.Frontier,
            frontierOrder,
            neighbor.FCost));
        step.edgeUpdates.Add(PathPlanningVisualizationBuilder.CreateEdge(
            currentWorld,
            neighborWorld,
            PathPlanningVisualizationEdgeRole.Tree,
            frontierOrder,
            neighbor.gCost));
        step.replaceCandidatePath = true;
        step.candidatePath = BuildPathToNode(neighbor, grid, request);
        recorder.RecordStep(step);
    }

    private static void RecordRejectedNeighbor(
        PathPlanningVisualizationRecorder recorder,
        NodeRecord current,
        Vector3 neighborWorld,
        PlanningGridMap grid,
        HashSet<string> blockedNodeKeys,
        string description,
        PathPlanningVisualizationNodeRole rejectedRole)
    {
        if (recorder == null || current == null)
        {
            return;
        }

        PathPlanningVisualizationStep step = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.NodeRejected,
            description);
        string key = BuildVectorKey(neighborWorld);
        if (blockedNodeKeys != null && blockedNodeKeys.Add(key))
        {
            step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
                neighborWorld,
                rejectedRole));
        }

        step.edgeUpdates.Add(PathPlanningVisualizationBuilder.CreateEdge(
            grid.GridToWorld(current.position),
            neighborWorld,
            PathPlanningVisualizationEdgeRole.Rejected));
        recorder.RecordStep(step);
    }

    private static void RecordBacktrack(
        PathPlanningVisualizationRecorder recorder,
        List<Vector3> rawPath,
        List<Vector3> finalPath,
        int expansionOrder)
    {
        if (recorder == null)
        {
            return;
        }

        PathPlanningVisualizationStep backtrackStep = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.BacktrackPathUpdated,
            $"已命中目标节点，开始沿父节点回溯生成路径，回溯节点数 {rawPath.Count}。");
        backtrackStep.replaceBacktrackPath = true;
        backtrackStep.backtrackPath = rawPath != null ? new List<Vector3>(rawPath) : new List<Vector3>();
        recorder.RecordStep(backtrackStep);

        PathPlanningVisualizationStep finalStep = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.FinalPathConfirmed,
            $"A* 已确认最终路径，扩展节点数 {expansionOrder}，最终路径点数 {finalPath.Count}。");
        finalStep.replaceFinalPath = true;
        finalStep.finalPath = finalPath != null ? new List<Vector3>(finalPath) : new List<Vector3>();
        recorder.RecordStep(finalStep);
    }

    private static IEnumerable<Vector2Int> GetDirections(bool allowDiagonal)
    {
        foreach (Vector2Int direction in CardinalDirections)
        {
            yield return direction;
        }

        if (!allowDiagonal)
        {
            yield break;
        }

        foreach (Vector2Int direction in DiagonalDirections)
        {
            yield return direction;
        }
    }

    private static NodeRecord GetLowestCostNode(List<NodeRecord> openList)
    {
        NodeRecord best = openList[0];
        for (int i = 1; i < openList.Count; i++)
        {
            NodeRecord candidate = openList[i];
            if (candidate.FCost < best.FCost ||
                (Mathf.Approximately(candidate.FCost, best.FCost) && candidate.hCost < best.hCost))
            {
                best = candidate;
            }
        }

        return best;
    }

    private static float Heuristic(Vector2Int from, Vector2Int to, bool allowDiagonal)
    {
        int dx = Mathf.Abs(from.x - to.x);
        int dz = Mathf.Abs(from.y - to.y);

        if (!allowDiagonal)
        {
            return dx + dz;
        }

        int diagonal = Mathf.Min(dx, dz);
        int straight = dx + dz - 2 * diagonal;
        return diagonal * 1.4142135f + straight;
    }

    private static bool IsDiagonalCornerCut(
        Vector2Int current,
        Vector2Int diagonalNeighbor,
        PlanningGridMap grid,
        PathPlanningRequest request,
        Vector2Int start,
        Vector2Int goal)
    {
        Vector2Int horizontal = new Vector2Int(diagonalNeighbor.x, current.y);
        Vector2Int vertical = new Vector2Int(current.x, diagonalNeighbor.y);

        if (!grid.IsInside(horizontal) || !grid.IsInside(vertical))
        {
            return true;
        }

        bool horizontalIsStartOrGoal = horizontal == start || horizontal == goal;
        bool verticalIsStartOrGoal = vertical == start || vertical == goal;

        bool horizontalBlocked = !horizontalIsStartOrGoal && grid.IsBlocked(horizontal);
        bool verticalBlocked = !verticalIsStartOrGoal && grid.IsBlocked(vertical);
        return horizontalBlocked || verticalBlocked;
    }

    private static List<Vector3> BuildRawWaypoints(
        NodeRecord goalNode,
        PlanningGridMap grid,
        PathPlanningRequest request)
    {
        List<Vector3> reversed = new List<Vector3>();
        NodeRecord current = goalNode;
        while (current != null)
        {
            reversed.Add(grid.GridToWorld(current.position));
            current = current.parent;
        }

        reversed.Reverse();

        if (reversed.Count == 0)
        {
            reversed.Add(request.startPosition);
            reversed.Add(request.targetPosition);
            return reversed;
        }

        reversed[0] = request.startPosition;
        reversed[reversed.Count - 1] = request.targetPosition;
        for (int i = 1; i < reversed.Count - 1; i++)
        {
            reversed[i] = new Vector3(reversed[i].x, request.startPosition.y, reversed[i].z);
        }

        return reversed;
    }

    private static List<Vector3> BuildPathToNode(
        NodeRecord node,
        PlanningGridMap grid,
        PathPlanningRequest request)
    {
        if (node == null)
        {
            return new List<Vector3>();
        }

        List<Vector3> path = new List<Vector3>();
        NodeRecord current = node;
        while (current != null)
        {
            path.Add(grid.GridToWorld(current.position));
            current = current.parent;
        }

        path.Reverse();
        if (path.Count > 0)
        {
            path[0] = request.startPosition;
            for (int i = 1; i < path.Count; i++)
            {
                path[i] = new Vector3(path[i].x, request.startPosition.y, path[i].z);
            }
        }

        return path;
    }

    private static List<Vector3> SimplifyWaypoints(
        List<Vector3> rawWaypoints,
        PlanningGridMap grid)
    {
        if (rawWaypoints == null || rawWaypoints.Count <= 2)
        {
            return rawWaypoints ?? new List<Vector3>();
        }

        List<Vector3> simplified = new List<Vector3>();
        int anchorIndex = 0;
        simplified.Add(rawWaypoints[0]);

        for (int i = 1; i < rawWaypoints.Count - 1; i++)
        {
            bool directionChanged = HasDirectionChanged(rawWaypoints[i - 1], rawWaypoints[i], rawWaypoints[i + 1]);
            bool shortcutBlocked = grid != null &&
                !grid.IsSegmentClear(rawWaypoints[anchorIndex], rawWaypoints[i + 1]);

            if (directionChanged || shortcutBlocked)
            {
                simplified.Add(rawWaypoints[i]);
                anchorIndex = i;
            }
        }

        simplified.Add(rawWaypoints[rawWaypoints.Count - 1]);
        return simplified;
    }

    private static bool HasDirectionChanged(Vector3 previous, Vector3 current, Vector3 next)
    {
        Vector2 prevDir = new Vector2(current.x - previous.x, current.z - previous.z).normalized;
        Vector2 nextDir = new Vector2(next.x - current.x, next.z - current.z).normalized;
        return Vector2.Distance(prevDir, nextDir) > 0.01f;
    }

    private static string BuildVectorKey(Vector3 position)
    {
        return $"{Mathf.RoundToInt(position.x * 100f)}_{Mathf.RoundToInt(position.y * 100f)}_{Mathf.RoundToInt(position.z * 100f)}";
    }

    private sealed class NodeRecord
    {
        public NodeRecord(Vector2Int position)
        {
            this.position = position;
            gCost = float.MaxValue;
        }

        public Vector2Int position;
        public float gCost;
        public float hCost;
        public NodeRecord parent;

        public float FCost => gCost + hCost;
    }

}
