using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Handles runtime placement, moving, and deletion of drone spawn points.
/// </summary>
public class DroneSpawnPointUIManager : MonoBehaviour
{
    [Header("References")]
    public SimulationManager simulationManager;
    public DroneManager droneManager;
    public CameraManager cameraManager;

    [Header("Interactive Placement")]
    public bool useClickPlacement = true;
    public LayerMask placementSurfaceLayer;
    public float maxPlacementDistance = 500f;
    public float minimumSpawnPointSpacing = 1.6f;
    public float deleteSelectionRadius = 1.4f;
    public float moveSelectionRadius = 1.4f;
    public KeyCode cancelPlacementKey = KeyCode.Escape;

    [Header("Preview")]
    public bool showPlacementPreview = true;
    public float previewRadius = 0.9f;
    public float previewHeightOffset = 0.08f;
    public float previewLineWidth = 0.08f;
    public int previewSegments = 36;
    public Color validPreviewColor = new Color(0.16f, 0.82f, 0.94f, 0.95f);
    public Color invalidPreviewColor = new Color(0.95f, 0.32f, 0.24f, 0.95f);

    [Header("Marker")]
    public Vector3 markerScale = new Vector3(0.85f, 0.06f, 0.85f);
    public float markerHeightOffset = 0.06f;
    public Color markerColor = new Color(0.16f, 0.82f, 0.94f, 0.92f);
    public float labelHeightOffset = 0.7f;
    public float labelFontSize = 3.2f;
    public Color labelColor = new Color(0.92f, 0.98f, 1f, 1f);
    public bool autoRespawnFleetWhenIdle = true;

    public bool IsPlacementMode => isPlacementMode;
    public bool IsDeleteMode => isDeleteMode;
    public bool IsMoveMode => isMoveMode;

    private bool isPlacementMode;
    private bool isDeleteMode;
    private bool isMoveMode;
    private int nextOrderIndex;
    private LineRenderer placementPreviewRenderer;
    private DroneSpawnPointMarker selectedMarkerForMove;
    private SimulationContext simulationContext;

    private void OnEnable()
    {
        simulationContext = SimulationContext.GetOrCreate(this);
        simulationContext.SpawnPointsChanged += HandleSpawnPointsChanged;
    }

    private void Start()
    {
        CacheReferences();
        nextOrderIndex = GetMaxOrderIndex() + 1;
        RefreshMarkerOrdering();
    }

    private void Update()
    {
        UpdatePlacementPreview();
        UpdateMarkerLabelFacing();
        HandleMoveInput();
        HandleDeleteInput();
        HandlePlacementInput();
    }

    public void TogglePlacementMode()
    {
        CacheReferences();
        SetInteractionModes(!isPlacementMode, false, false);

        if (isPlacementMode)
        {
            cameraManager?.SwitchToOverview();
            Debug.Log("[DroneSpawnPointUIManager] 已进入无人机起飞点放置模式，请点击地面放置起飞点，按 Esc 取消。");
        }
        else
        {
            Debug.Log("[DroneSpawnPointUIManager] 已取消无人机起飞点放置模式。");
        }
    }

    public void ToggleDeleteMode()
    {
        CacheReferences();
        SetInteractionModes(false, !isDeleteMode, false);

        if (isDeleteMode)
        {
            cameraManager?.SwitchToOverview();
            Debug.Log("[DroneSpawnPointUIManager] 已进入起飞点删除模式，请点击已有起飞点删除，按 Esc 取消。");
        }
        else
        {
            Debug.Log("[DroneSpawnPointUIManager] 已取消起飞点删除模式。");
        }
    }

    public void ToggleMoveMode()
    {
        CacheReferences();
        SetInteractionModes(false, false, !isMoveMode);

        if (isMoveMode)
        {
            cameraManager?.SwitchToOverview();
            Debug.Log("[DroneSpawnPointUIManager] 已进入起飞点移动模式，请先点击已有起飞点，再点击目标位置完成移动，按 Esc 取消。");
        }
        else
        {
            Debug.Log("[DroneSpawnPointUIManager] 已取消起飞点移动模式。");
        }
    }

    public void ClearSpawnPoints()
    {
        SetInteractionModes(false, false, false);

        SimulationContext context = SimulationContext.GetOrCreate(this);
        DroneSpawnPointMarker[] markers = context.GetSpawnPointMarkers();
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null)
            {
                context.UnregisterSpawnPoint(markers[i], false);
                Destroy(markers[i].gameObject);
            }
        }

        nextOrderIndex = 0;
        context.NotifySpawnPointsChanged();
        RespawnFleetIfAllowed();
        Debug.Log("[DroneSpawnPointUIManager] 已清空所有手动起飞点。");
    }

    public int GetSpawnPointCount()
    {
        return SimulationContext.GetOrCreate(this).GetSpawnPointMarkers().Length;
    }

    private void CacheReferences()
    {
        simulationManager = RuntimeSceneRegistry.Resolve(simulationManager, this);
        droneManager = RuntimeSceneRegistry.Resolve(
            droneManager,
            simulationManager != null ? simulationManager.droneManager : null,
            this);
        cameraManager = RuntimeSceneRegistry.Resolve(cameraManager, this);
    }

    private void HandlePlacementInput()
    {
        if (!useClickPlacement || !isPlacementMode)
        {
            return;
        }

        if (Input.GetKeyDown(cancelPlacementKey))
        {
            SetInteractionModes(false, false, false);
            Debug.Log("[DroneSpawnPointUIManager] 已取消起飞点放置。");
            return;
        }

        if (!Input.GetMouseButtonDown(0) || IsPointerOverUi())
        {
            return;
        }

        if (TryGetPlacementPosition(out Vector3 placementPosition, out _, out string failureReason))
        {
            CreateSpawnPointMarker(placementPosition);
            SetInteractionModes(false, false, false);
            RespawnFleetIfAllowed();
            return;
        }

        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            Debug.LogWarning(failureReason);
        }
    }

    private void HandleDeleteInput()
    {
        if (!useClickPlacement || !isDeleteMode)
        {
            return;
        }

        if (Input.GetKeyDown(cancelPlacementKey))
        {
            SetInteractionModes(false, false, false);
            Debug.Log("[DroneSpawnPointUIManager] 已取消起飞点删除。");
            return;
        }

        if (!Input.GetMouseButtonDown(0) || IsPointerOverUi())
        {
            return;
        }

        if (TryGetGroundPoint(out Vector3 clickPosition, out string failureReason))
        {
            if (TryDeleteClosestSpawnPoint(clickPosition))
            {
                SetInteractionModes(false, false, false);
                RespawnFleetIfAllowed();
            }
            else
            {
                Debug.LogWarning("[DroneSpawnPointUIManager] 当前点击位置附近没有可删除的起飞点。");
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            Debug.LogWarning(failureReason);
        }
    }

    private void HandleMoveInput()
    {
        if (!useClickPlacement || !isMoveMode)
        {
            return;
        }

        if (Input.GetKeyDown(cancelPlacementKey))
        {
            SetInteractionModes(false, false, false);
            Debug.Log("[DroneSpawnPointUIManager] 已取消起飞点移动。");
            return;
        }

        if (!Input.GetMouseButtonDown(0) || IsPointerOverUi())
        {
            return;
        }

        if (selectedMarkerForMove == null)
        {
            if (TryGetGroundPoint(out Vector3 selectionPosition, out string selectionFailureReason))
            {
                selectedMarkerForMove = FindClosestMarker(selectionPosition, Mathf.Max(0.6f, moveSelectionRadius));
                if (selectedMarkerForMove != null)
                {
                    Debug.Log($"[DroneSpawnPointUIManager] 已选中起飞点 {selectedMarkerForMove.orderIndex + 1:D2}，请点击新的位置。");
                }
                else
                {
                    Debug.LogWarning("[DroneSpawnPointUIManager] 当前点击位置附近没有可移动的起飞点。");
                }
            }
            else if (!string.IsNullOrWhiteSpace(selectionFailureReason))
            {
                Debug.LogWarning(selectionFailureReason);
            }

            return;
        }

        if (TryGetPlacementPosition(selectedMarkerForMove, out Vector3 placementPosition, out _, out string failureReason))
        {
            selectedMarkerForMove.transform.position = placementPosition;
            EnsureMarkerLabel(selectedMarkerForMove);
            Debug.Log($"[DroneSpawnPointUIManager] 已移动起飞点 {selectedMarkerForMove.orderIndex + 1:D2}。");
            SetInteractionModes(false, false, false);
            RespawnFleetIfAllowed();
            return;
        }

        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            Debug.LogWarning(failureReason);
        }
    }

    private bool IsPointerOverUi()
    {
        return UIInputGate.IsPointerOverBlockingUi();
    }

    private bool TryGetPlacementPosition(out Vector3 placementPosition, out bool isValidPlacement, out string failureReason)
    {
        return TryGetPlacementPosition(null, out placementPosition, out isValidPlacement, out failureReason);
    }

    private bool TryGetPlacementPosition(
        DroneSpawnPointMarker ignoredMarker,
        out Vector3 placementPosition,
        out bool isValidPlacement,
        out string failureReason)
    {
        placementPosition = Vector3.zero;
        isValidPlacement = false;
        failureReason = string.Empty;

        Camera activeCamera = cameraManager != null ? cameraManager.GetActiveCamera() : Camera.main;
        if (activeCamera == null)
        {
            failureReason = "[DroneSpawnPointUIManager] 当前没有可用相机，无法放置起飞点。";
            return false;
        }

        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        LayerMask obstacleLayer = ResolveObstacleLayer();
        LayerMask surfaceLayer = ResolvePlacementSurfaceLayer();
        RaycastHit obstacleHit = default;
        RaycastHit surfaceHit = default;

        bool hasObstacleHit = obstacleLayer.value != 0 &&
            Physics.Raycast(ray, out obstacleHit, maxPlacementDistance, obstacleLayer, QueryTriggerInteraction.Ignore);
        bool hasSurfaceHit = surfaceLayer.value != 0 &&
            Physics.Raycast(ray, out surfaceHit, maxPlacementDistance, surfaceLayer, QueryTriggerInteraction.Ignore);

        if (hasObstacleHit && (!hasSurfaceHit || obstacleHit.distance <= surfaceHit.distance + 0.01f))
        {
            placementPosition = obstacleHit.point;
            failureReason = "[DroneSpawnPointUIManager] 当前点击位置被建筑或障碍物遮挡，请点击空旷地面区域。";
            return false;
        }

        if (hasSurfaceHit)
        {
            bool isValidSurfacePoint = ValidatePlacementPoint(surfaceHit.point, ignoredMarker, out placementPosition, out failureReason);
            isValidPlacement = isValidSurfacePoint;
            return isValidSurfacePoint;
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            bool isValidPlanePoint = ValidatePlacementPoint(ray.GetPoint(enter), ignoredMarker, out placementPosition, out failureReason);
            isValidPlacement = isValidPlanePoint;
            return isValidPlanePoint;
        }

        failureReason = "[DroneSpawnPointUIManager] 当前鼠标位置没有命中可用地面。";
        return false;
    }

    private bool TryGetGroundPoint(out Vector3 position, out string failureReason)
    {
        position = Vector3.zero;
        failureReason = string.Empty;

        Camera activeCamera = cameraManager != null ? cameraManager.GetActiveCamera() : Camera.main;
        if (activeCamera == null)
        {
            failureReason = "[DroneSpawnPointUIManager] 当前没有可用相机。";
            return false;
        }

        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        LayerMask surfaceLayer = ResolvePlacementSurfaceLayer();
        if (surfaceLayer.value != 0 &&
            Physics.Raycast(ray, out RaycastHit surfaceHit, maxPlacementDistance, surfaceLayer, QueryTriggerInteraction.Ignore))
        {
            position = GetGroundedPosition(surfaceHit.point);
            return true;
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            position = GetGroundedPosition(ray.GetPoint(enter));
            return true;
        }

        failureReason = "[DroneSpawnPointUIManager] 当前鼠标位置没有命中可用地面。";
        return false;
    }

    private bool ValidatePlacementPoint(Vector3 rawPoint, out Vector3 placementPosition, out string failureReason)
    {
        return ValidatePlacementPoint(rawPoint, null, out placementPosition, out failureReason);
    }

    private bool ValidatePlacementPoint(
        Vector3 rawPoint,
        DroneSpawnPointMarker ignoredMarker,
        out Vector3 placementPosition,
        out string failureReason)
    {
        placementPosition = GetGroundedPosition(rawPoint);
        failureReason = string.Empty;

        DroneSpawnPointMarker[] existingMarkers = SimulationContext.GetOrCreate(this).GetSpawnPointMarkers();
        float minimumDistance = Mathf.Max(0.5f, minimumSpawnPointSpacing);
        for (int i = 0; i < existingMarkers.Length; i++)
        {
            DroneSpawnPointMarker marker = existingMarkers[i];
            if (marker == null || marker == ignoredMarker)
            {
                continue;
            }

            Vector3 markerPosition = marker.transform.position;
            markerPosition.y = placementPosition.y;
            if (Vector3.Distance(markerPosition, placementPosition) < minimumDistance)
            {
                failureReason = "[DroneSpawnPointUIManager] 起飞点之间距离太近，请拉开一点。";
                return false;
            }
        }

        return true;
    }

    private Vector3 GetGroundedPosition(Vector3 position)
    {
        return new Vector3(position.x, markerHeightOffset, position.z);
    }

    private void CreateSpawnPointMarker(Vector3 position)
    {
        GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        markerObject.name = $"DroneSpawnPoint_{nextOrderIndex + 1:D2}";
        markerObject.transform.position = position;
        markerObject.transform.localScale = markerScale;

        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer >= 0)
        {
            markerObject.layer = ignoreRaycastLayer;
        }

        DroneSpawnPointMarker marker = markerObject.AddComponent<DroneSpawnPointMarker>();
        marker.orderIndex = nextOrderIndex++;
        SimulationContext.GetOrCreate(this).RegisterSpawnPoint(marker);

        TryAssignSpawnPointTag(markerObject);

        Collider collider = markerObject.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        Renderer renderer = markerObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material.color = markerColor;
        }

        EnsureMarkerLabel(marker);
        RefreshMarkerOrdering();
    }

    private void TryAssignSpawnPointTag(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        try
        {
            target.tag = "SpawnPoint";
        }
        catch (UnityException)
        {
            // The custom marker component is enough for runtime discovery.
        }
    }

    private void RespawnFleetIfAllowed()
    {
        CacheReferences();

        if (!autoRespawnFleetWhenIdle || droneManager == null)
        {
            return;
        }

        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            return;
        }

        droneManager.RespawnDrones(droneManager.droneCount);
    }

    private bool TryDeleteClosestSpawnPoint(Vector3 clickPosition)
    {
        DroneSpawnPointMarker closestMarker = FindClosestMarker(clickPosition, Mathf.Max(0.6f, deleteSelectionRadius));
        if (closestMarker == null)
        {
            return false;
        }

        if (selectedMarkerForMove == closestMarker)
        {
            selectedMarkerForMove = null;
        }

        SimulationContext.GetOrCreate(this).UnregisterSpawnPoint(closestMarker);
        Destroy(closestMarker.gameObject);
        RefreshMarkerOrdering();
        return true;
    }

    private DroneSpawnPointMarker FindClosestMarker(Vector3 position, float maxDistance)
    {
        DroneSpawnPointMarker closestMarker = null;
        float bestDistance = maxDistance;
        DroneSpawnPointMarker[] markers = SimulationContext.GetOrCreate(this).GetSpawnPointMarkers();
        for (int i = 0; i < markers.Length; i++)
        {
            DroneSpawnPointMarker marker = markers[i];
            if (marker == null)
            {
                continue;
            }

            Vector3 markerPosition = marker.transform.position;
            markerPosition.y = position.y;
            float distance = Vector3.Distance(markerPosition, position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                closestMarker = marker;
            }
        }

        return closestMarker;
    }

    private int GetMaxOrderIndex()
    {
        int maxOrder = -1;
        DroneSpawnPointMarker[] markers = SimulationContext.GetOrCreate(this).GetSpawnPointMarkers();
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null)
            {
                maxOrder = Mathf.Max(maxOrder, markers[i].orderIndex);
            }
        }

        return maxOrder;
    }

    private void RefreshMarkerOrdering()
    {
        List<DroneSpawnPointMarker> markers =
            new List<DroneSpawnPointMarker>(SimulationContext.GetOrCreate(this).GetSpawnPointMarkers());
        markers.Sort((left, right) =>
        {
            int orderCompare = left.orderIndex.CompareTo(right.orderIndex);
            return orderCompare != 0 ? orderCompare : string.CompareOrdinal(left.name, right.name);
        });

        for (int i = 0; i < markers.Count; i++)
        {
            DroneSpawnPointMarker marker = markers[i];
            if (marker == null)
            {
                continue;
            }

            marker.orderIndex = i;
            marker.name = $"DroneSpawnPoint_{i + 1:D2}";
            EnsureMarkerLabel(marker);
            UpdateMarkerLabel(marker, i + 1);
        }

        nextOrderIndex = markers.Count;
    }

    private void EnsureMarkerLabel(DroneSpawnPointMarker marker)
    {
        if (marker == null)
        {
            return;
        }

        Transform existing = marker.transform.Find("Label");
        TextMeshPro label = existing != null ? existing.GetComponent<TextMeshPro>() : null;
        if (label == null)
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(marker.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, labelHeightOffset, 0f);
            label = labelObject.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.outlineWidth = 0.18f;
            label.outlineColor = new Color(0.02f, 0.08f, 0.12f, 0.95f);
            label.sortingOrder = 20;
        }
    }

    private void UpdateMarkerLabel(DroneSpawnPointMarker marker, int displayIndex)
    {
        if (marker == null)
        {
            return;
        }

        Transform labelTransform = marker.transform.Find("Label");
        TextMeshPro label = labelTransform != null ? labelTransform.GetComponent<TextMeshPro>() : null;
        if (label == null)
        {
            return;
        }

        label.text = displayIndex.ToString("D2");
        label.fontSize = labelFontSize;
        label.color = labelColor;
        label.transform.localPosition = new Vector3(0f, labelHeightOffset, 0f);
        label.transform.localScale = Vector3.one * 0.18f;
    }

    private void UpdateMarkerLabelFacing()
    {
        Camera activeCamera = cameraManager != null ? cameraManager.GetActiveCamera() : Camera.main;
        if (activeCamera == null)
        {
            return;
        }

        DroneSpawnPointMarker[] markers = SimulationContext.GetOrCreate(this).GetSpawnPointMarkers();
        for (int i = 0; i < markers.Length; i++)
        {
            Transform labelTransform = markers[i] != null ? markers[i].transform.Find("Label") : null;
            if (labelTransform == null)
            {
                continue;
            }

            Vector3 direction = labelTransform.position - activeCamera.transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                labelTransform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
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

    private void SetInteractionModes(bool placementEnabled, bool deleteEnabled, bool moveEnabled)
    {
        isPlacementMode = placementEnabled;
        isDeleteMode = deleteEnabled;
        isMoveMode = moveEnabled;

        if (!moveEnabled)
        {
            selectedMarkerForMove = null;
        }

        if (!placementEnabled && (!moveEnabled || selectedMarkerForMove == null))
        {
            SetPreviewVisible(false);
        }
    }

    private void UpdatePlacementPreview()
    {
        bool showPlacementPreviewRing = isPlacementMode && !isDeleteMode;
        bool showMovePreviewRing = isMoveMode && selectedMarkerForMove != null;
        if (!showPlacementPreview || !useClickPlacement || (!showPlacementPreviewRing && !showMovePreviewRing))
        {
            SetPreviewVisible(false);
            return;
        }

        EnsurePreviewRenderer();
        if (placementPreviewRenderer == null)
        {
            return;
        }

        Vector3 previewPosition;
        bool isValidPlacement;
        bool hasPlacementPoint = showMovePreviewRing
            ? TryGetPlacementPosition(selectedMarkerForMove, out previewPosition, out isValidPlacement, out _)
            : TryGetPlacementPosition(out previewPosition, out isValidPlacement, out _);
        if (!hasPlacementPoint && previewPosition == Vector3.zero)
        {
            SetPreviewVisible(false);
            return;
        }

        Vector3 groundedPreviewPosition = GetGroundedPosition(previewPosition);
        groundedPreviewPosition.y += previewHeightOffset;

        Color previewColor = isValidPlacement ? validPreviewColor : invalidPreviewColor;
        placementPreviewRenderer.startColor = previewColor;
        placementPreviewRenderer.endColor = previewColor;

        int segmentCount = Mathf.Max(12, previewSegments);
        float radius = Mathf.Max(0.2f, previewRadius);
        for (int i = 0; i <= segmentCount; i++)
        {
            float angle = (float)i / segmentCount * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            placementPreviewRenderer.SetPosition(i, groundedPreviewPosition + offset);
        }

        SetPreviewVisible(true);
    }

    private void EnsurePreviewRenderer()
    {
        if (placementPreviewRenderer != null)
        {
            return;
        }

        GameObject previewObject = new GameObject("DroneSpawnPointPlacementPreview");
        previewObject.hideFlags = HideFlags.HideAndDontSave;
        placementPreviewRenderer = previewObject.AddComponent<LineRenderer>();
        placementPreviewRenderer.loop = false;
        placementPreviewRenderer.useWorldSpace = true;
        placementPreviewRenderer.alignment = LineAlignment.View;
        placementPreviewRenderer.widthMultiplier = previewLineWidth;
        placementPreviewRenderer.positionCount = Mathf.Max(12, previewSegments) + 1;
        placementPreviewRenderer.numCornerVertices = 4;
        placementPreviewRenderer.numCapVertices = 4;
        placementPreviewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        placementPreviewRenderer.receiveShadows = false;
        placementPreviewRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        placementPreviewRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        placementPreviewRenderer.textureMode = LineTextureMode.Stretch;
        placementPreviewRenderer.material = new Material(Shader.Find("Sprites/Default"));
        placementPreviewRenderer.enabled = false;
    }

    private void SetPreviewVisible(bool visible)
    {
        if (placementPreviewRenderer != null)
        {
            placementPreviewRenderer.enabled = visible;
        }
    }

    private void OnDisable()
    {
        SetPreviewVisible(false);
        if (simulationContext != null)
        {
            simulationContext.SpawnPointsChanged -= HandleSpawnPointsChanged;
            simulationContext = null;
        }
    }

    private void HandleSpawnPointsChanged()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        nextOrderIndex = GetMaxOrderIndex() + 1;
        RefreshMarkerOrdering();
    }

    private void OnDestroy()
    {
        if (placementPreviewRenderer != null)
        {
            if (placementPreviewRenderer.material != null)
            {
                Destroy(placementPreviewRenderer.material);
            }

            Destroy(placementPreviewRenderer.gameObject);
        }
    }
}
