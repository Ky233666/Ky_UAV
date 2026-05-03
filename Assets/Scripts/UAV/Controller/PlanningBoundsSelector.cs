using UnityEngine;

/// <summary>
/// Runtime drag selector for the XZ planning map bounds.
/// </summary>
public class PlanningBoundsSelector : MonoBehaviour
{
    [Header("References")]
    public SimulationManager simulationManager;
    public DroneManager droneManager;
    public CameraManager cameraManager;
    public PlanningMapVisualizer planningMapVisualizer;

    [Header("Selection")]
    public LayerMask selectionSurfaceLayer;
    public float maxSelectionDistance = 500f;
    public float minimumSelectionSpan = 4f;
    public KeyCode cancelKey = KeyCode.Escape;

    [Header("Preview")]
    public Color previewColor = new Color(1f, 0.78f, 0.18f, 0.95f);
    public float previewLineWidth = 0.18f;
    public float previewHeightOffset = 0.45f;

    public bool IsSelecting => isSelecting;
    public string LastMessage { get; private set; } = "地图范围框选未启动";

    private bool isSelecting;
    private bool isDragging;
    private Vector3 dragStart;
    private Vector3 dragCurrent;
    private LineRenderer previewRenderer;
    private Material previewMaterial;

    private void Awake()
    {
        RuntimeSceneRegistry.Register(this);
    }

    private void Update()
    {
        if (!isSelecting)
        {
            return;
        }

        CacheReferences();

        if (Input.GetKeyDown(cancelKey))
        {
            CancelSelection("已取消地图范围框选");
            return;
        }

        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            CancelSelection("仿真运行中，已退出地图范围框选");
            return;
        }

        HandleSelectionInput();
        UpdatePreview();
    }

    public void ToggleSelection()
    {
        if (isSelecting)
        {
            CancelSelection("已退出地图范围框选");
            return;
        }

        BeginSelection();
    }

    public void BeginSelection()
    {
        CacheReferences();
        if (droneManager == null)
        {
            LastMessage = "未找到 DroneManager，无法框选地图范围";
            return;
        }

        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            LastMessage = "请先停止或重置仿真，再框选地图范围";
            return;
        }

        isSelecting = true;
        isDragging = false;
        cameraManager?.SwitchToOverview();
        EnsurePreviewRenderer();
        SetPreviewVisible(false);
        LastMessage = "按住左键拖拽地面，松开后应用地图范围";
    }

    public void CancelSelection(string message = null)
    {
        isSelecting = false;
        isDragging = false;
        SetPreviewVisible(false);
        LastMessage = string.IsNullOrWhiteSpace(message)
            ? "已退出地图范围框选"
            : message;
    }

    private void HandleSelectionInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (UIInputGate.IsPointerOverBlockingUi())
            {
                return;
            }

            if (TryGetGroundPoint(out Vector3 startPoint, out string failureReason))
            {
                dragStart = startPoint;
                dragCurrent = startPoint;
                isDragging = true;
                LastMessage = "正在框选地图范围，松开左键应用";
                return;
            }

            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                LastMessage = failureReason;
                Debug.LogWarning($"[PlanningBoundsSelector] {failureReason}");
            }
        }

        if (!isDragging)
        {
            return;
        }

        if (TryGetGroundPoint(out Vector3 currentPoint, out _))
        {
            dragCurrent = currentPoint;
        }

        if (!Input.GetMouseButtonUp(0))
        {
            return;
        }

        ApplySelectedBounds();
    }

    private void ApplySelectedBounds()
    {
        Bounds selectedBounds = BuildSelectionBounds(dragStart, dragCurrent);
        Vector3 currentMin = droneManager.planningWorldMin;
        Vector3 currentMax = droneManager.planningWorldMax;
        Vector3 worldMin = new Vector3(selectedBounds.min.x, currentMin.y, selectedBounds.min.z);
        Vector3 worldMax = new Vector3(selectedBounds.max.x, currentMax.y, selectedBounds.max.z);

        droneManager.autoFitPlanningBoundsToScene = false;
        droneManager.ApplyPlanningSettings(
            droneManager.planningGridCellSize,
            droneManager.allowDiagonalPlanning,
            droneManager.autoConfigurePlanningObstacles,
            worldMin,
            worldMax);

        if (planningMapVisualizer != null)
        {
            planningMapVisualizer.droneManager = droneManager;
            planningMapVisualizer.SetBoundsVisible(true);
            planningMapVisualizer.ForceRefresh();
        }

        isSelecting = false;
        isDragging = false;
        SetPreviewVisible(false);
        LastMessage =
            $"已应用手动地图范围 X[{worldMin.x:0},{worldMax.x:0}] Z[{worldMin.z:0},{worldMax.z:0}]";
        Debug.Log($"[PlanningBoundsSelector] {LastMessage}");
    }

    private Bounds BuildSelectionBounds(Vector3 firstPoint, Vector3 secondPoint)
    {
        float minX = Mathf.Min(firstPoint.x, secondPoint.x);
        float maxX = Mathf.Max(firstPoint.x, secondPoint.x);
        float minZ = Mathf.Min(firstPoint.z, secondPoint.z);
        float maxZ = Mathf.Max(firstPoint.z, secondPoint.z);
        float minSpan = Mathf.Max(minimumSelectionSpan, droneManager.planningGridCellSize * 2f);

        ExpandAxisToMinimumSpan(ref minX, ref maxX, minSpan);
        ExpandAxisToMinimumSpan(ref minZ, ref maxZ, minSpan);

        Vector3 center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        Vector3 size = new Vector3(maxX - minX, 0f, maxZ - minZ);
        return new Bounds(center, size);
    }

    private static void ExpandAxisToMinimumSpan(ref float min, ref float max, float minimumSpan)
    {
        if (max - min >= minimumSpan)
        {
            return;
        }

        float center = (min + max) * 0.5f;
        float halfSpan = minimumSpan * 0.5f;
        min = center - halfSpan;
        max = center + halfSpan;
    }

    private bool TryGetGroundPoint(out Vector3 groundPosition, out string failureReason)
    {
        groundPosition = Vector3.zero;
        failureReason = string.Empty;

        Camera activeCamera = cameraManager != null ? cameraManager.GetActiveCamera() : Camera.main;
        if (activeCamera == null)
        {
            failureReason = "当前没有可用相机，无法框选地图范围";
            return false;
        }

        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        LayerMask surfaceLayer = ResolveSelectionSurfaceLayer();
        if (surfaceLayer.value != 0 &&
            Physics.Raycast(ray, out RaycastHit hit, maxSelectionDistance, surfaceLayer, QueryTriggerInteraction.Ignore))
        {
            groundPosition = hit.point;
            return true;
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            groundPosition = ray.GetPoint(enter);
            return true;
        }

        failureReason = "当前鼠标位置没有命中可用地面";
        return false;
    }

    private LayerMask ResolveSelectionSurfaceLayer()
    {
        if (selectionSurfaceLayer.value != 0)
        {
            return selectionSurfaceLayer;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
        {
            return 0;
        }

        return 1 << groundLayer;
    }

    private void CacheReferences()
    {
        simulationManager = RuntimeSceneRegistry.Resolve(simulationManager, this);
        droneManager = RuntimeSceneRegistry.Resolve(
            droneManager,
            simulationManager != null ? simulationManager.droneManager : null,
            this);
        cameraManager = RuntimeSceneRegistry.Resolve(cameraManager, this);
        planningMapVisualizer = RuntimeSceneRegistry.Resolve(
            planningMapVisualizer,
            simulationManager != null ? simulationManager.planningMapVisualizer : null,
            this);
    }

    private void EnsurePreviewRenderer()
    {
        if (previewRenderer != null)
        {
            return;
        }

        GameObject previewObject = new GameObject("PlanningBoundsSelectionPreview");
        previewObject.transform.SetParent(transform, false);
        previewRenderer = previewObject.AddComponent<LineRenderer>();
        previewRenderer.useWorldSpace = true;
        previewRenderer.loop = false;
        previewRenderer.positionCount = 5;
        previewRenderer.alignment = LineAlignment.View;
        previewRenderer.numCapVertices = 4;
        previewRenderer.numCornerVertices = 4;
        previewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        previewRenderer.receiveShadows = false;
        previewRenderer.material = GetOrCreatePreviewMaterial();
        previewRenderer.enabled = false;
    }

    private Material GetOrCreatePreviewMaterial()
    {
        if (previewMaterial != null)
        {
            previewMaterial.color = previewColor;
            return previewMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        previewMaterial = new Material(shader);
        previewMaterial.color = previewColor;
        return previewMaterial;
    }

    private void UpdatePreview()
    {
        EnsurePreviewRenderer();
        if (previewRenderer == null || !isDragging)
        {
            SetPreviewVisible(false);
            return;
        }

        Bounds bounds = BuildSelectionBounds(dragStart, dragCurrent);
        float y = ResolvePreviewHeight();
        previewRenderer.enabled = true;
        previewRenderer.startColor = previewColor;
        previewRenderer.endColor = previewColor;
        previewRenderer.startWidth = previewLineWidth;
        previewRenderer.endWidth = previewLineWidth;
        previewRenderer.SetPosition(0, new Vector3(bounds.min.x, y, bounds.min.z));
        previewRenderer.SetPosition(1, new Vector3(bounds.max.x, y, bounds.min.z));
        previewRenderer.SetPosition(2, new Vector3(bounds.max.x, y, bounds.max.z));
        previewRenderer.SetPosition(3, new Vector3(bounds.min.x, y, bounds.max.z));
        previewRenderer.SetPosition(4, new Vector3(bounds.min.x, y, bounds.min.z));
    }

    private float ResolvePreviewHeight()
    {
        if (droneManager == null)
        {
            return previewHeightOffset;
        }

        return Mathf.Max(droneManager.cruiseHeight + previewHeightOffset, droneManager.planningWorldMin.y + previewHeightOffset);
    }

    private void SetPreviewVisible(bool visible)
    {
        if (previewRenderer != null)
        {
            previewRenderer.enabled = visible;
        }
    }

    private void OnDestroy()
    {
        if (previewMaterial != null)
        {
            Destroy(previewMaterial);
        }
    }
}
