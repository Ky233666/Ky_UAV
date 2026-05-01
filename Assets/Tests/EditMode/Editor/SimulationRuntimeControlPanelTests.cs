using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class SimulationRuntimeControlPanelTests
{
    private const BindingFlags PrivateInstanceFlags =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
    private readonly List<string> createdDirectories = new List<string>();

    [TearDown]
    public void TearDown()
    {
        foreach (UnityEngine.Object createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();

        foreach (string directory in createdDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        createdDirectories.Clear();

        if (SimulationContext.Current != null)
        {
            UnityEngine.Object.DestroyImmediate(SimulationContext.Current.gameObject);
        }
    }

    [Test]
    public void ChangeExperimentPreset_WrapsWithinSelectedGroup()
    {
        SimulationRuntimeControlPanel panel = CreatePanel();
        ExperimentPreset firstPreset = CreatePreset("Planning A", "planning");
        ExperimentPreset secondPreset = CreatePreset("Planning B", "planning");

        List<ExperimentPresetGroupInfo> groups =
            GetField<List<ExperimentPresetGroupInfo>>(panel, "experimentPresetGroups");
        groups.Clear();
        groups.Add(new ExperimentPresetGroupInfo(
            "planning",
            "Planning",
            new List<ExperimentPreset> { firstPreset, secondPreset }));

        SetField(panel, "selectedExperimentGroupIndex", 0);
        SetField(panel, "selectedExperimentPresetIndex", 0);

        Invoke(panel, "ChangeExperimentPreset", 1);
        ExperimentPreset selectedPreset = Invoke<ExperimentPreset>(panel, "GetSelectedExperimentPreset");

        Assert.AreSame(secondPreset, selectedPreset);

        Invoke(panel, "ChangeExperimentPreset", 1);
        selectedPreset = Invoke<ExperimentPreset>(panel, "GetSelectedExperimentPreset");

        Assert.AreSame(firstPreset, selectedPreset);
    }

    [Test]
    public void ApplyCustomExportDirectory_UpdatesExporterAndStatusText()
    {
        SimulationRuntimeControlPanel panel = CreatePanel();
        SimulationResultExporter exporter = CreateGameObject("Exporter").AddComponent<SimulationResultExporter>();
        TMP_InputField inputField = CreateGameObject("ExportInput", typeof(RectTransform)).AddComponent<TMP_InputField>();
        TextMeshProUGUI statusText = CreateGameObject("ExportStatus", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        string exportDirectory = CreateTempDirectory();

        panel.resultExporter = exporter;
        inputField.text = exportDirectory;
        SetField(panel, "exportDirectoryInputField", inputField);
        SetField(panel, "exportDirectoryStatusText", statusText);

        Invoke(panel, "ApplyCustomExportDirectory");

        Assert.IsTrue(exporter.IsUsingCustomExportDirectory);
        Assert.AreEqual(Path.GetFullPath(exportDirectory), exporter.customExportDirectory);
        StringAssert.Contains("当前: 自定义目录", statusText.text);
        StringAssert.Contains(Path.GetFullPath(exportDirectory), statusText.text);
    }

    [Test]
    public void DroneCountStepper_PreservesPendingCountAcrossSystemRefresh()
    {
        SimulationRuntimeControlPanel panel = CreatePanel();
        DroneManager droneManager = CreateGameObject("DroneManager").AddComponent<DroneManager>();
        droneManager.droneCount = 4;
        panel.droneManager = droneManager;
        SetField(panel, "configuredDroneCount", 4);

        Invoke(panel, "OnDecreaseDroneCountClicked");

        Assert.AreEqual(3, GetField<int>(panel, "configuredDroneCount"));
        Assert.IsTrue(GetField<bool>(panel, "hasPendingDroneCountChange"));

        Invoke(panel, "SyncFromSystems");

        Assert.AreEqual(3, GetField<int>(panel, "configuredDroneCount"));
        Assert.IsTrue(GetField<bool>(panel, "hasPendingDroneCountChange"));
    }

    [Test]
    public void RefreshBatchStatus_RendersIdleProgressAndLastMessage()
    {
        SimulationRuntimeControlPanel panel = CreatePanel();
        BatchExperimentRunner runner = CreateGameObject("BatchRunner").AddComponent<BatchExperimentRunner>();
        TextMeshProUGUI statusText = CreateGameObject("BatchStatus", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();

        runner.SetBatchRunCount(7);
        SetPropertyBackingField(runner, "LastBatchMessage", "等待下一轮");

        panel.batchExperimentRunner = runner;
        SetField(panel, "batchStatusText", statusText);

        Invoke(panel, "RefreshBatchStatus");

        StringAssert.Contains("状态: 空闲", statusText.text);
        StringAssert.Contains("预设: Current Runtime", statusText.text);
        StringAssert.Contains("进度: 0/7", statusText.text);
        StringAssert.Contains("等待下一轮", statusText.text);
    }

    private SimulationRuntimeControlPanel CreatePanel()
    {
        return CreateGameObject("RuntimePanel").AddComponent<SimulationRuntimeControlPanel>();
    }

    private GameObject CreateGameObject(string name, params Type[] componentTypes)
    {
        GameObject gameObject = componentTypes.Length > 0
            ? new GameObject(name, componentTypes)
            : new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private ExperimentPreset CreatePreset(string presetName, string groupName)
    {
        ExperimentPreset preset = ScriptableObject.CreateInstance<ExperimentPreset>();
        preset.presetName = presetName;
        preset.groupName = groupName;
        createdObjects.Add(preset);
        return preset;
    }

    private string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "KyUAVTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        createdDirectories.Add(directory);
        return directory;
    }

    private static void Invoke(object target, string methodName, params object[] arguments)
    {
        target.GetType()
            .GetMethod(methodName, PrivateInstanceFlags)
            .Invoke(target, arguments);
    }

    private static T Invoke<T>(object target, string methodName, params object[] arguments)
    {
        return (T)target.GetType()
            .GetMethod(methodName, PrivateInstanceFlags)
            .Invoke(target, arguments);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        return (T)target.GetType()
            .GetField(fieldName, PrivateInstanceFlags)
            .GetValue(target);
    }

    private static void SetField<T>(object target, string fieldName, T value)
    {
        target.GetType()
            .GetField(fieldName, PrivateInstanceFlags)
            .SetValue(target, value);
    }

    private static void SetPropertyBackingField<T>(object target, string propertyName, T value)
    {
        SetField(target, $"<{propertyName}>k__BackingField", value);
    }
}
