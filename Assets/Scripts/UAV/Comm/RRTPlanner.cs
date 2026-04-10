using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基于二维采样树的 RRT 路径规划器。
/// 当前在 XZ 平面上扩展采样树，使用 worldMin/worldMax 和 obstacleLayer 做边界与障碍检测。
/// 为了实验结果可复现，采样随机数种子由请求参数稳定生成。
/// </summary>
public class RRTPlanner : IPathPlanner
{
    private const float GoalSampleProbability = 0.20f;
    private const float NodeCollisionPadding = 0.30f;
    private const float SegmentCollisionPadding = 0.32f;

    public string PlannerName => "RRT";

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

        if (request.worldMax.x <= request.worldMin.x || request.worldMax.z <= request.worldMin.z)
        {
            result.message = "规划边界无效";
            return result;
        }

        if (!IsInsideBounds(request.startPosition, request.worldMin, request.worldMax) ||
            !IsInsideBounds(request.targetPosition, request.worldMin, request.worldMax))
        {
            result.message = "起点或终点超出规划边界";
            return result;
        }

        if (IsPointBlocked(request.startPosition, request) || IsPointBlocked(request.targetPosition, request))
        {
            result.message = "起点或终点位于障碍区域";
            return result;
        }

        if (!IsSegmentBlocked(request.startPosition, request.targetPosition, request))
        {
            result.waypoints.Add(request.startPosition);
            result.waypoints.Add(request.targetPosition);
            result.totalCost = Vector3.Distance(request.startPosition, request.targetPosition);
            result.success = true;
            result.message = "已生成直达 RRT 路径";
            return result;
        }

        float stepSize = Mathf.Clamp(Mathf.Max(request.gridCellSize * 1.5f, 1f), 1f, 6f);
        float connectDistance = Mathf.Max(stepSize * 1.25f, request.gridCellSize);
        int maxIterations = CalculateIterationBudget(request, stepSize);
        System.Random random = new System.Random(BuildDeterministicSeed(request));

        List<RRTNode> tree = new List<RRTNode>
        {
            new RRTNode(request.startPosition, -1)
        };

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            Vector3 sample = random.NextDouble() < GoalSampleProbability
                ? request.targetPosition
                : SamplePoint(random, request.worldMin, request.worldMax, request.startPosition.y);

            int nearestIndex = FindNearestNodeIndex(tree, sample);
            Vector3 nearest = tree[nearestIndex].position;
            Vector3 newPoint = SteerTowards(nearest, sample, stepSize, request.startPosition.y);

            if (!IsInsideBounds(newPoint, request.worldMin, request.worldMax))
            {
                continue;
            }

            if (IsPointBlocked(newPoint, request) || IsSegmentBlocked(nearest, newPoint, request))
            {
                continue;
            }

            tree.Add(new RRTNode(newPoint, nearestIndex));
            int newIndex = tree.Count - 1;

            if (Vector3.Distance(newPoint, request.targetPosition) <= connectDistance &&
                !IsSegmentBlocked(newPoint, request.targetPosition, request))
            {
                tree.Add(new RRTNode(request.targetPosition, newIndex));
                List<Vector3> waypoints = BuildWaypoints(tree, tree.Count - 1, request);
                result.waypoints = SimplifyWaypoints(waypoints, request);
                result.totalCost = CalculatePathCost(result.waypoints);
                result.success = true;
                result.message = $"RRT 规划成功，迭代次数：{iteration + 1}，路径点数量：{result.waypoints.Count}";
                return result;
            }
        }

        result.message = $"RRT 未在 {maxIterations} 次迭代内找到可达路径";
        return result;
    }

    private static int CalculateIterationBudget(PathPlanningRequest request, float stepSize)
    {
        float width = request.worldMax.x - request.worldMin.x;
        float depth = request.worldMax.z - request.worldMin.z;
        float areaFactor = Mathf.Max(1f, (width * depth) / Mathf.Max(stepSize * stepSize, 1f));
        return Mathf.Clamp(Mathf.RoundToInt(areaFactor * 8f), 400, 5000);
    }

    private static int BuildDeterministicSeed(PathPlanningRequest request)
    {
        unchecked
        {
            int seed = 17;
            seed = seed * 31 + request.droneId;
            seed = seed * 31 + Mathf.RoundToInt(request.startPosition.x * 100f);
            seed = seed * 31 + Mathf.RoundToInt(request.startPosition.z * 100f);
            seed = seed * 31 + Mathf.RoundToInt(request.targetPosition.x * 100f);
            seed = seed * 31 + Mathf.RoundToInt(request.targetPosition.z * 100f);
            seed = seed * 31 + Mathf.RoundToInt(request.gridCellSize * 100f);
            seed = seed * 31 + Mathf.RoundToInt(request.worldMin.x * 10f);
            seed = seed * 31 + Mathf.RoundToInt(request.worldMin.z * 10f);
            seed = seed * 31 + Mathf.RoundToInt(request.worldMax.x * 10f);
            seed = seed * 31 + Mathf.RoundToInt(request.worldMax.z * 10f);
            return seed;
        }
    }

    private static Vector3 SamplePoint(System.Random random, Vector3 worldMin, Vector3 worldMax, float y)
    {
        float x = Mathf.Lerp(worldMin.x, worldMax.x, (float)random.NextDouble());
        float z = Mathf.Lerp(worldMin.z, worldMax.z, (float)random.NextDouble());
        return new Vector3(x, y, z);
    }

    private static int FindNearestNodeIndex(List<RRTNode> tree, Vector3 sample)
    {
        int bestIndex = 0;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < tree.Count; i++)
        {
            float distance = (tree[i].position - sample).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static Vector3 SteerTowards(Vector3 from, Vector3 to, float stepSize, float y)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
        {
            return new Vector3(from.x, y, from.z);
        }

        Vector3 direction = delta / distance;
        float step = Mathf.Min(stepSize, distance);
        Vector3 next = from + direction * step;
        next.y = y;
        return next;
    }

    private static bool IsInsideBounds(Vector3 point, Vector3 worldMin, Vector3 worldMax)
    {
        return point.x >= worldMin.x && point.x <= worldMax.x &&
               point.z >= worldMin.z && point.z <= worldMax.z;
    }

    private static bool IsPointBlocked(Vector3 point, PathPlanningRequest request)
    {
        if (request.obstacleLayer.value == 0)
        {
            return false;
        }

        float verticalHalfExtent = Mathf.Max((request.worldMax.y - request.worldMin.y) * 0.5f, 1f);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(request.gridCellSize * NodeCollisionPadding, 0.2f),
            verticalHalfExtent,
            Mathf.Max(request.gridCellSize * NodeCollisionPadding, 0.2f));
        Vector3 probeCenter = new Vector3(point.x, request.worldMin.y + verticalHalfExtent, point.z);
        return Physics.CheckBox(
            probeCenter,
            halfExtents,
            Quaternion.identity,
            request.obstacleLayer,
            QueryTriggerInteraction.Ignore);
    }

    private static bool IsSegmentBlocked(Vector3 from, Vector3 to, PathPlanningRequest request)
    {
        if (request.obstacleLayer.value == 0)
        {
            return false;
        }

        Vector3 direction = to - from;
        direction.y = 0f;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
        {
            return false;
        }

        Vector3 normalizedDirection = direction / distance;
        float verticalHalfExtent = Mathf.Max((request.worldMax.y - request.worldMin.y) * 0.5f, 1f);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(request.gridCellSize * SegmentCollisionPadding, 0.25f),
            verticalHalfExtent,
            Mathf.Max(request.gridCellSize * SegmentCollisionPadding, 0.25f));
        Vector3 center = new Vector3(from.x, request.worldMin.y + verticalHalfExtent, from.z);

        return Physics.BoxCast(
            center,
            halfExtents,
            normalizedDirection,
            Quaternion.identity,
            distance,
            request.obstacleLayer,
            QueryTriggerInteraction.Ignore);
    }

    private static List<Vector3> BuildWaypoints(List<RRTNode> tree, int goalIndex, PathPlanningRequest request)
    {
        List<Vector3> reversed = new List<Vector3>();
        int currentIndex = goalIndex;

        while (currentIndex >= 0)
        {
            reversed.Add(tree[currentIndex].position);
            currentIndex = tree[currentIndex].parentIndex;
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
        return reversed;
    }

    private static List<Vector3> SimplifyWaypoints(List<Vector3> rawWaypoints, PathPlanningRequest request)
    {
        if (rawWaypoints == null || rawWaypoints.Count <= 2)
        {
            return rawWaypoints ?? new List<Vector3>();
        }

        List<Vector3> simplified = new List<Vector3> { rawWaypoints[0] };
        int anchorIndex = 0;

        for (int i = 1; i < rawWaypoints.Count - 1; i++)
        {
            if (IsSegmentBlocked(rawWaypoints[anchorIndex], rawWaypoints[i + 1], request))
            {
                simplified.Add(rawWaypoints[i]);
                anchorIndex = i;
            }
        }

        simplified.Add(rawWaypoints[rawWaypoints.Count - 1]);
        return simplified;
    }

    private static float CalculatePathCost(List<Vector3> waypoints)
    {
        if (waypoints == null || waypoints.Count <= 1)
        {
            return 0f;
        }

        float cost = 0f;
        for (int i = 1; i < waypoints.Count; i++)
        {
            cost += Vector3.Distance(waypoints[i - 1], waypoints[i]);
        }

        return cost;
    }

    private readonly struct RRTNode
    {
        public readonly Vector3 position;
        public readonly int parentIndex;

        public RRTNode(Vector3 position, int parentIndex)
        {
            this.position = position;
            this.parentIndex = parentIndex;
        }
    }
}
