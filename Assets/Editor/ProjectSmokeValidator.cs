using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProjectSmokeValidator
{
    private const string MainScenePath = "Assets/Scenes/Main/MainScene.unity";

    [MenuItem("Tools/KY UAV/Run Project Smoke Validation")]
    public static void RunSmokeValidationMenu()
    {
        RunSmokeValidation();
    }

    public static void RunSmokeValidation()
    {
        StringBuilder log = new StringBuilder(512);
        bool success = true;

        if (!System.IO.File.Exists(MainScenePath))
        {
            throw new System.IO.FileNotFoundException("主场景不存在", MainScenePath);
        }

        EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        success &= ValidateObject<SimulationManager>("SimulationManager", log);
        success &= ValidateObject<DroneManager>("DroneManager", log);
        success &= ValidateObject<CameraManager>("CameraManager", log);
        success &= ValidateObject<TaskPointSpawner>("TaskPointSpawner", log);
        success &= ValidateObject<TaskPointImporter>("TaskPointImporter", log);
        success &= ValidateNamedObject("Buildings", log);
        success &= ValidateNamedObject("CityEnvironment", log);
        success &= ValidateNamedObject("Canvas", log);
        success &= ValidateNamedObject("OverviewCamera", log);
        success &= ValidateNamedObject("FollowCamera", log);

        SimulationManager simulationManager = Object.FindObjectOfType<SimulationManager>();
        if (simulationManager != null && simulationManager.droneManager == null)
        {
            success = false;
            log.AppendLine("SimulationManager 未连接 DroneManager。");
        }

        DroneManager droneManager = Object.FindObjectOfType<DroneManager>();
        if (droneManager != null && droneManager.dronePrefab == null)
        {
            success = false;
            log.AppendLine("DroneManager 未配置 dronePrefab。");
        }

        log.Insert(0, success ? "[SmokeValidation] PASS\n" : "[SmokeValidation] FAIL\n");
        Debug.Log(log.ToString());

        if (!success)
        {
            throw new BuildFailedException("KY UAV smoke validation failed. See log for details.");
        }
    }

    private static bool ValidateObject<T>(string label, StringBuilder log) where T : Object
    {
        T instance = Object.FindObjectOfType<T>();
        if (instance != null)
        {
            log.Append(label).Append(": OK").AppendLine();
            return true;
        }

        log.Append(label).Append(": MISSING").AppendLine();
        return false;
    }

    private static bool ValidateNamedObject(string objectName, StringBuilder log)
    {
        if (GameObject.Find(objectName) != null)
        {
            log.Append(objectName).Append(": OK").AppendLine();
            return true;
        }

        log.Append(objectName).Append(": MISSING").AppendLine();
        return false;
    }
}
