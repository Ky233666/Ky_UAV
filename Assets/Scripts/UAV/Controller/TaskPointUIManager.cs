using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles task point creation and import buttons.
/// </summary>
public class TaskPointUIManager : MonoBehaviour
{
    [Header("Task Point Tools")]
    public TaskPointSpawner spawner;
    public TaskPointImporter importer;

    [Header("UI Buttons")]
    public Button addTaskButton;
    public Button clearButton;
    public Button importButton;

    [Header("Fallback Random Spawn")]
    public float spawnRadius = 20f;

    [Header("Interactive Placement")]
    public bool useClickPlacement = true;
    public CameraManager cameraManager;
    public LayerMask placementSurfaceLayer;
    public float maxPlacementDistance = 500f;
    public KeyCode cancelPlacementKey = KeyCode.Escape;
    public string addTaskIdleLabel = "新增任务点";
    public string addTaskPlacementLabel = "点击场景放置";

    [Header("Placement Preview")]
    public bool showPlacementPreview = true;
    public float previewRadius = 1.15f;
    public float previewHeightOffset = 0.08f;
    public float previewLineWidth = 0.08f;
    public int previewSegments = 40;
    public Color validPreviewColor = new Color(0.18f, 0.88f, 0.48f, 0.95f);
    public Color invalidPreviewColor = new Color(0.95f, 0.28f, 0.24f, 0.95f);

    private bool isPlacementMode;
    private TMP_Text addTaskButtonLabel;
    private LineRenderer placementPreviewRenderer;

    void Start()
    {
        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<CameraManager>();
        }

        addTaskButtonLabel = addTaskButton != null ? addTaskButton.GetComponentInChildren<TMP_Text>() : null;

        if (addTaskButton != null)
        {
            addTaskButton.onClick.AddListener(OnAddTaskClicked);
        }

        if (clearButton != null)
        {
            clearButton.onClick.AddListener(OnClearClicked);
        }

        if (importButton != null)
        {
            importButton.onClick.AddListener(OnImportClicked);
        }

        UpdateAddTaskButtonLabel();
    }

    void Update()
    {
        UpdatePlacementPreview();
        HandlePlacementInput();
    }

    public void OnAddTaskClicked()
    {
        if (spawner == null)
        {
            Debug.LogWarning("[TaskPointUIManager] 未设置 Spawner");
            return;
        }

        if (!useClickPlacement)
        {
            Vector3 randomPos = new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                0f,
                Random.Range(-spawnRadius, spawnRadius));

            spawner.SpawnTaskPoint(randomPos);
            return;
        }

        SetPlacementMode(!isPlacementMode);

        if (isPlacementMode)
        {
            if (cameraManager != null)
            {
                cameraManager.SwitchToOverview();
            }

            Debug.Log("[TaskPointUIManager] 已进入任务点放置模式，请在场景中左键点击空旷地面放置，按 Esc 取消。");
        }
        else
        {
            Debug.Log("[TaskPointUIManager] 已取消任务点放置模式。");
        }
    }

    public void OnClearClicked()
    {
        SetPlacementMode(false);

        if (spawner != null)
        {
            spawner.ClearAll();
        }

        if (TaskManager.Instance != null)
        {
            TaskManager.Instance.ResetAllTasks();
        }
    }

    public void OnImportClicked()
    {
        if (importer == null)
        {
            Debug.LogWarning("[TaskPointUIManager] 未设置 Importer");
            return;
        }

        SetPlacementMode(false);
        importer.ImportFromResources();
    }

    private void HandlePlacementInput()
    {
        if (!useClickPlacement || !isPlacementMode)
        {
            return;
        }

        if (Input.GetKeyDown(cancelPlacementKey))
        {
            SetPlacementMode(false);
            Debug.Log("[TaskPointUIManager] 已取消任务点放置。");
            return;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (TryGetPlacementPosition(out Vector3 placementPosition, out _, out string failureReason))
        {
            spawner.SpawnTaskPoint(placementPosition);
            SetPlacementMode(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            Debug.LogWarning(failureReason);
        }
        else
        {
            Debug.LogWarning("[TaskPointUIManager] 未能获取有效的放置位置，请点击空旷地面。");
        }
    }

    private bool TryGetPlacementPosition(out Vector3 placementPosition, out bool isValidPlacement, out string failureReason)
    {
        placementPosition = Vector3.zero;
        isValidPlacement = false;
        failureReason = string.Empty;

        Camera activeCamera = cameraManager != null ? cameraManager.GetActiveCamera() : Camera.main;
        if (activeCamera == null)
        {
            failureReason = "[TaskPointUIManager] 当前没有可用相机，无法放置任务点。";
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
            failureReason = "[TaskPointUIManager] 当前点击位置被建筑或障碍物遮挡，请点击空旷地面区域。";
            return false;
        }

        if (hasSurfaceHit)
        {
            bool isValidSurfacePoint = ValidatePlacementPoint(surfaceHit.point, out placementPosition, out failureReason);
            isValidPlacement = isValidSurfacePoint;
            return isValidSurfacePoint;
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            bool isValidPlanePoint = ValidatePlacementPoint(ray.GetPoint(enter), out placementPosition, out failureReason);
            isValidPlacement = isValidPlanePoint;
            return isValidPlanePoint;
        }

        failureReason = "[TaskPointUIManager] 当前鼠标位置没有命中可用地面。";
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
        if (DroneManager.Instance != null && DroneManager.Instance.planningObstacleLayer.value != 0)
        {
            return DroneManager.Instance.planningObstacleLayer;
        }

        int buildingLayer = LayerMask.NameToLayer("Building");
        if (buildingLayer < 0)
        {
            return 0;
        }

        return 1 << buildingLayer;
    }

    private bool ValidatePlacementPoint(Vector3 rawPoint, out Vector3 placementPosition, out string failureReason)
    {
        placementPosition = rawPoint;
        failureReason = string.Empty;

        if (spawner == null)
        {
            return true;
        }

        Vector3 groundedPoint = spawner.GetGroundedPosition(rawPoint);
        if (!spawner.IsPlacementSafe(groundedPoint))
        {
            failureReason = "[TaskPointUIManager] 当前点击位置位于建筑占地区域，请换一个空旷位置。";
            return false;
        }

        placementPosition = groundedPoint;
        return true;
    }

    private void SetPlacementMode(bool enabled)
    {
        isPlacementMode = enabled;
        UpdateAddTaskButtonLabel();

        if (!enabled)
        {
            SetPreviewVisible(false);
        }
    }

    private void UpdateAddTaskButtonLabel()
    {
        if (addTaskButtonLabel == null)
        {
            return;
        }

        addTaskButtonLabel.text = isPlacementMode ? addTaskPlacementLabel : addTaskIdleLabel;
    }

    private void UpdatePlacementPreview()
    {
        if (!showPlacementPreview || !useClickPlacement || !isPlacementMode)
        {
            SetPreviewVisible(false);
            return;
        }

        EnsurePreviewRenderer();
        if (placementPreviewRenderer == null)
        {
            return;
        }

        bool hasPlacementPoint = TryGetPlacementPosition(out Vector3 previewPosition, out bool isValidPlacement, out _);
        if (!hasPlacementPoint && previewPosition == Vector3.zero)
        {
            SetPreviewVisible(false);
            return;
        }

        Vector3 groundedPreviewPosition = spawner != null
            ? spawner.GetGroundedPosition(previewPosition)
            : new Vector3(previewPosition.x, previewHeightOffset, previewPosition.z);
        groundedPreviewPosition.y += previewHeightOffset;

        placementPreviewRenderer.startColor = isValidPlacement ? validPreviewColor : invalidPreviewColor;
        placementPreviewRenderer.endColor = isValidPlacement ? validPreviewColor : invalidPreviewColor;

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

        GameObject previewObject = new GameObject("TaskPointPlacementPreview");
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

    void OnDisable()
    {
        SetPreviewVisible(false);
    }

    void OnDestroy()
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
