using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把路径规划步骤状态渲染为世界空间中的节点、边和路径线。
/// 只负责表现，不参与播放控制与步骤推进。
/// </summary>
public class PathPlanningProcessRenderer : MonoBehaviour
{
    [Header("Render Root")]
    public Transform renderRoot;

    [Header("Projection")]
    public bool useTopDownProjection;
    public float topDownProjectionHeight = 12f;

    [Header("Marker Size")]
    public float markerOffsetY = 0.18f;
    public float baseNodeScale = 0.36f;
    public float highlightedNodeScale = 0.52f;
    public float blockedNodeScale = 0.44f;
    public float startGoalScale = 0.58f;

    [Header("Line Width")]
    public float treeEdgeWidth = 0.05f;
    public float rejectedEdgeWidth = 0.04f;
    public float candidatePathWidth = 0.12f;
    public float backtrackPathWidth = 0.16f;
    public float finalPathWidth = 0.22f;

    private readonly Dictionary<PathPlanningVisualizationNodeRole, List<GameObject>> nodePools =
        new Dictionary<PathPlanningVisualizationNodeRole, List<GameObject>>();
    private readonly Dictionary<PathPlanningVisualizationEdgeRole, List<LineRenderer>> edgePools =
        new Dictionary<PathPlanningVisualizationEdgeRole, List<LineRenderer>>();

    private LineRenderer candidatePathRenderer;
    private LineRenderer backtrackPathRenderer;
    private LineRenderer finalPathRenderer;
    private GameObject pulsingCurrentMarker;

    private void LateUpdate()
    {
        if (pulsingCurrentMarker == null || !pulsingCurrentMarker.activeSelf)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.08f;
        pulsingCurrentMarker.transform.localScale = Vector3.one * highlightedNodeScale * pulse;
    }

    public bool SetProjectionMode(bool enabled, float projectionHeight)
    {
        bool changed = useTopDownProjection != enabled ||
                       !Mathf.Approximately(topDownProjectionHeight, projectionHeight);
        useTopDownProjection = enabled;
        topDownProjectionHeight = projectionHeight;
        return changed;
    }

    public void ClearVisualization()
    {
        EnsureRoot();
        HideAllNodePools();
        HideAllEdgePools();
        SetPolyline(candidatePathRenderer, null, Color.clear, 0f);
        SetPolyline(backtrackPathRenderer, null, Color.clear, 0f);
        SetPolyline(finalPathRenderer, null, Color.clear, 0f);
        pulsingCurrentMarker = null;
    }

    public void RenderTrace(PathPlanningVisualizationTrace trace, AlgorithmVisualizerManager.PlaybackRuntimeState state)
    {
        EnsureRoot();
        if (trace == null || state == null)
        {
            ClearVisualization();
            return;
        }

        List<PathPlanningVisualizationNodeState> startNodes = new List<PathPlanningVisualizationNodeState>(1);
        List<PathPlanningVisualizationNodeState> goalNodes = new List<PathPlanningVisualizationNodeState>(1);
        List<PathPlanningVisualizationNodeState> frontierNodes = new List<PathPlanningVisualizationNodeState>();
        List<PathPlanningVisualizationNodeState> visitedNodes = new List<PathPlanningVisualizationNodeState>();
        List<PathPlanningVisualizationNodeState> closedNodes = new List<PathPlanningVisualizationNodeState>();
        List<PathPlanningVisualizationNodeState> currentNodes = new List<PathPlanningVisualizationNodeState>();
        List<PathPlanningVisualizationNodeState> rejectedNodes = new List<PathPlanningVisualizationNodeState>();
        List<PathPlanningVisualizationNodeState> blockedNodes = new List<PathPlanningVisualizationNodeState>();

        startNodes.Add(new PathPlanningVisualizationNodeState
        {
            position = trace.request != null ? trace.request.startPosition : Vector3.zero,
            role = PathPlanningVisualizationNodeRole.Start
        });
        goalNodes.Add(new PathPlanningVisualizationNodeState
        {
            position = trace.request != null ? trace.request.targetPosition : Vector3.zero,
            role = PathPlanningVisualizationNodeRole.Goal
        });

        foreach (PathPlanningVisualizationNodeState node in state.Nodes)
        {
            switch (node.role)
            {
                case PathPlanningVisualizationNodeRole.Frontier:
                    frontierNodes.Add(node);
                    break;
                case PathPlanningVisualizationNodeRole.Visited:
                    visitedNodes.Add(node);
                    break;
                case PathPlanningVisualizationNodeRole.Closed:
                    closedNodes.Add(node);
                    break;
                case PathPlanningVisualizationNodeRole.Current:
                    currentNodes.Add(node);
                    break;
                case PathPlanningVisualizationNodeRole.Rejected:
                    rejectedNodes.Add(node);
                    break;
                case PathPlanningVisualizationNodeRole.Blocked:
                    blockedNodes.Add(node);
                    break;
                case PathPlanningVisualizationNodeRole.Start:
                    startNodes.Clear();
                    startNodes.Add(node);
                    break;
                case PathPlanningVisualizationNodeRole.Goal:
                    goalNodes.Clear();
                    goalNodes.Add(node);
                    break;
            }
        }

        ApplyNodeGroup(PathPlanningVisualizationNodeRole.Blocked, blockedNodes, trace.accentColor);
        ApplyNodeGroup(PathPlanningVisualizationNodeRole.Visited, visitedNodes, trace.accentColor);
        ApplyNodeGroup(PathPlanningVisualizationNodeRole.Closed, closedNodes, trace.accentColor);
        ApplyNodeGroup(PathPlanningVisualizationNodeRole.Frontier, frontierNodes, trace.accentColor);
        ApplyNodeGroup(PathPlanningVisualizationNodeRole.Rejected, rejectedNodes, trace.accentColor);
        ApplyNodeGroup(PathPlanningVisualizationNodeRole.Start, startNodes, trace.accentColor);
        ApplyNodeGroup(PathPlanningVisualizationNodeRole.Goal, goalNodes, trace.accentColor);
        ApplyNodeGroup(PathPlanningVisualizationNodeRole.Current, currentNodes, trace.accentColor);

        List<PathPlanningVisualizationEdgeState> treeEdges = new List<PathPlanningVisualizationEdgeState>();
        List<PathPlanningVisualizationEdgeState> candidateEdges = new List<PathPlanningVisualizationEdgeState>();
        List<PathPlanningVisualizationEdgeState> rejectedEdges = new List<PathPlanningVisualizationEdgeState>();

        foreach (PathPlanningVisualizationEdgeState edge in state.Edges)
        {
            switch (edge.role)
            {
                case PathPlanningVisualizationEdgeRole.Tree:
                    treeEdges.Add(edge);
                    break;
                case PathPlanningVisualizationEdgeRole.Candidate:
                    candidateEdges.Add(edge);
                    break;
                case PathPlanningVisualizationEdgeRole.Rejected:
                    rejectedEdges.Add(edge);
                    break;
            }
        }

        ApplyEdgeGroup(PathPlanningVisualizationEdgeRole.Tree, treeEdges, trace.accentColor);
        ApplyEdgeGroup(PathPlanningVisualizationEdgeRole.Candidate, candidateEdges, trace.accentColor);
        ApplyEdgeGroup(PathPlanningVisualizationEdgeRole.Rejected, rejectedEdges, trace.accentColor);

        EnsurePathRenderers();
        SetPolyline(candidatePathRenderer, state.candidatePath, new Color(1f, 0.76f, 0.18f, 0.96f), candidatePathWidth);
        SetPolyline(backtrackPathRenderer, state.backtrackPath, new Color(1f, 0.55f, 0.18f, 0.96f), backtrackPathWidth);
        SetPolyline(finalPathRenderer, state.finalPath, trace.accentColor, finalPathWidth);
    }

    private void EnsureRoot()
    {
        if (renderRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("__PathPlanningProcessRenderer");
        if (existing != null)
        {
            renderRoot = existing;
            return;
        }

        GameObject rootObject = new GameObject("__PathPlanningProcessRenderer");
        renderRoot = rootObject.transform;
        renderRoot.SetParent(transform, false);
        renderRoot.localPosition = Vector3.zero;
        renderRoot.localRotation = Quaternion.identity;
        renderRoot.localScale = Vector3.one;
    }

    private void EnsurePathRenderers()
    {
        if (candidatePathRenderer == null)
        {
            candidatePathRenderer = CreateLineRenderer("CandidatePath", 220);
        }

        if (backtrackPathRenderer == null)
        {
            backtrackPathRenderer = CreateLineRenderer("BacktrackPath", 222);
        }

        if (finalPathRenderer == null)
        {
            finalPathRenderer = CreateLineRenderer("FinalPath", 224);
        }
    }

    private void HideAllNodePools()
    {
        foreach (List<GameObject> pool in nodePools.Values)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].SetActive(false);
            }
        }
    }

    private void HideAllEdgePools()
    {
        foreach (List<LineRenderer> pool in edgePools.Values)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].enabled = false;
                pool[i].positionCount = 0;
            }
        }
    }

    private void ApplyNodeGroup(
        PathPlanningVisualizationNodeRole role,
        List<PathPlanningVisualizationNodeState> nodes,
        Color accentColor)
    {
        if (!nodePools.TryGetValue(role, out List<GameObject> pool))
        {
            pool = new List<GameObject>();
            nodePools[role] = pool;
        }

        EnsureNodePool(pool, role, nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            GameObject marker = pool[i];
            marker.SetActive(true);
            marker.transform.position = BuildDisplayPoint(nodes[i].position, role);
            marker.transform.localScale = Vector3.one * ResolveNodeScale(role);

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial.color = ResolveNodeColor(role, accentColor);
            }

            if (role == PathPlanningVisualizationNodeRole.Current)
            {
                pulsingCurrentMarker = marker;
            }
        }

        for (int i = nodes.Count; i < pool.Count; i++)
        {
            pool[i].SetActive(false);
        }
    }

    private void ApplyEdgeGroup(
        PathPlanningVisualizationEdgeRole role,
        List<PathPlanningVisualizationEdgeState> edges,
        Color accentColor)
    {
        if (!edgePools.TryGetValue(role, out List<LineRenderer> pool))
        {
            pool = new List<LineRenderer>();
            edgePools[role] = pool;
        }

        EnsureEdgePool(pool, role, edges.Count);
        for (int i = 0; i < edges.Count; i++)
        {
            LineRenderer renderer = pool[i];
            renderer.enabled = true;
            renderer.positionCount = 2;
            renderer.SetPosition(0, BuildDisplayPoint(edges[i].from, PathPlanningVisualizationNodeRole.Visited));
            renderer.SetPosition(1, BuildDisplayPoint(edges[i].to, PathPlanningVisualizationNodeRole.Visited));
            Color color = ResolveEdgeColor(role, accentColor);
            renderer.startColor = color;
            renderer.endColor = color;
            float width = ResolveEdgeWidth(role);
            renderer.startWidth = width;
            renderer.endWidth = width;
        }

        for (int i = edges.Count; i < pool.Count; i++)
        {
            pool[i].enabled = false;
            pool[i].positionCount = 0;
        }
    }

    private void EnsureNodePool(List<GameObject> pool, PathPlanningVisualizationNodeRole role, int requiredCount)
    {
        while (pool.Count < requiredCount)
        {
            PrimitiveType primitiveType = ResolvePrimitive(role);
            GameObject marker = GameObject.CreatePrimitive(primitiveType);
            marker.name = $"{role}_{pool.Count:D3}";
            marker.transform.SetParent(renderRoot, false);
            marker.layer = LayerMask.NameToLayer("Ignore Raycast");

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyUnityObject(collider);
            }

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = CreateUnlitMaterial();
            }

            pool.Add(marker);
            marker.SetActive(false);
        }
    }

    private void EnsureEdgePool(List<LineRenderer> pool, PathPlanningVisualizationEdgeRole role, int requiredCount)
    {
        while (pool.Count < requiredCount)
        {
            pool.Add(CreateLineRenderer($"{role}_{pool.Count:D3}", 180));
            pool[pool.Count - 1].enabled = false;
        }
    }

    private LineRenderer CreateLineRenderer(string name, int sortingOrder)
    {
        GameObject lineObject = new GameObject(name, typeof(RectTransform));
        lineObject.transform.SetParent(renderRoot, false);
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.material = CreateLineMaterial();
        lineRenderer.sortingOrder = sortingOrder;
        return lineRenderer;
    }

    private void SetPolyline(LineRenderer lineRenderer, List<Vector3> points, Color color, float width)
    {
        if (lineRenderer == null)
        {
            return;
        }

        if (points == null || points.Count < 2)
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 0;
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            lineRenderer.SetPosition(i, BuildDisplayPoint(points[i], PathPlanningVisualizationNodeRole.Visited));
        }

        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
    }

    private Vector3 BuildDisplayPoint(Vector3 worldPoint, PathPlanningVisualizationNodeRole role)
    {
        float offset = markerOffsetY;
        if (role == PathPlanningVisualizationNodeRole.Blocked)
        {
            offset = markerOffsetY * 0.55f;
        }
        else if (role == PathPlanningVisualizationNodeRole.Current)
        {
            offset = markerOffsetY * 1.35f;
        }

        if (useTopDownProjection)
        {
            return new Vector3(worldPoint.x, topDownProjectionHeight + offset, worldPoint.z);
        }

        return worldPoint + Vector3.up * offset;
    }

    private PrimitiveType ResolvePrimitive(PathPlanningVisualizationNodeRole role)
    {
        switch (role)
        {
            case PathPlanningVisualizationNodeRole.Blocked:
            case PathPlanningVisualizationNodeRole.Closed:
            case PathPlanningVisualizationNodeRole.Rejected:
                return PrimitiveType.Cube;
            default:
                return PrimitiveType.Sphere;
        }
    }

    private float ResolveNodeScale(PathPlanningVisualizationNodeRole role)
    {
        switch (role)
        {
            case PathPlanningVisualizationNodeRole.Start:
            case PathPlanningVisualizationNodeRole.Goal:
                return startGoalScale;
            case PathPlanningVisualizationNodeRole.Current:
                return highlightedNodeScale;
            case PathPlanningVisualizationNodeRole.Blocked:
                return blockedNodeScale;
            default:
                return baseNodeScale;
        }
    }

    private Color ResolveNodeColor(PathPlanningVisualizationNodeRole role, Color accentColor)
    {
        switch (role)
        {
            case PathPlanningVisualizationNodeRole.Start:
                return new Color(0.20f, 0.92f, 0.46f, 0.96f);
            case PathPlanningVisualizationNodeRole.Goal:
                return new Color(1.00f, 0.30f, 0.30f, 0.96f);
            case PathPlanningVisualizationNodeRole.Frontier:
                return new Color(0.24f, 0.84f, 1.00f, 0.55f);
            case PathPlanningVisualizationNodeRole.Visited:
                return new Color(accentColor.r, accentColor.g, accentColor.b, 0.42f);
            case PathPlanningVisualizationNodeRole.Closed:
                return new Color(0.12f, 0.34f, 0.72f, 0.74f);
            case PathPlanningVisualizationNodeRole.Current:
                return new Color(1.00f, 0.84f, 0.20f, 0.98f);
            case PathPlanningVisualizationNodeRole.Rejected:
                return new Color(0.58f, 0.34f, 0.34f, 0.76f);
            case PathPlanningVisualizationNodeRole.Blocked:
                return new Color(0.45f, 0.12f, 0.12f, 0.66f);
            default:
                return accentColor;
        }
    }

    private Color ResolveEdgeColor(PathPlanningVisualizationEdgeRole role, Color accentColor)
    {
        switch (role)
        {
            case PathPlanningVisualizationEdgeRole.Tree:
                return new Color(accentColor.r, accentColor.g, accentColor.b, 0.45f);
            case PathPlanningVisualizationEdgeRole.Candidate:
                return new Color(0.36f, 0.72f, 1.00f, 0.38f);
            case PathPlanningVisualizationEdgeRole.Rejected:
                return new Color(0.62f, 0.24f, 0.24f, 0.34f);
            default:
                return accentColor;
        }
    }

    private float ResolveEdgeWidth(PathPlanningVisualizationEdgeRole role)
    {
        switch (role)
        {
            case PathPlanningVisualizationEdgeRole.Tree:
                return treeEdgeWidth;
            case PathPlanningVisualizationEdgeRole.Rejected:
                return rejectedEdgeWidth;
            case PathPlanningVisualizationEdgeRole.Candidate:
            default:
                return treeEdgeWidth;
        }
    }

    private static Material CreateUnlitMaterial()
    {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        return new Material(shader);
    }

    private static Material CreateLineMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        return new Material(shader);
    }

    private static void DestroyUnityObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
