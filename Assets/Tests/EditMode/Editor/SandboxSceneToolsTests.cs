using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SandboxSceneToolsTests
{
    private const string SandboxScenePath = "Assets/Scenes/Sandbox/CustomObstacleSandbox.unity";

    [Test]
    public void CreateOrRefreshCustomObstacleSandboxSceneBatch_CreatesCleanSandboxScene()
    {
        Type sandboxSceneToolsType = Type.GetType("KyUavSandboxSceneTools, Assembly-CSharp-Editor");
        Assert.IsNotNull(sandboxSceneToolsType, "未找到 KyUavSandboxSceneTools。");

        MethodInfo createMethod = sandboxSceneToolsType.GetMethod(
            "CreateOrRefreshCustomObstacleSandboxSceneBatch",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(createMethod, "未找到 CreateOrRefreshCustomObstacleSandboxSceneBatch 方法。");

        createMethod.Invoke(null, null);

        Assert.IsTrue(File.Exists(SandboxScenePath), "自定义障碍物实验场景未生成。");

        Scene sandboxScene = EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Single);
        Assert.IsTrue(sandboxScene.IsValid(), "生成后的 sandbox 场景无效。");

        GameObject obstacleRoot = GameObject.Find("Buildings");
        Assert.IsNotNull(obstacleRoot, "sandbox 场景缺少 Buildings 根节点。");
        Assert.AreEqual(0, obstacleRoot.transform.childCount, "sandbox 场景中的 Buildings 应保持为空。");

        GameObject groundObject =
            GameObject.Find("Plane001") ??
            GameObject.Find("Plane") ??
            GameObject.Find("Ground");
        Assert.IsNotNull(groundObject, "sandbox 场景缺少可用于拖拽创建障碍物的地面。");

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
        {
            Assert.AreEqual(groundLayer, groundObject.layer, "sandbox 地面未设置为 Ground 层。");
        }

        DroneManager droneManager = UnityEngine.Object.FindObjectOfType<DroneManager>();
        Assert.IsNotNull(droneManager, "sandbox 场景缺少 DroneManager。");
        Assert.AreEqual(obstacleRoot.transform, droneManager.obstacleRoot, "DroneManager 未回写 sandbox 的 Buildings 根节点。");
        Assert.IsTrue(droneManager.autoConfigurePlanningObstacles, "sandbox 场景应保持自动障碍配置开启。");
        Assert.AreEqual(PathPlannerType.AStar, droneManager.pathPlannerType, "sandbox 场景应默认使用 A* 避障规划。");
        Assert.AreEqual(new Vector3(-100f, 0f, -100f), droneManager.planningWorldMin);
        Assert.AreEqual(new Vector3(100f, 30f, 100f), droneManager.planningWorldMax);

        Assert.Zero(UnityEngine.Object.FindObjectsOfType<TaskPoint>().Length, "sandbox 场景应清空默认任务点。");
    }
}
