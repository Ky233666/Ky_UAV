using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基于二维采样树的 RRT 路径规划器。
/// 当前在 XZ 平面扩展随机树，并通过碰撞检测规避障碍区域。
/// </summary>
public class RRTPlanner : IPathPlannerWithVisualization
{
    private const float GoalSampleProbability = 0.20f;

    public string PlannerName => "RRT";

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
            "RRT 初始化完成，随机树已从起点建立，等待采样扩展。");

        if (request.worldMax.x <= request.worldMin.x || request.worldMax.z <= request.worldMin.z)
        {
            result.message = "规划边界无效";
            PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, false, result.message);
            return result;
        }

        if (request.planningMap == null || !request.planningMap.IsValid)
        {
            request.planningMap = new PlanningGridMap(request.worldMin, request.worldMax, request.gridCellSize);
        }

        if (!request.planningMap.IsWorldInside(request.startPosition) ||
            !request.planningMap.IsWorldInside(request.targetPosition))
        {
            result.message = "起点或终点超出规划边界";
            PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, false, result.message);
            return result;
        }

        if (IsPointBlocked(request.startPosition, request) || IsPointBlocked(request.targetPosition, request))
        {
            result.message = "起点或终点位于障碍区域";
            PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, false, result.message);
            return result;
        }

        if (!IsSegmentBlocked(request.startPosition, request.targetPosition, request))
        {
            result.waypoints.Add(request.startPosition);
            result.waypoints.Add(request.targetPosition);
            result.totalCost = Vector3.Distance(request.startPosition, request.targetPosition);
            result.success = true;
            result.message = "已生成直达 RRT 路径";
            RecordDirectPath(recorder, result.waypoints);
            PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, true, result.message);
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
            bool sampledGoal = random.NextDouble() < GoalSampleProbability;
            Vector3 sample = sampledGoal
                ? request.targetPosition
                : SamplePoint(random, request.worldMin, request.worldMax, request.startPosition.y);

            int nearestIndex = FindNearestNodeIndex(tree, sample);
            Vector3 nearest = tree[nearestIndex].position;
            Vector3 newPoint = SteerTowards(nearest, sample, stepSize, request.startPosition.y);

            if (!request.planningMap.IsWorldInside(newPoint))
            {
                RecordRejectedExpansion(
                    recorder,
                    iteration + 1,
                    nearest,
                    newPoint,
                    "采样点超出规划边界，本次扩展被丢弃。");
                continue;
            }

            if (IsPointBlocked(newPoint, request))
            {
                RecordBlockedExpansion(
                    recorder,
                    iteration + 1,
                    nearest,
                    newPoint,
                    "新采样节点落入障碍区域，本次树扩展失败。");
                continue;
            }

            if (IsSegmentBlocked(nearest, newPoint, request))
            {
                RecordBlockedExpansion(
                    recorder,
                    iteration + 1,
                    nearest,
                    newPoint,
                    "采样连线穿过障碍物，本次树扩展被拒绝。");
                continue;
            }

            tree.Add(new RRTNode(newPoint, nearestIndex));
            int newIndex = tree.Count - 1;
            RecordAcceptedExpansion(
                recorder,
                tree,
                nearestIndex,
                newIndex,
                iteration + 1,
                sampledGoal,
                request);

            if (Vector3.Distance(newPoint, request.targetPosition) <= connectDistance &&
                !IsSegmentBlocked(newPoint, request.targetPosition, request))
            {
                tree.Add(new RRTNode(request.targetPosition, newIndex));
                List<Vector3> rawWaypoints = BuildWaypoints(tree, tree.Count - 1, request);
                result.waypoints = SimplifyWaypoints(new List<Vector3>(rawWaypoints), request);
                result.totalCost = CalculatePathCost(result.waypoints);
                result.success = true;
                result.message = $"RRT 规划成功，迭代次数：{iteration + 1}，路径点数量：{result.waypoints.Count}";
                RecordGoalConnection(recorder, tree, tree.Count - 1, rawWaypoints, result.waypoints, iteration + 1, request);
                PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, true, result.message);
                return result;
            }
        }

        result.message = $"RRT 未在 {maxIterations} 次迭代内找到可达路径";
        PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, false, result.message);
        return result;
    }

    private static void RecordDirectPath(
        PathPlanningVisualizationRecorder recorder,
        List<Vector3> path)
    {
        if (recorder == null)
        {
            return;
        }

        PathPlanningVisualizationStep candidateStep = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.CandidatePathUpdated,
            "RRT 检测到起点与终点之间无遮挡，直接采用直达候选路径。");
        candidateStep.replaceCandidatePath = true;
        candidateStep.candidatePath = new List<Vector3>(path);
        recorder.RecordStep(candidateStep);

        PathPlanningVisualizationStep finalStep = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.FinalPathConfirmed,
            "已确认直达路径，无需继续扩展随机树。");
        finalStep.replaceFinalPath = true;
        finalStep.finalPath = new List<Vector3>(path);
        recorder.RecordStep(finalStep);
    }

    private static void RecordRejectedExpansion(
        PathPlanningVisualizationRecorder recorder,
        int iteration,
        Vector3 nearest,
        Vector3 rejectedPoint,
        string description)
    {
        if (recorder == null)
        {
            return;
        }

        PathPlanningVisualizationStep step = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.NodeRejected,
            $"RRT 迭代 {iteration}：{description}");
        step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            nearest,
            PathPlanningVisualizationNodeRole.Current,
            iteration,
            label: $"#{iteration}"));
        step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            rejectedPoint,
            PathPlanningVisualizationNodeRole.Rejected,
            iteration));
        step.edgeUpdates.Add(PathPlanningVisualizationBuilder.CreateEdge(
            nearest,
            rejectedPoint,
            PathPlanningVisualizationEdgeRole.Rejected,
            iteration));
        recorder.RecordStep(step);
    }

    private static void RecordBlockedExpansion(
        PathPlanningVisualizationRecorder recorder,
        int iteration,
        Vector3 nearest,
        Vector3 blockedPoint,
        string description)
    {
        if (recorder == null)
        {
            return;
        }

        PathPlanningVisualizationStep step = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.NodeRejected,
            $"RRT 迭代 {iteration}：{description}");
        step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            nearest,
            PathPlanningVisualizationNodeRole.Current,
            iteration,
            label: $"#{iteration}"));
        step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            blockedPoint,
            PathPlanningVisualizationNodeRole.Blocked,
            iteration));
        step.edgeUpdates.Add(PathPlanningVisualizationBuilder.CreateEdge(
            nearest,
            blockedPoint,
            PathPlanningVisualizationEdgeRole.Rejected,
            iteration));
        recorder.RecordStep(step);
    }

    private static void RecordAcceptedExpansion(
        PathPlanningVisualizationRecorder recorder,
        List<RRTNode> tree,
        int nearestIndex,
        int newIndex,
        int iteration,
        bool sampledGoal,
        PathPlanningRequest request)
    {
        if (recorder == null || tree == null || nearestIndex < 0 || newIndex < 0)
        {
            return;
        }

        Vector3 nearest = tree[nearestIndex].position;
        Vector3 newPoint = tree[newIndex].position;
        PathPlanningVisualizationStep step = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.NodeVisited,
            sampledGoal
                ? $"RRT 迭代 {iteration}：目标偏置采样成功，随机树向终点方向延伸。"
                : $"RRT 迭代 {iteration}：接受新采样节点，随机树继续向可行空间扩展。");
        step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            nearest,
            PathPlanningVisualizationNodeRole.Current,
            iteration,
            label: $"#{iteration}"));
        step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            newPoint,
            PathPlanningVisualizationNodeRole.Visited,
            iteration));
        step.edgeUpdates.Add(PathPlanningVisualizationBuilder.CreateEdge(
            nearest,
            newPoint,
            PathPlanningVisualizationEdgeRole.Tree,
            iteration,
            Vector3.Distance(nearest, newPoint)));
        step.replaceCandidatePath = true;
        step.candidatePath = BuildBranchPath(tree, newIndex, request);
        recorder.RecordStep(step);
    }

    private static void RecordGoalConnection(
        PathPlanningVisualizationRecorder recorder,
        List<RRTNode> tree,
        int goalIndex,
        List<Vector3> rawWaypoints,
        List<Vector3> finalPath,
        int iteration,
        PathPlanningRequest request)
    {
        if (recorder == null || tree == null || goalIndex <= 0)
        {
            return;
        }

        int parentIndex = tree[goalIndex].parentIndex;
        if (parentIndex >= 0)
        {
            PathPlanningVisualizationStep connectionStep = PathPlanningVisualizationBuilder.CreateStep(
                PathPlanningVisualizationStepType.CandidatePathUpdated,
                $"RRT 迭代 {iteration}：新节点已进入终点连接半径，成功连接终点。");
            connectionStep.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
                tree[parentIndex].position,
                PathPlanningVisualizationNodeRole.Current,
                iteration,
                label: $"#{iteration}"));
            connectionStep.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
                request.targetPosition,
                PathPlanningVisualizationNodeRole.Goal));
            connectionStep.edgeUpdates.Add(PathPlanningVisualizationBuilder.CreateEdge(
                tree[parentIndex].position,
                request.targetPosition,
                PathPlanningVisualizationEdgeRole.Tree,
                iteration,
                Vector3.Distance(tree[parentIndex].position, request.targetPosition)));
            connectionStep.replaceCandidatePath = true;
            connectionStep.candidatePath = BuildBranchPath(tree, goalIndex, request);
            recorder.RecordStep(connectionStep);
        }

        PathPlanningVisualizationStep backtrackStep = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.BacktrackPathUpdated,
            $"RRT 通过树回溯生成路径，原始回溯节点数 {rawWaypoints.Count}。");
        backtrackStep.replaceBacktrackPath = true;
        backtrackStep.backtrackPath = rawWaypoints != null ? new List<Vector3>(rawWaypoints) : new List<Vector3>();
        recorder.RecordStep(backtrackStep);

        PathPlanningVisualizationStep finalStep = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.FinalPathConfirmed,
            $"RRT 已确认最终路径，迭代次数 {iteration}，最终路径点数 {finalPath.Count}。");
        finalStep.replaceFinalPath = true;
        finalStep.finalPath = finalPath != null ? new List<Vector3>(finalPath) : new List<Vector3>();
        recorder.RecordStep(finalStep);
    }

    private static List<Vector3> BuildBranchPath(
        List<RRTNode> tree,
        int nodeIndex,
        PathPlanningRequest request)
    {
        List<Vector3> reversed = new List<Vector3>();
        int currentIndex = nodeIndex;

        while (currentIndex >= 0)
        {
            reversed.Add(tree[currentIndex].position);
            currentIndex = tree[currentIndex].parentIndex;
        }

        reversed.Reverse();
        if (reversed.Count > 0)
        {
            reversed[0] = request.startPosition;
        }

        return reversed;
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

    private static bool IsPointBlocked(Vector3 point, PathPlanningRequest request)
    {
        PlanningGridMap map = request.planningMap;
        return map != null && map.IsBlocked(map.WorldToGrid(point));
    }

    private static bool IsSegmentBlocked(Vector3 from, Vector3 to, PathPlanningRequest request)
    {
        PlanningGridMap map = request.planningMap;
        return map != null && !map.IsSegmentClear(from, to);
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
