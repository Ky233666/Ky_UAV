using UnityEngine;

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
    
    [Header("当前模式")]
    public bool isOverview = true;
    
    void Start()
    {
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
            UpdateCameraState();
        }
        
        // 跟随模式下，更新相机位置
        if (!isOverview && targetDrone != null)
        {
            followCamera.transform.position = targetDrone.position + followOffset;
            followCamera.transform.LookAt(targetDrone);
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
}
