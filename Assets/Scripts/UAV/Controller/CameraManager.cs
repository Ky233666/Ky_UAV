using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 相机管理器：支持总览视角和跟随视角切换
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Header("相机引用")]
    public Camera overviewCamera;
    public Camera followCamera;
    
    [Header("跟随设置")]
    public Transform targetDrone;
    public Vector3 followOffset = new Vector3(0, 5, -10);
    public Vector3 lookAtOffset = new Vector3(0, 1.5f, 0);
    public float followSmoothTime = 6f;
    
    [Header("当前模式")]
    public bool isOverview = true;

    private readonly List<DroneController> managedDrones = new List<DroneController>();
    private int currentFollowIndex = -1;
    
    void Start()
    {
        RefreshManagedDrones();
        UpdateCameraState();
    }
    
    void Update()
    {
        // 按 1 键切换到总览视角
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            isOverview = true;
            UpdateCameraState();
        }
        
        // 按 2 键切换到跟随视角
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            isOverview = false;
            EnsureFollowTarget();
            UpdateCameraState();
        }

        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.E))
        {
            FocusNextDrone();
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            FocusPreviousDrone();
        }
        
        // 跟随模式下，更新相机位置
        if (!isOverview)
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
            var overviewListener = overviewCamera.GetComponent<AudioListener>();
            if (overviewListener != null)
                overviewListener.enabled = isOverview;
        }

        if (followCamera != null)
        {
            followCamera.enabled = !isOverview;
            var followListener = followCamera.GetComponent<AudioListener>();
            if (followListener != null)
                followListener.enabled = !isOverview;
        }
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
}
