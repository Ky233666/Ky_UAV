using UnityEngine;

/// <summary>
/// Supports runtime drag placement and deletion of user-defined box obstacles.
/// </summary>
public class RuntimeObstacleEditor : MonoBehaviour
{
    [Header("References")]
    public SimulationManager simulationManager;
    public DroneManager droneManager;
    public CameraManager cameraManager;
    public RuntimeObstacleCatalog obstacleCatalog;

    [Header("Editing")]
    public bool useDragPlacement = true;
    public LayerMask placementSurfaceLayer;
    public string obstacleCatalogResourcePath = "Configs/RuntimeObstacleCatalog_Default";
    public float maxPlacementDistance = 500f;
    public KeyCode cancelPlacementKey = KeyCode.Escape;
    public float defaultObstacleHeight = 10f;
    public float minObstacleHeight = 2f;
    public float maxObstacleHeight = 30f;
    public float obstacleHeightStep = 1f;
    public float defaultObstacleScaleMultiplier = 1f;
    public float minObstacleScaleMultiplier = 0.5f;
    public float maxObstacleScaleMultiplier = 5f;
    public float obstacleScaleStep = 0.25f;
    public float minFootprintSize = 2f;
    public float overlapPadding = 0.35f;
    public float pointClearancePadding = 0.8f;

    [Header("Appearance")]
    public Color obstacleColor = new Color(0.34f, 0.42f, 0.50f, 0.96f);
    public bool disablePreviewCollider = true;

    [Header("Preview")]
    public bool showPlacementPreview = true;
    public Color validPreviewColor = new Color(0.12f, 0.74f, 0.46f, 0.35f);
    public Color invalidPreviewColor = new Color(0.92f, 0.28f, 0.24f, 0.35f);

    public bool IsCreateMode => isCreateMode;
    public bool IsDeleteMode => isDeleteMode;

    private bool isCreateMode;
    private bool isDeleteMode;
    private bool isDraggingPlacement;
    private int nextObstacleId = 1;
    private Vector3 dragStartPosition;
    private Vector3 dragCurrentPosition;
    private GameObject previewObstacle;
    private int selectedTemplateIndex;
    private readonly System.Collections.Generic.List<RuntimeObstacleCatalog.Entry> availableTemplates =
        new System.Collections.Generic.List<RuntimeObstacleCatalog.Entry>();

    private void Start()
    {
        CacheReferences();
        RefreshObstacleTemplateCache();
        nextObstacleId = GetMaxObstacleId() + 1;
        ClampHeightSettings();
    }

    private void Update()
    {
        HandleCreateInput();
        HandleDeleteInput();
        UpdatePlacementPreview();
    }

    public void ToggleCreateMode()
    {
        CacheReferences();
        if (!isCreateMode && !CanEditObstacleLayout(out string failureReason))
        {
            Debug.LogWarning(failureReason);
            return;
        }

        isCreateMode = !isCreateMode;
        isDeleteMode = false;
        CancelCurrentDrag();

        if (isCreateMode)
        {
            cameraManager?.SwitchToOverview();
            Debug.Log("[RuntimeObstacleEditor] 已进入自定义障碍物绘制模式，按住左键拖拽地面可生成建筑，按 Esc 退出。");
        }
        else
        {
            Debug.Log("[RuntimeObstacleEditor] 已退出自定义障碍物绘制模式。");
        }
    }

    public void ToggleDeleteMode()
    {
        CacheReferences();
        if (!isDeleteMode && !CanEditObstacleLayout(out string failureReason))
        {
            Debug.LogWarning(failureReason);
            return;
        }

        isDeleteMode = !isDeleteMode;
        isCreateMode = false;
        CancelCurrentDrag();

        if (isDeleteMode)
        {
            cameraManager?.SwitchToOverview();
            Debug.Log("[RuntimeObstacleEditor] 已进入自定义障碍物删除模式，点击自定义建筑即可删除，按 Esc 退出。");
        }
        else
        {
            Debug.Log("[RuntimeObstacleEditor] 已退出自定义障碍物删除模式。");
        }
    }

    public void ClearCustomObstacles()
    {
        CacheReferences();
        if (!CanEditObstacleLayout(out string failureReason))
        {
            Debug.LogWarning(failureReason);
            return;
        }

        SimulationContext context = SimulationContext.GetOrCreate(this);
        RuntimeObstacleMarker[] markers = context.GetRuntimeObstacleMarkers();
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null)
            {
                context.UnregisterObstacle(markers[i], false);
                Destroy(markers[i].gameObject);
            }
        }

        CancelCurrentDrag();
        isCreateMode = false;
        isDeleteMode = false;
        context.NotifyObstaclesChanged();
        RequestObstacleRefresh();
        Debug.Log("[RuntimeObstacleEditor] 已清空全部自定义障碍物。");
    }

    public void SetDefaultObstacleHeight(float obstacleHeight)
    {
        defaultObstacleHeight = Mathf.Clamp(obstacleHeight, minObstacleHeight, maxObstacleHeight);
    }

    public void SetDefaultObstacleScaleMultiplier(float scaleMultiplier)
    {
        defaultObstacleScaleMultiplier = Mathf.Clamp(
            scaleMultiplier,
            minObstacleScaleMultiplier,
            maxObstacleScaleMultiplier);
    }

    public float GetDefaultObstacleScaleMultiplier()
    {
        return Mathf.Clamp(defaultObstacleScaleMultiplier, minObstacleScaleMultiplier, maxObstacleScaleMultiplier);
    }

    public string GetCurrentTemplateDisplayName()
    {
        RuntimeObstacleCatalog.Entry templateEntry = GetSelectedTemplateEntry();
        return templateEntry != null ? templateEntry.displayName : "长方体";
    }

    public void SelectPreviousTemplate()
    {
        RefreshObstacleTemplateCache();
        selectedTemplateIndex = WrapTemplateIndex(selectedTemplateIndex - 1);
    }

    public void SelectNextTemplate()
    {
        RefreshObstacleTemplateCache();
        selectedTemplateIndex = WrapTemplateIndex(selectedTemplateIndex + 1);
    }

    public int GetCustomObstacleCount()
    {
        return SimulationContext.GetOrCreate(this).GetRuntimeObstacleMarkers().Length;
    }

    private void CacheReferences()
    {
        simulationManager = RuntimeSceneRegistry.Resolve(simulationManager, this);
        droneManager = RuntimeSceneRegistry.Resolve(
            droneManager,
            simulationManager != null ? simulationManager.droneManager : null,
            this);
        cameraManager = RuntimeSceneRegistry.Resolve(cameraManager, this);

        LoadObstacleCatalogIfNeeded();
    }

    private void LoadObstacleCatalogIfNeeded()
    {
        if (obstacleCatalog != null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(obstacleCatalogResourcePath))
        {
            obstacleCatalog = Resources.Load<RuntimeObstacleCatalog>(obstacleCatalogResourcePath);
        }
    }

    private void RefreshObstacleTemplateCache()
    {
        LoadObstacleCatalogIfNeeded();

        availableTemplates.Clear();
        if (obstacleCatalog != null && obstacleCatalog.entries != null)
        {
            for (int i = 0; i < obstacleCatalog.entries.Length; i++)
            {
                RuntimeObstacleCatalog.Entry entry = obstacleCatalog.entries[i];
                if (entry != null && entry.prefab != null)
                {
                    availableTemplates.Add(entry);
                }
            }
        }

        selectedTemplateIndex = WrapTemplateIndex(selectedTemplateIndex);
    }

    private int WrapTemplateIndex(int index)
    {
        int templateCount = availableTemplates.Count + 1;
        if (templateCount <= 0)
        {
            return 0;
        }

        while (index < 0)
        {
            index += templateCount;
        }

        while (index >= templateCount)
        {
            index -= templateCount;
        }

        return index;
    }

    private RuntimeObstacleCatalog.Entry GetSelectedTemplateEntry()
    {
        RefreshObstacleTemplateCache();
        if (selectedTemplateIndex <= 0)
        {
            return null;
        }

        int entryIndex = selectedTemplateIndex - 1;
        return entryIndex >= 0 && entryIndex < availableTemplates.Count
            ? availableTemplates[entryIndex]
            : null;
    }

    private void ClampHeightSettings()
    {
        minObstacleHeight = Mathf.Max(0.5f, minObstacleHeight);
        maxObstacleHeight = Mathf.Max(minObstacleHeight, maxObstacleHeight);
        obstacleHeightStep = Mathf.Max(0.25f, obstacleHeightStep);
        defaultObstacleHeight = Mathf.Clamp(defaultObstacleHeight, minObstacleHeight, maxObstacleHeight);
        minObstacleScaleMultiplier = Mathf.Max(0.1f, minObstacleScaleMultiplier);
        maxObstacleScaleMultiplier = Mathf.Max(minObstacleScaleMultiplier, maxObstacleScaleMultiplier);
        obstacleScaleStep = Mathf.Max(0.05f, obstacleScaleStep);
        defaultObstacleScaleMultiplier = Mathf.Clamp(
            defaultObstacleScaleMultiplier,
            minObstacleScaleMultiplier,
            maxObstacleScaleMultiplier);
        minFootprintSize = Mathf.Max(0.5f, minFootprintSize);
    }

    private void HandleCreateInput()
    {
        if (!useDragPlacement || !isCreateMode)
        {
            return;
        }

        if (Input.GetKeyDown(cancelPlacementKey))
        {
            isCreateMode = false;
            CancelCurrentDrag();
            Debug.Log("[RuntimeObstacleEditor] 已退出自定义障碍物绘制模式。");
            return;
        }

        if (!CanEditObstacleLayout(out string failureReason))
        {
            if (Input.GetMouseButtonDown(0))
            {
                Debug.LogWarning(failureReason);
            }

            CancelCurrentDrag();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUi())
            {
                return;
            }

            if (TryGetGroundPoint(blockObstacleHits: true, out Vector3 startPosition, out string placementFailureReason))
            {
                dragStartPosition = startPosition;
                dragCurrentPosition = startPosition;
                isDraggingPlacement = true;
                return;
            }

            if (!string.IsNullOrWhiteSpace(placementFailureReason))
            {
                Debug.LogWarning(placementFailureReason);
            }
        }

        if (!isDraggingPlacement)
        {
            return;
        }

        if (TryGetGroundPoint(blockObstacleHits: false, out Vector3 currentPosition, out _))
        {
            dragCurrentPosition = currentPosition;
        }

        if (!Input.GetMouseButtonUp(0))
        {
            return;
        }

        Bounds obstacleBounds = BuildObstacleBounds(dragStartPosition, dragCurrentPosition);
        if (!ValidateObstacleBounds(obstacleBounds, out string validationFailureReason))
        {
            Debug.LogWarning(validationFailureReason);
            CancelCurrentDrag();
            return;
        }

        CreateObstacle(obstacleBounds);
        CancelCurrentDrag();
    }

    private void HandleDeleteInput()
    {
        if (!isDeleteMode)
        {
            return;
        }

        if (Input.GetKeyDown(cancelPlacementKey))
        {
            isDeleteMode = false;
            Debug.Log("[RuntimeObstacleEditor] 已退出自定义障碍物删除模式。");
            return;
        }

        if (!Input.GetMouseButtonDown(0) || IsPointerOverUi())
        {
            return;
        }

        if (!CanEditObstacleLayout(out string failureReason))
        {
            Debug.LogWarning(failureReason);
            return;
        }

        Camera activeCamera = cameraManager != null ? cameraManager.GetActiveCamera() : Camera.main;
        if (activeCamera == null)
        {
            Debug.LogWarning("[RuntimeObstacleEditor] 当前没有可用相机，无法删除障碍物。");
            return;
        }

        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        LayerMask obstacleLayer = ResolveObstacleLayer();
        if (obstacleLayer.value == 0)
        {
            Debug.LogWarning("[RuntimeObstacleEditor] 当前未配置 Building 层，无法删除障碍物。");
            return;
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, obstacleLayer, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning("[RuntimeObstacleEditor] 当前点击位置没有命中可删除的自定义障碍物。");
            return;
        }

        RuntimeObstacleMarker marker = hit.collider != null ? hit.collider.GetComponentInParent<RuntimeObstacleMarker>() : null;
        if (marker == null)
        {
            Debug.LogWarning("[RuntimeObstacleEditor] 当前命中的是固定场景建筑，不支持删除。");
            return;
        }

        SimulationContext.GetOrCreate(this).UnregisterObstacle(marker);
        Destroy(marker.gameObject);
        RequestObstacleRefresh();
        Debug.Log($"[RuntimeObstacleEditor] 已删除自定义障碍物 #{marker.obstacleId:D2}。");
    }

    private bool CanEditObstacleLayout(out string failureReason)
    {
        failureReason = string.Empty;
        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            failureReason = "[RuntimeObstacleEditor] 仅可在仿真处于 Idle 状态时编辑障碍物，请先重置或停止当前仿真。";
            return false;
        }

        return true;
    }

    private bool IsPointerOverUi()
    {
        return UIInputGate.IsPointerOverBlockingUi();
    }

    private bool TryGetGroundPoint(bool blockObstacleHits, out Vector3 groundPosition, out string failureReason)
    {
        groundPosition = Vector3.zero;
        failureReason = string.Empty;

        Camera activeCamera = cameraManager != null ? cameraManager.GetActiveCamera() : Camera.main;
        if (activeCamera == null)
        {
            failureReason = "[RuntimeObstacleEditor] 当前没有可用相机，无法编辑障碍物。";
            return false;
        }

        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        LayerMask surfaceLayer = ResolvePlacementSurfaceLayer();
        LayerMask obstacleLayer = ResolveObstacleLayer();

        if (blockObstacleHits &&
            obstacleLayer.value != 0 &&
            Physics.Raycast(ray, out RaycastHit obstacleHit, maxPlacementDistance, obstacleLayer, QueryTriggerInteraction.Ignore))
        {
            RaycastHit surfaceHit = default;
            bool hasSurfaceHit =
                surfaceLayer.value != 0 &&
                Physics.Raycast(ray, out surfaceHit, maxPlacementDistance, surfaceLayer, QueryTriggerInteraction.Ignore);

            if (!hasSurfaceHit || obstacleHit.distance <= surfaceHit.distance + 0.01f)
            {
                groundPosition = obstacleHit.point;
                failureReason = "[RuntimeObstacleEditor] 当前点击位置被建筑或障碍物遮挡，请从空白地面开始拖拽。";
                return false;
            }
        }

        if (surfaceLayer.value != 0 &&
            Physics.Raycast(ray, out RaycastHit placementHit, maxPlacementDistance, surfaceLayer, QueryTriggerInteraction.Ignore))
        {
            groundPosition = placementHit.point;
            return true;
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            groundPosition = ray.GetPoint(enter);
            return true;
        }

        failureReason = "[RuntimeObstacleEditor] 当前鼠标位置没有命中可用地面。";
        return false;
    }

    private LayerMask ResolvePlacementSurfaceLayer()
    {
        if (placementSurfaceLayer.value != 0)
        {
            return placementSurfaceLayer;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
        {
            return 0;
        }

        return 1 << groundLayer;
    }

    private LayerMask ResolveObstacleLayer()
    {
        if (droneManager != null && droneManager.planningObstacleLayer.value != 0)
        {
            return droneManager.planningObstacleLayer;
        }

        int buildingLayer = LayerMask.NameToLayer("Building");
        if (buildingLayer < 0)
        {
            return 0;
        }

        return 1 << buildingLayer;
    }

    private Bounds BuildObstacleBounds(Vector3 startPosition, Vector3 endPosition)
    {
        float height = Mathf.Clamp(defaultObstacleHeight, minObstacleHeight, maxObstacleHeight);
        float width = Mathf.Max(minFootprintSize, Mathf.Abs(endPosition.x - startPosition.x));
        float depth = Mathf.Max(minFootprintSize, Mathf.Abs(endPosition.z - startPosition.z));
        float scaleMultiplier = GetDefaultObstacleScaleMultiplier();
        float groundY = Mathf.Min(startPosition.y, endPosition.y);

        width *= scaleMultiplier;
        height *= scaleMultiplier;
        depth *= scaleMultiplier;

        Vector3 center = new Vector3(
            (startPosition.x + endPosition.x) * 0.5f,
            groundY + height * 0.5f,
            (startPosition.z + endPosition.z) * 0.5f);

        Vector3 size = new Vector3(width, height, depth);
        return new Bounds(center, size);
    }

    private bool ValidateObstacleBounds(Bounds obstacleBounds, out string failureReason)
    {
        failureReason = string.Empty;

        if (DoesBoundsOverlapScenePoints(obstacleBounds, out string pointFailureReason))
        {
            failureReason = pointFailureReason;
            return false;
        }

        if (DoesBoundsOverlapObstacleFootprint(obstacleBounds))
        {
            failureReason = "[RuntimeObstacleEditor] 新建筑与现有建筑或障碍物重叠，请调整拖拽区域。";
            return false;
        }

        return true;
    }

    private bool DoesBoundsOverlapScenePoints(Bounds obstacleBounds, out string failureReason)
    {
        failureReason = string.Empty;

        SimulationContext context = SimulationContext.GetOrCreate(this);
        TaskPoint[] taskPoints = context.GetTaskPoints();
        for (int i = 0; i < taskPoints.Length; i++)
        {
            TaskPoint taskPoint = taskPoints[i];
            if (taskPoint == null)
            {
                continue;
            }

            if (IsPointInsideBoundsXZ(taskPoint.transform.position, obstacleBounds, pointClearancePadding))
            {
                failureReason = "[RuntimeObstacleEditor] 新建筑覆盖了任务点，请先调整任务点或建筑位置。";
                return true;
            }
        }

        DroneSpawnPointMarker[] spawnPointMarkers = context.GetSpawnPointMarkers();
        for (int i = 0; i < spawnPointMarkers.Length; i++)
        {
            DroneSpawnPointMarker spawnPointMarker = spawnPointMarkers[i];
            if (spawnPointMarker == null)
            {
                continue;
            }

            if (IsPointInsideBoundsXZ(spawnPointMarker.transform.position, obstacleBounds, pointClearancePadding))
            {
                failureReason = "[RuntimeObstacleEditor] 新建筑覆盖了起飞点，请先调整起飞点或建筑位置。";
                return true;
            }
        }

        return false;
    }

    private bool DoesBoundsOverlapObstacleFootprint(Bounds obstacleBounds)
    {
        LayerMask obstacleLayer = ResolveObstacleLayer();
        if (obstacleLayer.value == 0)
        {
            return false;
        }

        Vector3 extents = obstacleBounds.extents + new Vector3(overlapPadding, 0.1f, overlapPadding);
        Collider[] overlaps = Physics.OverlapBox(
            obstacleBounds.center,
            extents,
            Quaternion.identity,
            obstacleLayer,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlaps.Length; i++)
        {
            if (overlaps[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void CreateObstacle(Bounds obstacleBounds)
    {
        CacheReferences();
        Transform parentContainer = droneManager != null
            ? droneManager.EnsureRuntimeObstacleContainer()
            : null;

        RuntimeObstacleCatalog.Entry templateEntry = GetSelectedTemplateEntry();
        string templateDisplayName = templateEntry != null ? templateEntry.displayName : "长方体";

        GameObject obstacleObject = templateEntry != null
            ? Instantiate(templateEntry.prefab)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacleObject.name = $"RuntimeObstacle_{nextObstacleId:D2}";

        if (parentContainer != null)
        {
            obstacleObject.transform.SetParent(parentContainer, false);
        }

        int buildingLayer = LayerMask.NameToLayer("Building");
        if (buildingLayer >= 0)
        {
            ApplyLayerRecursively(obstacleObject, buildingLayer);
        }

        if (templateEntry != null)
        {
            FitPrefabObstacleToBounds(obstacleObject, obstacleBounds, templateEntry.preserveAspect);
            EnsureObstacleColliderIfNeeded(obstacleObject);
        }
        else
        {
            obstacleObject.transform.position = obstacleBounds.center;
            obstacleObject.transform.localScale = obstacleBounds.size;

            Renderer renderer = obstacleObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = obstacleColor;
            }
        }

        RuntimeObstacleMarker marker = obstacleObject.AddComponent<RuntimeObstacleMarker>();
        marker.obstacleId = nextObstacleId++;
        marker.templateDisplayName = templateDisplayName;
        SimulationContext.GetOrCreate(this).RegisterObstacle(marker);

        EnsureObstacleAwarePlanning();
        droneManager?.RefreshObstacleConfiguration();
        Debug.Log(
            $"[RuntimeObstacleEditor] 已创建自定义障碍物 #{marker.obstacleId:D2}，样式 {templateDisplayName}，中心 {obstacleBounds.center}，尺寸 {obstacleBounds.size}。");
    }

    private void EnsureObstacleAwarePlanning()
    {
        if (droneManager == null || droneManager.pathPlannerType != PathPlannerType.StraightLine)
        {
            return;
        }

        droneManager.pathPlannerType = PathPlannerType.AStar;
        droneManager.planningGridCellSize = Mathf.Min(droneManager.planningGridCellSize, 1.5f);
        Debug.Log("[RuntimeObstacleEditor] 检测到自定义障碍物，已自动将路径规划切换为 A* 以避免直线路径穿楼。");
    }

    private void FitPrefabObstacleToBounds(GameObject obstacleObject, Bounds obstacleBounds, bool preserveAspect)
    {
        if (obstacleObject == null)
        {
            return;
        }

        Vector3 originalScale = obstacleObject.transform.localScale;
        Quaternion originalLocalRotation = obstacleObject.transform.localRotation;

        obstacleObject.transform.localRotation = originalLocalRotation;
        obstacleObject.transform.localScale = originalScale;
        obstacleObject.transform.position = obstacleBounds.center;

        if (!TryCalculateRendererWorldBounds(obstacleObject.transform, out Bounds originalBounds))
        {
            obstacleObject.transform.position = obstacleBounds.center;
            obstacleObject.transform.localScale = obstacleBounds.size;
            return;
        }

        Vector3 sourceSize = originalBounds.size;
        sourceSize.x = Mathf.Max(sourceSize.x, 0.01f);
        sourceSize.y = Mathf.Max(sourceSize.y, 0.01f);
        sourceSize.z = Mathf.Max(sourceSize.z, 0.01f);

        Vector3 scaleFactors = new Vector3(
            obstacleBounds.size.x / sourceSize.x,
            obstacleBounds.size.y / sourceSize.y,
            obstacleBounds.size.z / sourceSize.z);

        if (preserveAspect)
        {
            float uniformScale = Mathf.Max(0.05f, Mathf.Min(scaleFactors.x, scaleFactors.y, scaleFactors.z));
            scaleFactors = Vector3.one * uniformScale;
        }

        obstacleObject.transform.localScale = Vector3.Scale(originalScale, scaleFactors);

        if (!TryCalculateRendererWorldBounds(obstacleObject.transform, out Bounds fittedBounds))
        {
            obstacleObject.transform.position = obstacleBounds.center;
            return;
        }

        Vector3 placementOffset = new Vector3(
            obstacleBounds.center.x - fittedBounds.center.x,
            obstacleBounds.min.y - fittedBounds.min.y,
            obstacleBounds.center.z - fittedBounds.center.z);
        obstacleObject.transform.position += placementOffset;
    }

    private void EnsureObstacleColliderIfNeeded(GameObject obstacleObject)
    {
        if (obstacleObject == null)
        {
            return;
        }

        Collider[] childColliders = obstacleObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childColliders.Length; i++)
        {
            if (childColliders[i] != null)
            {
                return;
            }
        }

        if (!TryCalculateLocalRendererBounds(obstacleObject.transform, out Bounds localBounds))
        {
            return;
        }

        BoxCollider boxCollider = obstacleObject.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = obstacleObject.AddComponent<BoxCollider>();
        }

        boxCollider.center = localBounds.center;
        boxCollider.size = localBounds.size;
        boxCollider.isTrigger = false;
    }

    private bool TryCalculateLocalRendererBounds(Transform target, out Bounds localBounds)
    {
        localBounds = default;
        if (target == null)
        {
            return false;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3 extents = rendererBounds.extents;
            Vector3 center = rendererBounds.center;
            Vector3[] corners =
            {
                center + new Vector3(-extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y, extents.z),
                center + new Vector3(-extents.x, extents.y, -extents.z),
                center + new Vector3(-extents.x, extents.y, extents.z),
                center + new Vector3(extents.x, -extents.y, -extents.z),
                center + new Vector3(extents.x, -extents.y, extents.z),
                center + new Vector3(extents.x, extents.y, -extents.z),
                center + new Vector3(extents.x, extents.y, extents.z)
            };

            for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
            {
                Vector3 localCorner = target.InverseTransformPoint(corners[cornerIndex]);
                if (!hasBounds)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }

        return hasBounds && localBounds.size.sqrMagnitude > 0.0001f;
    }

    private bool TryCalculateRendererWorldBounds(Transform target, out Bounds worldBounds)
    {
        worldBounds = default;
        if (target == null)
        {
            return false;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                worldBounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            worldBounds.Encapsulate(renderer.bounds);
        }

        return hasBounds && worldBounds.size.sqrMagnitude > 0.0001f;
    }

    private void ApplyLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            if (child != null)
            {
                ApplyLayerRecursively(child.gameObject, layer);
            }
        }
    }

    private void RequestObstacleRefresh()
    {
        SimulationContext.Current?.NotifyObstaclesChanged();

        if (droneManager == null)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            droneManager.RefreshObstacleConfiguration();
            return;
        }

        StartCoroutine(RefreshObstacleConfigurationNextFrame());
    }

    private System.Collections.IEnumerator RefreshObstacleConfigurationNextFrame()
    {
        yield return null;
        droneManager?.RefreshObstacleConfiguration();
    }

    private int GetMaxObstacleId()
    {
        int maxObstacleId = 0;
        RuntimeObstacleMarker[] markers = SimulationContext.GetOrCreate(this).GetRuntimeObstacleMarkers();
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null)
            {
                maxObstacleId = Mathf.Max(maxObstacleId, markers[i].obstacleId);
            }
        }

        return maxObstacleId;
    }

    private void UpdatePlacementPreview()
    {
        if (!showPlacementPreview || !isCreateMode || !isDraggingPlacement)
        {
            SetPreviewVisible(false);
            return;
        }

        EnsurePreviewObstacle();
        if (previewObstacle == null)
        {
            return;
        }

        Bounds previewBounds = BuildObstacleBounds(dragStartPosition, dragCurrentPosition);
        bool isValidPlacement = ValidateObstacleBounds(previewBounds, out _);
        Renderer previewRenderer = previewObstacle.GetComponent<Renderer>();
        if (previewRenderer != null)
        {
            previewRenderer.material.color = isValidPlacement ? validPreviewColor : invalidPreviewColor;
        }

        previewObstacle.transform.position = previewBounds.center;
        previewObstacle.transform.localScale = previewBounds.size;
        SetPreviewVisible(true);
    }

    private void EnsurePreviewObstacle()
    {
        if (previewObstacle != null)
        {
            return;
        }

        previewObstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        previewObstacle.name = "RuntimeObstaclePreview";
        previewObstacle.hideFlags = HideFlags.HideAndDontSave;

        if (disablePreviewCollider)
        {
            Collider collider = previewObstacle.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        Renderer renderer = previewObstacle.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader previewShader =
                Shader.Find("Legacy Shaders/Transparent/Diffuse") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Standard");
            renderer.material = new Material(previewShader);
            renderer.material.color = validPreviewColor;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        SetPreviewVisible(false);
    }

    private void SetPreviewVisible(bool isVisible)
    {
        if (previewObstacle != null)
        {
            previewObstacle.SetActive(isVisible);
        }
    }

    private void CancelCurrentDrag()
    {
        isDraggingPlacement = false;
        SetPreviewVisible(false);
    }

    private static bool IsPointInsideBoundsXZ(Vector3 point, Bounds bounds, float padding)
    {
        float minX = bounds.min.x - padding;
        float maxX = bounds.max.x + padding;
        float minZ = bounds.min.z - padding;
        float maxZ = bounds.max.z + padding;

        return point.x >= minX &&
               point.x <= maxX &&
               point.z >= minZ &&
               point.z <= maxZ;
    }

    private void OnDisable()
    {
        CancelCurrentDrag();
        isCreateMode = false;
        isDeleteMode = false;
    }

    private void OnDestroy()
    {
        if (previewObstacle != null)
        {
            Renderer previewRenderer = previewObstacle.GetComponent<Renderer>();
            if (previewRenderer != null && previewRenderer.material != null)
            {
                Destroy(previewRenderer.material);
            }

            Destroy(previewObstacle);
        }
    }
}
