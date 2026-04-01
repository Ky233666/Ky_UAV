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
                result.waypoints = BuildWaypoints(current, grid, request.startPosition, request.targetPosition);
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

    private static bool IsBlocked(Vector3 worldPosition, PathPlanningRequest request)
    {
        if (request.obstacleLayer.value == 0)
        {
            return false;
        }

        float halfExtent = Mathf.Max(request.gridCellSize * 0.4f, 0.1f);
        Vector3 halfExtents = new Vector3(halfExtent, halfExtent, halfExtent);
        return Physics.CheckBox(worldPosition, halfExtents, Quaternion.identity, request.obstacleLayer);
    }

    private static List<Vector3> BuildWaypoints(
        NodeRecord goalNode,
        GridDefinition grid,
        Vector3 exactStart,
        Vector3 exactGoal)
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

        return SimplifyWaypoints(reversed);
    }

    private static List<Vector3> SimplifyWaypoints(List<Vector3> rawWaypoints)
    {
        if (rawWaypoints == null || rawWaypoints.Count <= 2)
        {
            return rawWaypoints ?? new List<Vector3>();
        }

        List<Vector3> simplified = new List<Vector3> { rawWaypoints[0] };
        for (int i = 1; i < rawWaypoints.Count - 1; i++)
        {
            Vector3 previous = rawWaypoints[i - 1];
            Vector3 current = rawWaypoints[i];
            Vector3 next = rawWaypoints[i + 1];

            Vector2 prevDir = new Vector2(current.x - previous.x, current.z - previous.z).normalized;
            Vector2 nextDir = new Vector2(next.x - current.x, next.z - current.z).normalized;
            if (Vector2.Distance(prevDir, nextDir) > 0.01f)
            {
                simplified.Add(current);
            }
        }

        simplified.Add(rawWaypoints[rawWaypoints.Count - 1]);
        return simplified;
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
