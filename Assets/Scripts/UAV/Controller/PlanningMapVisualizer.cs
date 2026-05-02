using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime preview for the path planning map used by A*/RRT/Q-learning export.
/// It shows the active planning boundary and, on demand, the occupied grid cells.
/// </summary>
public class PlanningMapVisualizer : MonoBehaviour
{
    [Header("References")]
    public DroneManager droneManager;

    [Header("Boundary")]
    public bool showBounds = true;
    public Color boundsColor = new Color(0.18f, 0.85f, 0.92f, 0.95f);
    public float boundsLineWidth = 0.14f;
    public float heightOffset = 0.25f;

    [Header("Occupied Cells")]
    public bool showBlockedCells;
    public Color blockedCellColor = new Color(0.95f, 0.20f, 0.14f, 0.88f);
    public float blockedCellHeight = 0.32f;
    public float blockedCellScale = 0.86f;
    public int maxBlockedCellPreviewCount = 2500;
    public int maxPreviewGridCells = 60000;

    public string LastPreviewMessage { get; private set; } = "Planning map preview not built";

    private LineRenderer boundsRenderer;
    private Transform blockedCellRoot;
    private Material boundsMaterial;
    private Material blockedCellMaterial;
    private readonly List<GameObject> blockedCellObjects = new List<GameObject>();
    private string lastBlockedPreviewSignature = string.Empty;

    private void LateUpdate()
    {
        CacheReferences();
        EnsureBoundsRenderer();
        UpdateBoundsRenderer();

        if (showBlockedCells)
        {
            RefreshBlockedCellsIfNeeded();
        }
    }

    public void SetBoundsVisible(bool visible)
    {
        showBounds = visible;
        EnsureBoundsRenderer();
        UpdateBoundsRenderer();
    }

    public void SetBlockedCellsVisible(bool visible)
    {
        showBlockedCells = visible;
        if (!showBlockedCells)
        {
            ClearBlockedCells();
            lastBlockedPreviewSignature = string.Empty;
            LastPreviewMessage = "Planning occupied-cell preview hidden";
            return;
        }

        ForceRefreshBlockedCells();
    }

    public void ForceRefresh()
    {
        UpdateBoundsRenderer();
        ForceRefreshBlockedCells();
    }

    public void ForceRefreshBlockedCells()
    {
        lastBlockedPreviewSignature = string.Empty;
        RefreshBlockedCellsIfNeeded();
    }

    public void ClearBlockedCells()
    {
        for (int i = blockedCellObjects.Count - 1; i >= 0; i--)
        {
            if (blockedCellObjects[i] != null)
            {
                Destroy(blockedCellObjects[i]);
            }
        }

        blockedCellObjects.Clear();
    }

    private void CacheReferences()
    {
        droneManager = RuntimeSceneRegistry.Resolve(droneManager, this);
    }

    private void EnsureBoundsRenderer()
    {
        if (boundsRenderer != null)
        {
            return;
        }

        GameObject lineObject = new GameObject("PlanningMapBounds");
        lineObject.transform.SetParent(transform, false);
        boundsRenderer = lineObject.AddComponent<LineRenderer>();
        boundsRenderer.useWorldSpace = true;
        boundsRenderer.loop = false;
        boundsRenderer.positionCount = 5;
        boundsRenderer.alignment = LineAlignment.View;
        boundsRenderer.numCapVertices = 4;
        boundsRenderer.numCornerVertices = 4;
        boundsRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        boundsRenderer.receiveShadows = false;
        boundsRenderer.material = GetOrCreateBoundsMaterial();
    }

    private Material GetOrCreateBoundsMaterial()
    {
        if (boundsMaterial != null)
        {
            boundsMaterial.color = boundsColor;
            return boundsMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        boundsMaterial = new Material(shader);
        boundsMaterial.color = boundsColor;
        return boundsMaterial;
    }

    private Material GetOrCreateBlockedCellMaterial()
    {
        if (blockedCellMaterial != null)
        {
            blockedCellMaterial.color = blockedCellColor;
            return blockedCellMaterial;
        }

        Shader shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        blockedCellMaterial = new Material(shader);
        blockedCellMaterial.color = blockedCellColor;
        return blockedCellMaterial;
    }

    private void UpdateBoundsRenderer()
    {
        if (boundsRenderer == null)
        {
            return;
        }

        bool visible = showBounds && droneManager != null;
        boundsRenderer.enabled = visible;
        if (!visible)
        {
            return;
        }

        Vector3 min = droneManager.planningWorldMin;
        Vector3 max = droneManager.planningWorldMax;
        float y = Mathf.Max(droneManager.cruiseHeight + heightOffset, min.y + heightOffset);

        boundsRenderer.startColor = boundsColor;
        boundsRenderer.endColor = boundsColor;
        boundsRenderer.startWidth = boundsLineWidth;
        boundsRenderer.endWidth = boundsLineWidth;
        boundsRenderer.SetPosition(0, new Vector3(min.x, y, min.z));
        boundsRenderer.SetPosition(1, new Vector3(max.x, y, min.z));
        boundsRenderer.SetPosition(2, new Vector3(max.x, y, max.z));
        boundsRenderer.SetPosition(3, new Vector3(min.x, y, max.z));
        boundsRenderer.SetPosition(4, new Vector3(min.x, y, min.z));
    }

    private void RefreshBlockedCellsIfNeeded()
    {
        if (droneManager == null)
        {
            return;
        }

        PlanningGridMap map = droneManager.GetOrBuildPlanningMap();
        if (map == null || !map.IsValid)
        {
            ClearBlockedCells();
            LastPreviewMessage = "Planning map is not valid";
            return;
        }

        string signature = BuildBlockedPreviewSignature(map);
        if (string.Equals(lastBlockedPreviewSignature, signature, System.StringComparison.Ordinal))
        {
            return;
        }

        lastBlockedPreviewSignature = signature;
        RebuildBlockedCells(map);
    }

    private void RebuildBlockedCells(PlanningGridMap map)
    {
        ClearBlockedCells();
        if (map.width * map.height > maxPreviewGridCells)
        {
            LastPreviewMessage =
                $"Planning grid is too large for occupied-cell preview: {map.width}x{map.height}. Boundary is still visible.";
            Debug.LogWarning($"[PlanningMapVisualizer] {LastPreviewMessage}");
            return;
        }

        EnsureBlockedCellRoot();
        Material material = GetOrCreateBlockedCellMaterial();
        float footprint = Mathf.Max(0.05f, map.cellSize * Mathf.Clamp(blockedCellScale, 0.1f, 1f));
        float y = Mathf.Max(droneManager.cruiseHeight + heightOffset, map.worldMin.y + heightOffset);
        List<Vector3> blockedPositions = map.GetBlockedWorldPositions(maxBlockedCellPreviewCount);

        for (int i = 0; i < blockedPositions.Count; i++)
        {
            Vector3 center = blockedPositions[i];
            center.y = y + blockedCellHeight * 0.5f;

            GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cell.name = $"PlanningBlockedCell_{i:0000}";
            cell.transform.SetParent(blockedCellRoot, false);
            cell.transform.position = center;
            cell.transform.localScale = new Vector3(footprint, blockedCellHeight, footprint);

            Collider collider = cell.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = cell.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer >= 0)
            {
                cell.layer = ignoreRaycastLayer;
            }

            blockedCellObjects.Add(cell);
        }

        LastPreviewMessage =
            $"Planning occupied-cell preview: {blockedCellObjects.Count}/{map.BlockedCellCount} cells, grid {map.width}x{map.height}";
        Debug.Log($"[PlanningMapVisualizer] {LastPreviewMessage}");
    }

    private void EnsureBlockedCellRoot()
    {
        if (blockedCellRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("PlanningBlockedCells");
        if (existing != null)
        {
            blockedCellRoot = existing;
            return;
        }

        GameObject root = new GameObject("PlanningBlockedCells");
        root.transform.SetParent(transform, false);
        blockedCellRoot = root.transform;
    }

    private static string BuildBlockedPreviewSignature(PlanningGridMap map)
    {
        return string.Join(
            "|",
            Mathf.RoundToInt(map.worldMin.x * 100f),
            Mathf.RoundToInt(map.worldMin.z * 100f),
            Mathf.RoundToInt(map.worldMax.x * 100f),
            Mathf.RoundToInt(map.worldMax.z * 100f),
            Mathf.RoundToInt(map.cellSize * 100f),
            map.width,
            map.height,
            map.BlockedCellCount);
    }

    private void OnDestroy()
    {
        if (boundsMaterial != null)
        {
            Destroy(boundsMaterial);
        }

        if (blockedCellMaterial != null)
        {
            Destroy(blockedCellMaterial);
        }
    }
}
