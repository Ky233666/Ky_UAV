using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages overview, follow, and top-down 2D inspection cameras.
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Header("Camera References")]
    public Camera overviewCamera;
    public Camera followCamera;

    [Header("Overview Controls")]
    public float overviewMoveSpeed = 18f;
    public float overviewBoostMultiplier = 2.5f;
    public float overviewVerticalMoveSpeed = 12f;
    public float overviewRotationSensitivity = 3f;
    public float overviewZoomSpeed = 35f;
    public float overviewMinHeight = 8f;
    public float overviewMaxHeight = 90f;
    public bool requireRightMouseForOverviewRotation = true;
    public bool blockMouseSceneControlsWhenPointerOverUi = true;

    [Header("Follow Settings")]
    public Transform targetDrone;
    public Vector3 followOffset = new Vector3(0f, 5f, -10f);
    public Vector3 lookAtOffset = new Vector3(0f, 1.5f, 0f);
    public float followSmoothTime = 6f;

    [Header("Top Down 2D Settings")]
    public float topDownHeight = 48f;
    public float topDownPanSpeed = 20f;
    public float topDownZoomSpeed = 10f;
    public float topDownMinOrthographicSize = 8f;
    public float topDownMaxOrthographicSize = 120f;
    public float topDownFramePadding = 10f;

    [Header("Current Mode")]
    public bool isOverview = true;
    public bool isTopDown2D = false;

    private readonly List<DroneController> managedDrones = new List<DroneController>();
    private int currentFollowIndex = -1;
    private float overviewYaw;
    private float overviewPitch;
    private bool hasStoredOverviewPose;
    private Vector3 storedOverviewPosition;
    private Quaternion storedOverviewRotation;
    private bool storedOverviewOrthographic;
    private float storedOverviewOrthographicSize;
    private float storedOverviewFieldOfView;

    void Start()
    {
        InitializeOverviewCameraState();
        RefreshManagedDrones();
        UpdateCameraState();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SwitchToOverview();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SwitchToFollow();
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SwitchToTopDown2D();
        }

        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.E))
        {
            FocusNextDrone();
        }

        if (Input.GetKeyDown(KeyCode.Q) && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
        {
            FocusPreviousDrone();
        }

        if (isTopDown2D)
        {
            UpdateTopDownCamera();
            return;
        }

        if (isOverview)
        {
            UpdateOverviewCamera();
        }
        else
        {
            EnsureFollowTarget();
            UpdateFollowCamera();
        }
    }

    void UpdateCameraState()
    {
        bool useOverviewCamera = isOverview || isTopDown2D;

        if (overviewCamera != null)
        {
            overviewCamera.enabled = useOverviewCamera;
            AudioListener overviewListener = overviewCamera.GetComponent<AudioListener>();
            if (overviewListener != null)
            {
                overviewListener.enabled = useOverviewCamera;
            }
        }

        if (followCamera != null)
        {
            followCamera.enabled = !useOverviewCamera;
            AudioListener followListener = followCamera.GetComponent<AudioListener>();
            if (followListener != null)
            {
                followListener.enabled = !useOverviewCamera;
            }
        }
    }

    public Camera GetActiveCamera()
    {
        if (!isOverview && !isTopDown2D && followCamera != null)
        {
            return followCamera;
        }

        if (overviewCamera != null)
        {
            return overviewCamera;
        }

        return Camera.main;
    }

    public string GetCurrentModeIdentifier()
    {
        if (isTopDown2D)
        {
            return "TopDown2D";
        }

        return isOverview ? "Overview" : "Follow";
    }

    public string GetCurrentModeDisplayName()
    {
        if (isTopDown2D)
        {
            return "2D俯视";
        }

        return isOverview ? "总览" : "跟随";
    }

    public void SwitchToOverview()
    {
        if (hasStoredOverviewPose)
        {
            RestoreStoredOverviewPose();
        }

        isTopDown2D = false;
        isOverview = true;
        InitializeOverviewCameraState();
        UpdateCameraState();
        ApplyPathProjectionMode();
        Debug.Log("[CameraManager] 已切换到总览视角");
    }

    public void SwitchToFollow()
    {
        if (isTopDown2D)
        {
            RestoreStoredOverviewPose();
        }

        isTopDown2D = false;
        isOverview = false;
        EnsureFollowTarget();
        UpdateCameraState();
        ApplyPathProjectionMode();
        Debug.Log("[CameraManager] 已切换到跟随视角");
    }

    public void SwitchToTopDown2D()
    {
        if (overviewCamera == null)
        {
            SwitchToOverview();
            return;
        }

        if (!isTopDown2D)
        {
            StoreOverviewPose();
        }

        isOverview = true;
        isTopDown2D = true;
        ApplyTopDownFrame(true);
        UpdateCameraState();
        ApplyPathProjectionMode();
        Debug.Log("[CameraManager] 已切换到2D俯视轨迹视图");
    }

    public void RefreshManagedDrones()
    {
        managedDrones.Clear();
        if (DroneManager.Instance != null)
        {
            foreach (DroneController drone in DroneManager.Instance.drones)
            {
                if (drone != null)
                {
                    managedDrones.Add(drone);
                }
            }
        }

        if (managedDrones.Count == 0)
        {
            targetDrone = null;
            currentFollowIndex = -1;
            ApplyPathProjectionMode();
            return;
        }

        if (targetDrone == null)
        {
            SetFollowTarget(0);
            ApplyPathProjectionMode();
            return;
        }

        for (int i = 0; i < managedDrones.Count; i++)
        {
            if (managedDrones[i] != null && managedDrones[i].transform == targetDrone)
            {
                currentFollowIndex = i;
                ApplyPathProjectionMode();
                return;
            }
        }

        SetFollowTarget(0);
        ApplyPathProjectionMode();
    }

    public void FocusNextDrone()
    {
        if (isTopDown2D)
        {
            RestoreStoredOverviewPose();
        }

        if (isOverview || isTopDown2D)
        {
            isOverview = false;
            isTopDown2D = false;
        }

        EnsureFollowTarget();
        if (managedDrones.Count == 0)
        {
            return;
        }

        int nextIndex = currentFollowIndex < 0 ? 0 : (currentFollowIndex + 1) % managedDrones.Count;
        SetFollowTarget(nextIndex);
        UpdateCameraState();
        ApplyPathProjectionMode();
    }

    public void FocusPreviousDrone()
    {
        if (isTopDown2D)
        {
            RestoreStoredOverviewPose();
        }

        if (isOverview || isTopDown2D)
        {
            isOverview = false;
            isTopDown2D = false;
        }

        EnsureFollowTarget();
        if (managedDrones.Count == 0)
        {
            return;
        }

        int previousIndex = currentFollowIndex < 0
            ? managedDrones.Count - 1
            : (currentFollowIndex - 1 + managedDrones.Count) % managedDrones.Count;
        SetFollowTarget(previousIndex);
        UpdateCameraState();
        ApplyPathProjectionMode();
    }

    public void SetFollowOffset(Vector3 offset)
    {
        followOffset = offset;

        if (!isOverview && !isTopDown2D)
        {
            UpdateFollowCamera();
        }
    }

    private void EnsureFollowTarget()
    {
        if (managedDrones.Count == 0 || HasInvalidManagedDrones())
        {
            RefreshManagedDrones();
        }

        if (targetDrone == null && managedDrones.Count > 0)
        {
            SetFollowTarget(0);
        }
    }

    private bool HasInvalidManagedDrones()
    {
        foreach (DroneController drone in managedDrones)
        {
            if (drone == null)
            {
                return true;
            }
        }

        return false;
    }

    private void SetFollowTarget(int index)
    {
        if (index < 0 || index >= managedDrones.Count)
        {
            return;
        }

        DroneController drone = managedDrones[index];
        if (drone == null)
        {
            return;
        }

        currentFollowIndex = index;
        targetDrone = drone.transform;
        Debug.Log($"[CameraManager] 当前跟随目标切换为：{drone.droneName}");
    }

    private void UpdateFollowCamera()
    {
        if (followCamera == null || targetDrone == null)
        {
            return;
        }

        Vector3 desiredPosition = targetDrone.position + followOffset;
        float lerpFactor = Mathf.Clamp01(followSmoothTime * Time.deltaTime);
        followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, desiredPosition, lerpFactor);
        followCamera.transform.LookAt(targetDrone.position + lookAtOffset);
    }

    private void InitializeOverviewCameraState()
    {
        if (overviewCamera == null)
        {
            return;
        }

        Vector3 euler = overviewCamera.transform.rotation.eulerAngles;
        overviewYaw = euler.y;
        overviewPitch = NormalizePitch(euler.x);
    }

    private void UpdateOverviewCamera()
    {
        if (overviewCamera == null)
        {
            return;
        }

        Transform cameraTransform = overviewCamera.transform;
        bool blockMouseSceneInput = blockMouseSceneControlsWhenPointerOverUi && UIInputGate.IsPointerOverBlockingUi();

        Vector3 flatForward = cameraTransform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = Vector3.forward;
        }
        flatForward.Normalize();

        Vector3 flatRight = cameraTransform.right;
        flatRight.y = 0f;
        if (flatRight.sqrMagnitude < 0.001f)
        {
            flatRight = Vector3.right;
        }
        flatRight.Normalize();

        float moveMultiplier = Input.GetKey(KeyCode.LeftShift) ? overviewBoostMultiplier : 1f;
        Vector3 horizontalMove =
            flatForward * Input.GetAxisRaw("Vertical") +
            flatRight * Input.GetAxisRaw("Horizontal");

        float verticalInput = 0f;
        if (Input.GetKey(KeyCode.R))
        {
            verticalInput += 1f;
        }
        if (Input.GetKey(KeyCode.F))
        {
            verticalInput -= 1f;
        }

        cameraTransform.position +=
            horizontalMove * overviewMoveSpeed * moveMultiplier * Time.deltaTime +
            Vector3.up * verticalInput * overviewVerticalMoveSpeed * Time.deltaTime;

        float scroll = Input.mouseScrollDelta.y;
        if (!blockMouseSceneInput && Mathf.Abs(scroll) > 0.001f)
        {
            cameraTransform.position += cameraTransform.forward * (scroll * overviewZoomSpeed * Time.deltaTime);
        }

        bool allowRotation =
            !blockMouseSceneInput &&
            (!requireRightMouseForOverviewRotation || Input.GetMouseButton(1));
        if (allowRotation)
        {
            overviewYaw += Input.GetAxis("Mouse X") * overviewRotationSensitivity;
            overviewPitch -= Input.GetAxis("Mouse Y") * overviewRotationSensitivity;
            overviewPitch = Mathf.Clamp(overviewPitch, -80f, 80f);
            cameraTransform.rotation = Quaternion.Euler(overviewPitch, overviewYaw, 0f);
        }

        Vector3 clampedPosition = cameraTransform.position;
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, overviewMinHeight, overviewMaxHeight);
        cameraTransform.position = clampedPosition;
    }

    private void UpdateTopDownCamera()
    {
        if (overviewCamera == null)
        {
            return;
        }

        Transform cameraTransform = overviewCamera.transform;
        bool blockMouseSceneInput = blockMouseSceneControlsWhenPointerOverUi && UIInputGate.IsPointerOverBlockingUi();
        float moveMultiplier = Input.GetKey(KeyCode.LeftShift) ? overviewBoostMultiplier : 1f;
        Vector3 panInput = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        cameraTransform.position += panInput * (topDownPanSpeed * moveMultiplier * Time.deltaTime);

        float scroll = Input.mouseScrollDelta.y;
        if (!blockMouseSceneInput && Mathf.Abs(scroll) > 0.001f)
        {
            overviewCamera.orthographicSize = Mathf.Clamp(
                overviewCamera.orthographicSize - scroll * topDownZoomSpeed,
                topDownMinOrthographicSize,
                topDownMaxOrthographicSize);
        }

        float topDownY = ResolveTopDownHeight();
        cameraTransform.position = new Vector3(cameraTransform.position.x, topDownY, cameraTransform.position.z);
        cameraTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
        overviewCamera.orthographic = true;
    }

    private void ApplyTopDownFrame(bool refitSize)
    {
        if (overviewCamera == null)
        {
            return;
        }

        Vector3 center;
        Vector3 size;
        CalculateTopDownBounds(out center, out size);

        overviewCamera.orthographic = true;
        overviewCamera.transform.position = new Vector3(center.x, ResolveTopDownHeight(), center.z);
        overviewCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (refitSize)
        {
            float targetSize = Mathf.Max(size.x, size.z) * 0.5f + topDownFramePadding;
            overviewCamera.orthographicSize = Mathf.Clamp(
                targetSize,
                topDownMinOrthographicSize,
                topDownMaxOrthographicSize);
        }
    }

    private float ResolveTopDownHeight()
    {
        float minimumHeight = topDownHeight;
        if (DroneManager.Instance != null)
        {
            minimumHeight = Mathf.Max(minimumHeight, DroneManager.Instance.CalculatePathProjectionHeight() + 6f);
        }

        return minimumHeight;
    }

    private void CalculateTopDownBounds(out Vector3 center, out Vector3 size)
    {
        if (DroneManager.Instance != null)
        {
            Vector3 worldMin = DroneManager.Instance.planningWorldMin;
            Vector3 worldMax = DroneManager.Instance.planningWorldMax;
            Bounds bounds = new Bounds((worldMin + worldMax) * 0.5f, worldMax - worldMin);

            for (int i = 0; i < managedDrones.Count; i++)
            {
                DroneController drone = managedDrones[i];
                if (drone != null)
                {
                    bounds.Encapsulate(drone.transform.position);
                }
            }

            center = bounds.center;
            size = bounds.size;
            return;
        }

        center = Vector3.zero;
        size = new Vector3(60f, 1f, 60f);
    }

    private void StoreOverviewPose()
    {
        if (overviewCamera == null)
        {
            return;
        }

        Transform cameraTransform = overviewCamera.transform;
        hasStoredOverviewPose = true;
        storedOverviewPosition = cameraTransform.position;
        storedOverviewRotation = cameraTransform.rotation;
        storedOverviewOrthographic = overviewCamera.orthographic;
        storedOverviewOrthographicSize = overviewCamera.orthographicSize;
        storedOverviewFieldOfView = overviewCamera.fieldOfView;
    }

    private void RestoreStoredOverviewPose()
    {
        if (!hasStoredOverviewPose || overviewCamera == null)
        {
            return;
        }

        Transform cameraTransform = overviewCamera.transform;
        cameraTransform.position = storedOverviewPosition;
        cameraTransform.rotation = storedOverviewRotation;
        overviewCamera.orthographic = storedOverviewOrthographic;
        overviewCamera.orthographicSize = storedOverviewOrthographicSize;
        overviewCamera.fieldOfView = storedOverviewFieldOfView;
        hasStoredOverviewPose = false;
    }

    private void ApplyPathProjectionMode()
    {
        if (DroneManager.Instance == null)
        {
            return;
        }

        float projectionHeight = DroneManager.Instance.CalculatePathProjectionHeight();
        DroneManager.Instance.SetPathProjectionMode(isTopDown2D, projectionHeight);
    }

    private float NormalizePitch(float pitch)
    {
        if (pitch > 180f)
        {
            pitch -= 360f;
        }

        return pitch;
    }
}
