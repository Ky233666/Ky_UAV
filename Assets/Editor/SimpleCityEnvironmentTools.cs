using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SimpleCityEnvironmentTools
{
    private const string DaySkyboxPath = "Assets/SimpleCityPackage/Skybox/Day/Day_Sky.mat";

    [MenuItem("Tools/KY UAV/Apply Simple City Day Environment")]
    public static void ApplySimpleCityDayEnvironment()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogWarning("[SimpleCityEnvironmentTools] 当前没有可编辑场景。");
            return;
        }

        Material daySkybox = AssetDatabase.LoadAssetAtPath<Material>(DaySkyboxPath);
        if (daySkybox == null)
        {
            Debug.LogError($"[SimpleCityEnvironmentTools] 未找到天空盒材质: {DaySkyboxPath}");
            return;
        }

        RenderSettings.skybox = daySkybox;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.40916955f, 0.5030604f, 0.63235295f, 1f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.ambientSkyColor = Color.white;
        RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f, 1f);

        Light sun = FindDirectionalLight();
        if (sun != null)
        {
            RenderSettings.sun = sun;
            sun.color = Color.white;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(72.496f, -49.34f, 0f);
            EditorUtility.SetDirty(sun);
            EditorUtility.SetDirty(sun.transform);
        }

        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(activeScene);
        Debug.Log("[SimpleCityEnvironmentTools] 已应用 Simple City 白天环境。");
    }

    [MenuItem("Tools/KY UAV/Frame Overview Camera To City")]
    public static void FrameOverviewCameraToCity()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogWarning("[SimpleCityEnvironmentTools] 当前没有可编辑场景。");
            return;
        }

        GameObject cityRoot = GameObject.Find("CityEnvironment");
        Camera overviewCamera = FindCamera("OverviewCamera");
        if (cityRoot == null || overviewCamera == null)
        {
            Debug.LogWarning("[SimpleCityEnvironmentTools] 未找到 CityEnvironment 或 OverviewCamera。");
            return;
        }

        Renderer[] renderers = cityRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[SimpleCityEnvironmentTools] CityEnvironment 下没有可用于取景的 Renderer。");
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float maxHorizontalSize = Mathf.Max(bounds.size.x, bounds.size.z);
        Vector3 center = bounds.center;
        Vector3 offset = new Vector3(-maxHorizontalSize * 0.55f, maxHorizontalSize * 0.7f, -maxHorizontalSize * 0.6f);

        overviewCamera.transform.position = center + offset;
        overviewCamera.transform.rotation = Quaternion.Euler(42f, 38f, 0f);
        overviewCamera.fieldOfView = 50f;

        EditorUtility.SetDirty(overviewCamera);
        EditorUtility.SetDirty(overviewCamera.transform);
        EditorSceneManager.MarkSceneDirty(activeScene);

        Debug.Log("[SimpleCityEnvironmentTools] 已将总览镜头重新框住城市中心。");
    }

    private static Light FindDirectionalLight()
    {
        GameObject directionalLight = GameObject.Find("Directional Light");
        if (directionalLight == null)
        {
            return null;
        }

        return directionalLight.GetComponent<Light>();
    }

    private static Camera FindCamera(string name)
    {
        GameObject cameraObject = GameObject.Find(name);
        if (cameraObject == null)
        {
            return null;
        }

        return cameraObject.GetComponent<Camera>();
    }
}
