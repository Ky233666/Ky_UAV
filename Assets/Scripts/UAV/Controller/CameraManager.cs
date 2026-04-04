using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the overview and follow cameras.
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

    [Header("Follow Settings")]
    public Transform targetDrone;
    public Vector3 followOffset = new Vector3(0, 5, -10);
    public Vector3 lookAtOffset = new Vector3(0, 1.5f, 0);
    public float followSmoothTime = 6f;

    [Header("Current Mode")]
    public bool isOverview = true;

    private readonly List<DroneController> managedDrones = new List<DroneController>();
    private int currentFollowIndex = -1;
    private float overviewYaw;
    private float overviewPitch;

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

        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.E))
        {
            FocusNextDrone();
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            FocusPreviousDrone();
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
        if (overviewCamera != null)
        {
            overviewCamera.enabled = isOverview;
            AudioListener overviewListener = overviewCamera.GetComponent<AudioListener>();
            if (overviewListener != null)
            {
                overviewListener.enabled = isOverview;
            }
        }

        if (followCamera != null)
        {
            followCamera.enabled = !isOverview;
            AudioListener followListener = followCamera.GetComponent<AudioListener>();
            if (followListener != null)
            {
                followListener.enabled = !isOverview;
            }
        }
    }

    public Camera GetActiveCamera()
    {
        if (!isOverview && followCamera != null)
        {
            return followCamera;
        }

        if (overviewCamera != null)
        {
            return overviewCamera;
        }

        return Camera.main;
    }

    public void SwitchToOverview()
    {
        isOverview = true;
        UpdateCameraState();
    }

    public void SwitchToFollow()
    {
        isOverview = false;
        EnsureFollowTarget();
        UpdateCameraState();
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
            return;
        }

        if (targetDrone == null)
        {
            SetFollowTarget(0);
            return;
        }

        for (int i = 0; i < managedDrones.Count; i++)
        {
            if (managedDrones[i] != null && managedDrones[i].transform == targetDrone)
            {
                currentFollowIndex = i;
                return;
            }
        }

        SetFollowTarget(0);
    }

    public void FocusNextDrone()
    {
        if (isOverview)
        {
            isOverview = false;
        }

        EnsureFollowTarget();
        if (managedDrones.Count == 0)
        {
            return;
        }

        int nextIndex = currentFollowIndex < 0 ? 0 : (currentFollowIndex + 1) % managedDrones.Count;
        SetFollowTarget(nextIndex);
        UpdateCameraState();
    }

    public void FocusPreviousDrone()
    {
        if (isOverview)
        {
            isOverview = false;
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
    }

    public void SetFollowOffset(Vector3 offset)
    {
        followOffset = offset;

        if (!isOverview)
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
        if (Mathf.Abs(scroll) > 0.001f)
        {
            cameraTransform.position += cameraTransform.forward * (scroll * overviewZoomSpeed * Time.deltaTime);
        }

        bool allowRotation = !requireRightMouseForOverviewRotation || Input.GetMouseButton(1);
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

    private float NormalizePitch(float pitch)
    {
        if (pitch > 180f)
        {
            pitch -= 360f;
        }

        return pitch;
    }
}
