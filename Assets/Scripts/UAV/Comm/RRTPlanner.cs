using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基于二维采样树的 RRT 路径规划器。
/// 当前在 XZ 平面扩展随机树，并通过碰撞检测规避障碍区域。
/// </summary>
public class RRTPlanner : IPathPlannerWithVisualization
{
    private const float GoalSampleProbability = 0.28f;
    private const int MaxFreeSampleAttempts = 48;
    private const int MaxConnectStepsPerExpansion = 80;

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
            "RRT 初始化完成，双向随机树已从起点和终点建立，等待采样扩展。");

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

        float stepSize = CalculateStepSize(request);
        float connectDistance = Mathf.Max(stepSize * 1.25f, request.gridCellSize);
        int maxIterations = CalculateIterationBudget(request, stepSize);
        System.Random random = new System.Random(BuildDeterministicSeed(request));

        List<RRTNode> startTree = new List<RRTNode>
        {
            new RRTNode(request.startPosition, -1)
        };
        List<RRTNode> goalTree = new List<RRTNode>
        {
            new RRTNode(request.targetPosition, -1)
        };

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool expandFromStart = iteration % 2 == 0;
            List<RRTNode> primaryTree = expandFromStart ? startTree : goalTree;
            List<RRTNode> secondaryTree = expandFromStart ? goalTree : startTree;
            Vector3 biasedTarget = expandFromStart ? request.targetPosition : request.startPosition;
            bool sampledTarget = random.NextDouble() < GoalSampleProbability;
            Vector3 sample = sampledTarget
                ? biasedTarget
                : SampleFreePoint(random, request, biasedTarget);

            RrtExtension extension = ExtendTree(
                primaryTree,
                sample,
                request,
                stepSize);

            if (!extension.succeeded)
            {
                RecordRejectedOrBlockedExpansion(recorder, iteration + 1, extension);
                continue;
            }

            RecordAcceptedExpansion(
                recorder,
                primaryTree,
                extension.parentIndex,
                extension.nodeIndex,
                iteration + 1,
                sampledTarget,
                expandFromStart,
                request);

            if (ConnectTree(
                secondaryTree,
                primaryTree[extension.nodeIndex].position,
                request,
                stepSize,
                connectDistance,
                recorder,
                iteration + 1,
                !expandFromStart,
                out int connectedIndex))
            {
                int startConnectionIndex = expandFromStart ? extension.nodeIndex : connectedIndex;
                int goalConnectionIndex = expandFromStart ? connectedIndex : extension.nodeIndex;
                List<Vector3> rawWaypoints = BuildBidirectionalWaypoints(
                    startTree,
                    startConnectionIndex,
                    goalTree,
                    goalConnectionIndex,
                    request);
                result.waypoints = SimplifyWaypoints(new List<Vector3>(rawWaypoints), request);
                result.totalCost = CalculatePathCost(result.waypoints);
                result.success = true;
                result.message =
                    $"RRT 双向连接规划成功，迭代次数：{iteration + 1}，路径点数量：{result.waypoints.Count}";
                RecordBidirectionalConnection(recorder, rawWaypoints, result.waypoints, iteration + 1);
                PathPlanningVisualizationBuilder.RecordSearchFinished(recorder, true, result.message);
                return result;
            }
        }

        result.message = $"RRT 未在 {maxIterations} 次双向采样迭代内找到可达路径";
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

    private static void RecordRejectedOrBlockedExpansion(
        PathPlanningVisualizationRecorder recorder,
        int iteration,
        RrtExtension extension)
    {
        if (recorder == null)
        {
            return;
        }

        if (extension.rejectedAsBlocked)
        {
            RecordBlockedExpansion(
                recorder,
                iteration,
                extension.nearestPoint,
                extension.rejectedPoint,
                extension.message);
            return;
        }

        RecordRejectedExpansion(
            recorder,
            iteration,
            extension.nearestPoint,
            extension.rejectedPoint,
            extension.message);
    }

    private static void RecordAcceptedExpansion(
        PathPlanningVisualizationRecorder recorder,
        List<RRTNode> tree,
        int nearestIndex,
        int newIndex,
        int iteration,
        bool sampledGoal,
        bool expandFromStart,
        PathPlanningRequest request)
    {
        if (recorder == null || tree == null || nearestIndex < 0 || newIndex < 0)
        {
            return;
        }

        Vector3 nearest = tree[nearestIndex].position;
        Vector3 newPoint = tree[newIndex].position;
        string treeLabel = expandFromStart ? "起点树" : "终点树";
        PathPlanningVisualizationStep step = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.NodeVisited,
            sampledGoal
                ? $"RRT 迭代 {iteration}：目标偏置采样成功，{treeLabel}向对侧树方向延伸。"
                : $"RRT 迭代 {iteration}：{treeLabel}接受新采样节点，继续向可行空间扩展。");
        step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            nearest,
            PathPlanningVisualizationNodeRole.Current,
            iteration,
            label: expandFromStart ? $"S{iteration}" : $"G{iteration}"));
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
        if (expandFromStart)
        {
            step.replaceCandidatePath = true;
            step.candidatePath = BuildBranchPath(tree, newIndex, request.startPosition);
        }
        recorder.RecordStep(step);
    }

    private static void RecordBidirectionalConnection(
        PathPlanningVisualizationRecorder recorder,
        List<Vector3> rawWaypoints,
        List<Vector3> finalPath,
        int iteration)
    {
        if (recorder == null)
        {
            return;
        }

        PathPlanningVisualizationStep backtrackStep = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.BacktrackPathUpdated,
            $"RRT 双向树已连接，回溯生成原始路径，节点数 {rawWaypoints.Count}。");
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
        Vector3 rootPosition)
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
            reversed[0] = rootPosition;
        }

        return reversed;
    }

    private static int CalculateIterationBudget(PathPlanningRequest request, float stepSize)
    {
        float width = request.worldMax.x - request.worldMin.x;
        float depth = request.worldMax.z - request.worldMin.z;
        float areaFactor = Mathf.Max(1f, (width * depth) / Mathf.Max(stepSize * stepSize, 1f));
        return Mathf.Clamp(Mathf.RoundToInt(areaFactor * 10f), 800, 12000);
    }

    private static float CalculateStepSize(PathPlanningRequest request)
    {
        float cellSize = Mathf.Max(request.gridCellSize, 0.25f);
        return Mathf.Clamp(cellSize * 1.1f, 0.75f, 4f);
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

    private static Vector3 SampleFreePoint(
        System.Random random,
        PathPlanningRequest request,
        Vector3 biasedTarget)
    {
        PlanningGridMap map = request.planningMap;
        if (map == null || !map.IsValid)
        {
            return SamplePoint(random, request.worldMin, request.worldMax, request.startPosition.y);
        }

        for (int attempt = 0; attempt < MaxFreeSampleAttempts; attempt++)
        {
            Vector2Int cell;
            if (attempt % 5 == 0)
            {
                cell = SampleCellNearTarget(random, map, biasedTarget, request.gridCellSize);
            }
            else
            {
                cell = new Vector2Int(random.Next(0, map.width), random.Next(0, map.height));
            }

            if (!map.IsWalkable(cell))
            {
                continue;
            }

            Vector3 sample = map.GridToWorld(cell);
            sample.y = request.startPosition.y;
            return sample;
        }

        return SamplePoint(random, request.worldMin, request.worldMax, request.startPosition.y);
    }

    private static Vector2Int SampleCellNearTarget(
        System.Random random,
        PlanningGridMap map,
        Vector3 target,
        float gridCellSize)
    {
        Vector2Int targetCell = map.WorldToGrid(target);
        int radius = Mathf.Max(2, Mathf.RoundToInt(12f / Mathf.Max(gridCellSize, 0.5f)));
        int x = targetCell.x + random.Next(-radius, radius + 1);
        int z = targetCell.y + random.Next(-radius, radius + 1);
        return new Vector2Int(
            Mathf.Clamp(x, 0, map.width - 1),
            Mathf.Clamp(z, 0, map.height - 1));
    }

    private static RrtExtension ExtendTree(
        List<RRTNode> tree,
        Vector3 sample,
        PathPlanningRequest request,
        float stepSize)
    {
        int nearestIndex = FindNearestNodeIndex(tree, sample);
        Vector3 nearest = tree[nearestIndex].position;
        Vector3 newPoint = SteerTowards(nearest, sample, stepSize, request.startPosition.y);

        if (!request.planningMap.IsWorldInside(newPoint))
        {
            return RrtExtension.Rejected(
                nearest,
                newPoint,
                "采样点超出规划边界，本次扩展被丢弃。",
                false);
        }

        if (IsPointBlocked(newPoint, request))
        {
            return RrtExtension.Rejected(
                nearest,
                newPoint,
                "新采样节点落入障碍区域，本次树扩展失败。",
                true);
        }

        if (IsSegmentBlocked(nearest, newPoint, request))
        {
            return RrtExtension.Rejected(
                nearest,
                newPoint,
                "采样连线穿过障碍物，本次树扩展被拒绝。",
                true);
        }

        tree.Add(new RRTNode(newPoint, nearestIndex));
        return RrtExtension.Accepted(nearestIndex, tree.Count - 1, nearest, newPoint);
    }

    private static bool ConnectTree(
        List<RRTNode> tree,
        Vector3 target,
        PathPlanningRequest request,
        float stepSize,
        float connectDistance,
        PathPlanningVisualizationRecorder recorder,
        int iteration,
        bool expandFromStart,
        out int connectedIndex)
    {
        connectedIndex = -1;
        int currentIndex = FindNearestNodeIndex(tree, target);

        for (int step = 0; step < MaxConnectStepsPerExpansion; step++)
        {
            Vector3 current = tree[currentIndex].position;
            float distanceToTarget = Vector3.Distance(current, target);
            if (distanceToTarget <= connectDistance && !IsSegmentBlocked(current, target, request))
            {
                connectedIndex = AddConnectionNodeIfNeeded(tree, currentIndex, current, target);
                if (connectedIndex != currentIndex)
                {
                    RecordConnectionExpansion(
                        recorder,
                        current,
                        target,
                        iteration,
                        step + 1,
                        expandFromStart);
                }
                return true;
            }

            Vector3 next = SteerTowards(current, target, stepSize, request.startPosition.y);
            if (!request.planningMap.IsWorldInside(next) ||
                IsPointBlocked(next, request) ||
                IsSegmentBlocked(current, next, request))
            {
                return false;
            }

            tree.Add(new RRTNode(next, currentIndex));
            RecordConnectionExpansion(
                recorder,
                current,
                next,
                iteration,
                step + 1,
                expandFromStart);
            currentIndex = tree.Count - 1;
        }

        return false;
    }

    private static void RecordConnectionExpansion(
        PathPlanningVisualizationRecorder recorder,
        Vector3 from,
        Vector3 to,
        int iteration,
        int connectStep,
        bool expandFromStart)
    {
        if (recorder == null)
        {
            return;
        }

        int order = iteration * 100 + connectStep;
        string treeLabel = expandFromStart ? "起点树" : "终点树";
        PathPlanningVisualizationStep step = PathPlanningVisualizationBuilder.CreateStep(
            PathPlanningVisualizationStepType.NodeVisited,
            $"RRT 迭代 {iteration}：{treeLabel}执行连接扩展，第 {connectStep} 步靠近对侧新节点。");
        step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            from,
            PathPlanningVisualizationNodeRole.Current,
            order,
            label: expandFromStart ? $"S{iteration}.{connectStep}" : $"G{iteration}.{connectStep}"));
        step.nodeUpdates.Add(PathPlanningVisualizationBuilder.CreateNode(
            to,
            PathPlanningVisualizationNodeRole.Visited,
            order));
        step.edgeUpdates.Add(PathPlanningVisualizationBuilder.CreateEdge(
            from,
            to,
            PathPlanningVisualizationEdgeRole.Tree,
            order,
            Vector3.Distance(from, to)));
        recorder.RecordStep(step);
    }

    private static int AddConnectionNodeIfNeeded(
        List<RRTNode> tree,
        int currentIndex,
        Vector3 current,
        Vector3 target)
    {
        if (Vector3.SqrMagnitude(current - target) <= 0.0001f)
        {
            return currentIndex;
        }

        tree.Add(new RRTNode(target, currentIndex));
        return tree.Count - 1;
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

    private static List<Vector3> BuildBidirectionalWaypoints(
        List<RRTNode> startTree,
        int startConnectionIndex,
        List<RRTNode> goalTree,
        int goalConnectionIndex,
        PathPlanningRequest request)
    {
        List<Vector3> startBranch = BuildTreeBranch(startTree, startConnectionIndex);
        List<Vector3> goalBranch = BuildTreeBranch(goalTree, goalConnectionIndex);
        goalBranch.Reverse();

        List<Vector3> path = new List<Vector3>(startBranch.Count + goalBranch.Count);
        path.AddRange(startBranch);

        for (int i = 0; i < goalBranch.Count; i++)
        {
            if (path.Count > 0 && Vector3.SqrMagnitude(path[path.Count - 1] - goalBranch[i]) <= 0.0001f)
            {
                continue;
            }

            path.Add(goalBranch[i]);
        }

        if (path.Count == 0)
        {
            path.Add(request.startPosition);
            path.Add(request.targetPosition);
        }
        else
        {
            path[0] = request.startPosition;
            path[path.Count - 1] = request.targetPosition;
        }

        return path;
    }

    private static List<Vector3> BuildTreeBranch(List<RRTNode> tree, int nodeIndex)
    {
        List<Vector3> branch = new List<Vector3>();
        int currentIndex = nodeIndex;

        while (currentIndex >= 0 && currentIndex < tree.Count)
        {
            branch.Add(tree[currentIndex].position);
            currentIndex = tree[currentIndex].parentIndex;
        }

        branch.Reverse();
        return branch;
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

    private readonly struct RrtExtension
    {
        public readonly bool succeeded;
        public readonly int parentIndex;
        public readonly int nodeIndex;
        public readonly Vector3 nearestPoint;
        public readonly Vector3 rejectedPoint;
        public readonly string message;
        public readonly bool rejectedAsBlocked;

        private RrtExtension(
            bool succeeded,
            int parentIndex,
            int nodeIndex,
            Vector3 nearestPoint,
            Vector3 rejectedPoint,
            string message,
            bool rejectedAsBlocked)
        {
            this.succeeded = succeeded;
            this.parentIndex = parentIndex;
            this.nodeIndex = nodeIndex;
            this.nearestPoint = nearestPoint;
            this.rejectedPoint = rejectedPoint;
            this.message = message ?? string.Empty;
            this.rejectedAsBlocked = rejectedAsBlocked;
        }

        public static RrtExtension Accepted(
            int parentIndex,
            int nodeIndex,
            Vector3 nearestPoint,
            Vector3 acceptedPoint)
        {
            return new RrtExtension(
                true,
                parentIndex,
                nodeIndex,
                nearestPoint,
                acceptedPoint,
                string.Empty,
                false);
        }

        public static RrtExtension Rejected(
            Vector3 nearestPoint,
            Vector3 rejectedPoint,
            string message,
            bool rejectedAsBlocked)
        {
            return new RrtExtension(
                false,
                -1,
                -1,
                nearestPoint,
                rejectedPoint,
                message,
                rejectedAsBlocked);
        }
    }
}
