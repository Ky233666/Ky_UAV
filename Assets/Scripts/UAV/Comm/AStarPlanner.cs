using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基于二维网格的 A* 路径规划器。
/// 当前先在 XZ 平面上工作，用于毕设第一版静态障碍路径规划演示。
/// </summary>
public class AStarPlanner : IPathPlanner
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

        if (request.gridCellSize <= 0f)
        {
            result.message = "网格尺寸必须大于 0";
            return result;
        }

        if (request.worldMax.x <= request.worldMin.x || request.worldMax.z <= request.worldMin.z)
        {
            result.message = "规划边界无效";
            return result;
        }

        GridDefinition grid = new GridDefinition(request.worldMin, request.worldMax, request.gridCellSize);
        Vector2Int start = grid.WorldToGrid(request.startPosition);
        Vector2Int goal = grid.WorldToGrid(request.targetPosition);

        if (!grid.IsInside(start) || !grid.IsInside(goal))
        {
            result.message = "起点或终点超出规划边界";
            return result;
        }

        List<NodeRecord> openList = new List<NodeRecord>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, NodeRecord> discovered = new Dictionary<Vector2Int, NodeRecord>();

        NodeRecord startNode = new NodeRecord(start)
        {
            gCost = 0f,
            hCost = Heuristic(start, goal, request.allowDiagonal)
        };

        openList.Add(startNode);
        discovered[start] = startNode;

        while (openList.Count > 0)
        {
            NodeRecord current = GetLowestCostNode(openList);
            if (current.position == goal)
            {
                result.waypoints = BuildWaypoints(current, grid, request);
                result.totalCost = current.gCost * request.gridCellSize;
                result.success = true;
                result.message = $"A* 规划成功，路径点数量：{result.waypoints.Count}";
                return result;
            }

            openList.Remove(current);
            closedSet.Add(current.position);

            foreach (Vector2Int direction in GetDirections(request.allowDiagonal))
            {
                Vector2Int neighborPos = current.position + direction;
                if (!grid.IsInside(neighborPos) || closedSet.Contains(neighborPos))
                {
                    continue;
                }

                if (direction.x != 0 && direction.y != 0 &&
                    IsDiagonalCornerCut(current.position, neighborPos, grid, request, start, goal))
                {
                    continue;
                }

                bool isStartOrGoal = neighborPos == start || neighborPos == goal;
                if (!isStartOrGoal && IsBlocked(grid.GridToWorld(neighborPos), request))
                {
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

                neighbor.parent = current;
                neighbor.gCost = tentativeG;
                neighbor.hCost = Heuristic(neighborPos, goal, request.allowDiagonal);

                if (!openList.Contains(neighbor))
                {
                    openList.Add(neighbor);
                }
            }
        }

        result.message = "A* 未找到可达路径";
        return result;
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
        GridDefinition grid,
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

        bool horizontalBlocked = !horizontalIsStartOrGoal && IsBlocked(grid.GridToWorld(horizontal), request);
        bool verticalBlocked = !verticalIsStartOrGoal && IsBlocked(grid.GridToWorld(vertical), request);
        return horizontalBlocked || verticalBlocked;
    }

    private static bool IsBlocked(Vector3 worldPosition, PathPlanningRequest request)
    {
        if (request.obstacleLayer.value == 0)
        {
            return false;
        }

        float horizontalHalfExtent = Mathf.Max(request.gridCellSize * 0.5f, 0.15f);
        float verticalHalfExtent = Mathf.Max((request.worldMax.y - request.worldMin.y) * 0.5f, 1f);
        Vector3 halfExtents = new Vector3(horizontalHalfExtent, verticalHalfExtent, horizontalHalfExtent);
        Vector3 probeCenter = new Vector3(worldPosition.x, request.worldMin.y + verticalHalfExtent, worldPosition.z);
        return Physics.CheckBox(probeCenter, halfExtents, Quaternion.identity, request.obstacleLayer, QueryTriggerInteraction.Ignore);
    }

    private static List<Vector3> BuildWaypoints(
        NodeRecord goalNode,
        GridDefinition grid,
        PathPlanningRequest request)
    {
        Vector3 exactStart = request.startPosition;
        Vector3 exactGoal = request.targetPosition;
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
            reversed.Add(exactStart);
            reversed.Add(exactGoal);
            return reversed;
        }

        reversed[0] = exactStart;
        reversed[reversed.Count - 1] = exactGoal;
        for (int i = 1; i < reversed.Count - 1; i++)
        {
            reversed[i] = new Vector3(reversed[i].x, exactStart.y, reversed[i].z);
        }

        return SimplifyWaypoints(
            reversed,
            request.obstacleLayer,
            request.worldMin.y,
            request.worldMax.y);
    }

    private static List<Vector3> SimplifyWaypoints(
        List<Vector3> rawWaypoints,
        LayerMask obstacleLayer,
        float worldMinY,
        float worldMaxY)
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
            bool shortcutBlocked = IsShortcutBlocked(
                rawWaypoints[anchorIndex],
                rawWaypoints[i + 1],
                obstacleLayer,
                worldMinY,
                worldMaxY);

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

    private static bool IsShortcutBlocked(
        Vector3 from,
        Vector3 to,
        LayerMask obstacleLayer,
        float worldMinY,
        float worldMaxY)
    {
        if (obstacleLayer.value == 0)
        {
            return false;
        }

        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
        {
            return false;
        }

        float verticalHalfExtent = Mathf.Max((worldMaxY - worldMinY) * 0.5f, 1f);
        Vector3 halfExtents = new Vector3(0.35f, verticalHalfExtent, 0.35f);
        Vector3 center = new Vector3(from.x, worldMinY + verticalHalfExtent, from.z);
        Vector3 normalizedDirection = direction / distance;

        return Physics.BoxCast(
            center,
            halfExtents,
            normalizedDirection,
            Quaternion.identity,
            distance,
            obstacleLayer,
            QueryTriggerInteraction.Ignore);
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

    private readonly struct GridDefinition
    {
        private readonly Vector3 worldMin;
        private readonly Vector3 worldMax;
        private readonly float cellSize;
        private readonly int width;
        private readonly int height;

        public GridDefinition(Vector3 worldMin, Vector3 worldMax, float cellSize)
        {
            this.worldMin = worldMin;
            this.worldMax = worldMax;
            this.cellSize = cellSize;

            width = Mathf.Max(1, Mathf.RoundToInt((worldMax.x - worldMin.x) / cellSize) + 1);
            height = Mathf.Max(1, Mathf.RoundToInt((worldMax.z - worldMin.z) / cellSize) + 1);
        }

        public bool IsInside(Vector2Int gridPosition)
        {
            return gridPosition.x >= 0 && gridPosition.x < width &&
                   gridPosition.y >= 0 && gridPosition.y < height;
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            float x = Mathf.Clamp(worldPosition.x, worldMin.x, worldMax.x);
            float z = Mathf.Clamp(worldPosition.z, worldMin.z, worldMax.z);

            int gx = Mathf.RoundToInt((x - worldMin.x) / cellSize);
            int gz = Mathf.RoundToInt((z - worldMin.z) / cellSize);
            return new Vector2Int(gx, gz);
        }

        public Vector3 GridToWorld(Vector2Int gridPosition)
        {
            float x = worldMin.x + gridPosition.x * cellSize;
            float z = worldMin.z + gridPosition.y * cellSize;
            float y = Mathf.Lerp(worldMin.y, worldMax.y, 0.5f);
            return new Vector3(x, y, z);
        }
    }
}
