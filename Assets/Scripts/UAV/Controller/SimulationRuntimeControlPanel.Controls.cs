using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class SimulationRuntimeControlPanel
{
    private void OnPreviousSchedulerClicked()
    {
        schedulerIndex = WrapIndex(schedulerIndex - 1, schedulerOptions.Length);
        if (droneManager != null)
        {
            droneManager.schedulerAlgorithm = schedulerOptions[schedulerIndex];
        }

        transientMessage = $"调度切换为 {FormatSchedulerName(schedulerOptions[schedulerIndex])}";
        RefreshAllLabels();
    }

    private void OnNextSchedulerClicked()
    {
        schedulerIndex = WrapIndex(schedulerIndex + 1, schedulerOptions.Length);
        if (droneManager != null)
        {
            droneManager.schedulerAlgorithm = schedulerOptions[schedulerIndex];
        }

        transientMessage = $"调度切换为 {FormatSchedulerName(schedulerOptions[schedulerIndex])}";
        RefreshAllLabels();
    }

    private void OnPreviousPlannerClicked()
    {
        plannerIndex = WrapIndex(plannerIndex - 1, plannerOptions.Length);
        if (droneManager != null)
        {
            droneManager.pathPlannerType = plannerOptions[plannerIndex];
        }

        transientMessage = $"路径切换为 {FormatPlannerName(plannerOptions[plannerIndex])}";
        RefreshAllLabels();
    }

    private void OnNextPlannerClicked()
    {
        plannerIndex = WrapIndex(plannerIndex + 1, plannerOptions.Length);
        if (droneManager != null)
        {
            droneManager.pathPlannerType = plannerOptions[plannerIndex];
        }

        transientMessage = $"路径切换为 {FormatPlannerName(plannerOptions[plannerIndex])}";
        RefreshAllLabels();
    }

    private void OnDecreasePlanningGridClicked()
    {
        configuredPlanningGridCellSize = Mathf.Clamp(
            configuredPlanningGridCellSize - PlanningGridCellSizeStep,
            MinPlanningGridCellSize,
            MaxPlanningGridCellSize);
        ApplyPlanningSettings();
    }

    private void OnIncreasePlanningGridClicked()
    {
        configuredPlanningGridCellSize = Mathf.Clamp(
            configuredPlanningGridCellSize + PlanningGridCellSizeStep,
            MinPlanningGridCellSize,
            MaxPlanningGridCellSize);
        ApplyPlanningSettings();
    }

    private void OnDecreasePlanningMinXClicked()
    {
        configuredPlanningMinX -= PlanningBoundaryStep;
        ApplyPlanningSettings();
    }

    private void OnIncreasePlanningMinXClicked()
    {
        configuredPlanningMinX += PlanningBoundaryStep;
        ApplyPlanningSettings();
    }

    private void OnDecreasePlanningMaxXClicked()
    {
        configuredPlanningMaxX -= PlanningBoundaryStep;
        ApplyPlanningSettings();
    }

    private void OnIncreasePlanningMaxXClicked()
    {
        configuredPlanningMaxX += PlanningBoundaryStep;
        ApplyPlanningSettings();
    }

    private void OnDecreasePlanningMinZClicked()
    {
        configuredPlanningMinZ -= PlanningBoundaryStep;
        ApplyPlanningSettings();
    }

    private void OnIncreasePlanningMinZClicked()
    {
        configuredPlanningMinZ += PlanningBoundaryStep;
        ApplyPlanningSettings();
    }

    private void OnDecreasePlanningMaxZClicked()
    {
        configuredPlanningMaxZ -= PlanningBoundaryStep;
        ApplyPlanningSettings();
    }

    private void OnIncreasePlanningMaxZClicked()
    {
        configuredPlanningMaxZ += PlanningBoundaryStep;
        ApplyPlanningSettings();
    }

    private void OnDecreasePlanningMinYClicked()
    {
        configuredPlanningMinY -= PlanningHeightStep;
        ApplyPlanningSettings();
    }

    private void OnIncreasePlanningMinYClicked()
    {
        configuredPlanningMinY += PlanningHeightStep;
        ApplyPlanningSettings();
    }

    private void OnDecreasePlanningMaxYClicked()
    {
        configuredPlanningMaxY -= PlanningHeightStep;
        ApplyPlanningSettings();
    }

    private void OnIncreasePlanningMaxYClicked()
    {
        configuredPlanningMaxY += PlanningHeightStep;
        ApplyPlanningSettings();
    }

    private void ToggleDiagonalPlanning()
    {
        configuredAllowDiagonalPlanning = !configuredAllowDiagonalPlanning;
        ApplyPlanningSettings();
    }

    private void ToggleObstacleAutoConfiguration()
    {
        configuredAutoConfigureObstacles = !configuredAutoConfigureObstacles;
        ApplyPlanningSettings();
    }

    private void FitPlanningBoundsToScene()
    {
        if (droneManager == null)
        {
            transientMessage = "未找到 DroneManager，无法适配规划边界";
            RefreshAllLabels();
            return;
        }

        bool changed = droneManager.FitPlanningBoundsToCurrentScene();
        configuredPlanningMinX = droneManager.planningWorldMin.x;
        configuredPlanningMaxX = droneManager.planningWorldMax.x;
        configuredPlanningMinZ = droneManager.planningWorldMin.z;
        configuredPlanningMaxZ = droneManager.planningWorldMax.z;
        configuredPlanningMinY = droneManager.planningWorldMin.y;
        configuredPlanningMaxY = droneManager.planningWorldMax.y;

        if (EnsurePlanningMapVisualizer())
        {
            planningMapVisualizer.SetBoundsVisible(true);
            planningMapVisualizer.ForceRefresh();
        }

        transientMessage = changed
            ? $"规划边界已适配当前无人机、任务点和相关障碍物：X[{configuredPlanningMinX:0},{configuredPlanningMaxX:0}] Z[{configuredPlanningMinZ:0},{configuredPlanningMaxZ:0}]"
            : "当前规划边界已经覆盖无人机、任务点和相关障碍物";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void TogglePlanningBoundsPreview()
    {
        if (!EnsurePlanningMapVisualizer())
        {
            transientMessage = "未找到规划地图可视化组件";
            RefreshAllLabels();
            return;
        }

        bool nextVisible = !planningMapVisualizer.showBounds;
        planningMapVisualizer.SetBoundsVisible(nextVisible);
        transientMessage = nextVisible ? "规划边界显示已开启" : "规划边界显示已关闭";
        RefreshAllLabels();
    }

    private void TogglePlanningBlockedCellPreview()
    {
        if (!EnsurePlanningMapVisualizer())
        {
            transientMessage = "未找到规划地图可视化组件";
            RefreshAllLabels();
            return;
        }

        bool nextVisible = !planningMapVisualizer.showBlockedCells;
        planningMapVisualizer.SetBlockedCellsVisible(nextVisible);
        transientMessage = nextVisible
            ? planningMapVisualizer.LastPreviewMessage
            : "规划障碍格显示已关闭";
        RefreshAllLabels();
    }

    private bool EnsurePlanningMapVisualizer()
    {
        if (planningMapVisualizer == null)
        {
            planningMapVisualizer = RuntimeSceneRegistry.Get<PlanningMapVisualizer>(this);
        }

        if (planningMapVisualizer == null && simulationManager != null)
        {
            planningMapVisualizer = simulationManager.GetComponent<PlanningMapVisualizer>();
            if (planningMapVisualizer == null)
            {
                planningMapVisualizer = simulationManager.gameObject.AddComponent<PlanningMapVisualizer>();
            }

            simulationManager.planningMapVisualizer = planningMapVisualizer;
            RuntimeSceneRegistry.Register(planningMapVisualizer);
        }

        if (planningMapVisualizer == null)
        {
            return false;
        }

        if (planningMapVisualizer.droneManager == null)
        {
            planningMapVisualizer.droneManager = droneManager;
        }

        return true;
    }

    private void LoadRLTrainingScene()
    {
        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            simulationManager.OnResetClicked();
        }

        transientMessage = "Loading RL training scene";
        RefreshAllLabels();
        if (Application.isPlaying)
        {
            RLTrainingSceneBootstrap.LoadRuntimeTrainingScene(simulationManager, droneManager, cameraManager);
            return;
        }

        SceneManager.LoadScene(RLTrainingSceneBootstrap.SceneName, LoadSceneMode.Single);
    }

    private void ApplyRLTrainingMapPreset()
    {
        configuredPlanningGridCellSize = 2f;
        configuredPlanningMinX = -30f;
        configuredPlanningMaxX = 30f;
        configuredPlanningMinZ = -30f;
        configuredPlanningMaxZ = 30f;
        configuredPlanningMinY = 0f;
        configuredPlanningMaxY = 12f;
        configuredAllowDiagonalPlanning = false;
        configuredAutoConfigureObstacles = true;
        ApplyPlanningSettings();
        transientMessage = "已应用 RL 小地图: 约 31x31 网格，可在该范围内绘制障碍并导出";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ExportRLMapForFirstTask()
    {
        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            transientMessage = "请先停止或重置仿真，再导出 RL 地图";
            RefreshAllLabels();
            return;
        }

        if (rlMapExporter == null)
        {
            rlMapExporter = RuntimeSceneRegistry.Get<RLMapExporter>(this);
        }

        if (rlMapExporter == null)
        {
            transientMessage = "未找到 RL 地图导出器";
            RefreshAllLabels();
            return;
        }

        if (droneManager != null)
        {
            droneManager.ApplyPlanningSettings(
                configuredPlanningGridCellSize,
                configuredAllowDiagonalPlanning,
                configuredAutoConfigureObstacles,
                BuildPlanningWorldMin(),
                BuildPlanningWorldMax());
        }

        rlMapExporter.ExportFirstDroneFirstTaskMap();
        RefreshRLCaseCatalog();
        SelectRLCaseByName(rlMapExporter.LastExportCaseName);
        ShowSelectedRLMapObstaclePreview();
        transientMessage = rlMapExporter.LastExportMessage;
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ValidateRLPathResult()
    {
        if (rlPathResultImporter == null)
        {
            rlPathResultImporter = RuntimeSceneRegistry.Get<RLPathResultImporter>(this);
        }

        if (rlPathResultImporter == null)
        {
            transientMessage = "未找到 RL 路径读取器";
            RefreshAllLabels();
            return;
        }

        bool imported = rlPathResultImporter.TryImportPath(GetSelectedRLCasePathPath(), out PathPlanningResult result);
        transientMessage = imported
            ? $"RL路径可读取: {result.waypoints.Count} 个路径点"
            : rlPathResultImporter.LastImportMessage;
        RefreshAllLabels();
    }

    private void TrainSelectedRLCase()
    {
        StartRLTrainingForSelectedCase(false);
    }

    private void TrainSelectedRLCaseAndImport()
    {
        StartRLTrainingForSelectedCase(true);
    }

    private void StartRLTrainingForSelectedCase(bool importWhenDone)
    {
        CacheReferences();
        string caseName = GetSelectedRLCaseName();
        if (string.IsNullOrWhiteSpace(caseName))
        {
            transientMessage = "没有可训练的 RL 案例，请先导出地图";
            RefreshAllLabels();
            return;
        }

        if (!EnsureRLTrainingRunner())
        {
            transientMessage = "未找到 RL 训练执行器";
            RefreshAllLabels();
            return;
        }

        RLPathPlanningFileUtility.SetSelectedCaseName(caseName);
        bool started = rlTrainingRunner.TryStartTraining(caseName, (success, message) =>
        {
            transientMessage = message;
            if (success && importWhenDone)
            {
                ImportAndShowRLPathForFirstTask();
                transientMessage = $"{message}; 已自动导入显示";
            }

            RefreshRLCaseCatalog();
            RefreshAllLabels();
            RefreshSummary();
        });

        transientMessage = started
            ? $"RL 训练已启动: {caseName}"
            : rlTrainingRunner.LastTrainingMessage;
        RefreshAllLabels();
    }

    private void ImportAndShowRLPathForFirstTask()
    {
        CacheReferences();

        if (droneManager == null || droneManager.drones == null || droneManager.drones.Count == 0)
        {
            transientMessage = "没有可用于显示 RL 路径的无人机";
            RefreshAllLabels();
            return;
        }

        TaskPoint firstTask = GetFirstTaskPoint();
        if (firstTask == null)
        {
            transientMessage = "没有可用于显示 RL 路径的任务点";
            RefreshAllLabels();
            return;
        }

        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            simulationManager.OnResetClicked();
        }

        droneManager.pathPlannerType = PathPlannerType.QLearningOffline;
        string selectedCaseName = GetSelectedRLCaseName();
        if (!string.IsNullOrWhiteSpace(selectedCaseName))
        {
            RLPathPlanningFileUtility.SetSelectedCaseName(selectedCaseName);
        }

        plannerIndex = Array.IndexOf(plannerOptions, PathPlannerType.QLearningOffline);
        if (plannerIndex < 0)
        {
            plannerIndex = 0;
        }

        DroneController firstDrone = droneManager.drones[0];
        PathPlanningResult result = droneManager.PlanPathForTask(
            firstDrone.droneId,
            firstTask,
            PathPlannerType.QLearningOffline);

        string obstaclePreviewMessage = ShowSelectedRLMapObstaclePreview();

        if (result != null && result.HasPath())
        {
            showPlannedPath = true;
            droneManager.ApplyPathVisibilityToAll(true, showTrail);
            transientMessage =
                $"已导入并显示 RL 路径: {result.waypoints.Count} 点 / {result.totalCost:0.0}m；{obstaclePreviewMessage}";
        }
        else
        {
            transientMessage = result != null && !string.IsNullOrWhiteSpace(result.message)
                ? result.message
                : "RL 路径导入失败，请确认 Python 已生成当前案例目录的 path.json";
        }

        RefreshAllLabels();
        RefreshSummary();
    }

    private void StartSimulationFromPanel()
    {
        CacheReferences();
        if (simulationManager == null)
        {
            transientMessage = "未找到 SimulationManager，无法开始仿真";
            RefreshAllLabels();
            return;
        }

        simulationManager.OnStartClicked();
        transientMessage = simulationManager.currentState == SimulationState.Running
            ? "仿真已开始"
            : "仿真未开始，请检查任务点、无人机和路径结果";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void PauseSimulationFromPanel()
    {
        CacheReferences();
        if (simulationManager == null)
        {
            transientMessage = "未找到 SimulationManager，无法暂停仿真";
            RefreshAllLabels();
            return;
        }

        simulationManager.OnPauseClicked();
        transientMessage = "仿真已暂停";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ResetSimulationFromPanel()
    {
        CacheReferences();
        if (simulationManager == null)
        {
            transientMessage = "未找到 SimulationManager，无法重置仿真";
            RefreshAllLabels();
            return;
        }

        simulationManager.OnResetClicked();
        transientMessage = "仿真已重置";
        RefreshAllLabels();
        RefreshSummary();
    }

    private TaskPoint GetFirstTaskPoint()
    {
        TaskPoint[] tasks = SimulationContext.GetOrCreate(this).GetTaskPoints();
        if (tasks == null)
        {
            return null;
        }

        for (int i = 0; i < tasks.Length; i++)
        {
            if (tasks[i] != null)
            {
                return tasks[i];
            }
        }

        return null;
    }

    private bool EnsureRLTrainingRunner()
    {
        if (rlTrainingRunner == null)
        {
            rlTrainingRunner = RuntimeSceneRegistry.Get<RLQlearningTrainingRunner>(this);
        }

        if (rlTrainingRunner == null && simulationManager != null)
        {
            rlTrainingRunner = simulationManager.GetComponent<RLQlearningTrainingRunner>();
            if (rlTrainingRunner == null)
            {
                rlTrainingRunner = simulationManager.gameObject.AddComponent<RLQlearningTrainingRunner>();
            }

            simulationManager.rlTrainingRunner = rlTrainingRunner;
            RuntimeSceneRegistry.Register(rlTrainingRunner);
        }

        return rlTrainingRunner != null;
    }

    private void OnPreviousRLCaseClicked()
    {
        RefreshRLCaseCatalog();
        if (rlCaseNames.Count == 0)
        {
            transientMessage = "没有可选择的 RL 案例，请先导出地图";
            RefreshAllLabels();
            return;
        }

        selectedRLCaseIndex = WrapIndex(selectedRLCaseIndex - 1, rlCaseNames.Count);
        RLPathPlanningFileUtility.SetSelectedCaseName(rlCaseNames[selectedRLCaseIndex]);
        transientMessage = $"RL案例切换为 {rlCaseNames[selectedRLCaseIndex]}";
        RefreshAllLabels();
    }

    private void OnNextRLCaseClicked()
    {
        RefreshRLCaseCatalog();
        if (rlCaseNames.Count == 0)
        {
            transientMessage = "没有可选择的 RL 案例，请先导出地图";
            RefreshAllLabels();
            return;
        }

        selectedRLCaseIndex = WrapIndex(selectedRLCaseIndex + 1, rlCaseNames.Count);
        RLPathPlanningFileUtility.SetSelectedCaseName(rlCaseNames[selectedRLCaseIndex]);
        transientMessage = $"RL案例切换为 {rlCaseNames[selectedRLCaseIndex]}";
        RefreshAllLabels();
    }

    private void RefreshRLCaseCatalog()
    {
        string previousCase = GetSelectedRLCaseNameWithoutRefresh();
        rlCaseNames.Clear();
        rlCaseNames.AddRange(RLPathPlanningFileUtility.GetCaseNames());

        if (rlCaseNames.Count == 0)
        {
            selectedRLCaseIndex = 0;
            return;
        }

        int selectedIndex = -1;
        if (!string.IsNullOrWhiteSpace(previousCase))
        {
            selectedIndex = rlCaseNames.IndexOf(previousCase);
        }

        if (selectedIndex < 0 && RLPathPlanningFileUtility.TryGetSelectedCaseName(out string utilitySelectedCase))
        {
            selectedIndex = rlCaseNames.IndexOf(utilitySelectedCase);
        }

        selectedRLCaseIndex = selectedIndex >= 0
            ? selectedIndex
            : Mathf.Clamp(selectedRLCaseIndex, 0, rlCaseNames.Count - 1);
        RLPathPlanningFileUtility.SetSelectedCaseName(rlCaseNames[selectedRLCaseIndex]);
    }

    private void SelectRLCaseByName(string caseName)
    {
        if (string.IsNullOrWhiteSpace(caseName))
        {
            return;
        }

        for (int i = 0; i < rlCaseNames.Count; i++)
        {
            if (string.Equals(rlCaseNames[i], caseName, StringComparison.OrdinalIgnoreCase))
            {
                selectedRLCaseIndex = i;
                RLPathPlanningFileUtility.SetSelectedCaseName(rlCaseNames[i]);
                return;
            }
        }
    }

    private string GetSelectedRLCaseName()
    {
        RefreshRLCaseCatalog();
        return GetSelectedRLCaseNameWithoutRefresh();
    }

    private string GetSelectedRLCaseNameWithoutRefresh()
    {
        if (rlCaseNames.Count == 0)
        {
            return "";
        }

        selectedRLCaseIndex = Mathf.Clamp(selectedRLCaseIndex, 0, rlCaseNames.Count - 1);
        return rlCaseNames[selectedRLCaseIndex];
    }

    private string GetSelectedRLCaseMapPath()
    {
        string caseName = GetSelectedRLCaseName();
        return string.IsNullOrWhiteSpace(caseName)
            ? RLPathPlanningFileUtility.GetDefaultMapPath()
            : RLPathPlanningFileUtility.GetCaseMapPath(caseName);
    }

    private string GetSelectedRLCasePathPath()
    {
        string caseName = GetSelectedRLCaseName();
        return string.IsNullOrWhiteSpace(caseName)
            ? RLPathPlanningFileUtility.GetDefaultPathPath()
            : RLPathPlanningFileUtility.GetCasePathPath(caseName);
    }

    private string ShowSelectedRLMapObstaclePreview()
    {
        if (rlMapObstacleVisualizer == null)
        {
            rlMapObstacleVisualizer = RuntimeSceneRegistry.Get<RLMapObstacleVisualizer>(this);
        }

        if (rlMapObstacleVisualizer == null && simulationManager != null)
        {
            rlMapObstacleVisualizer = simulationManager.GetComponent<RLMapObstacleVisualizer>();
            if (rlMapObstacleVisualizer == null)
            {
                rlMapObstacleVisualizer = simulationManager.gameObject.AddComponent<RLMapObstacleVisualizer>();
            }

            RuntimeSceneRegistry.Register(rlMapObstacleVisualizer);
        }

        if (rlMapObstacleVisualizer == null)
        {
            return "未找到障碍物预览组件";
        }

        if (rlMapObstacleVisualizer.droneManager == null)
        {
            rlMapObstacleVisualizer.droneManager = droneManager;
        }

        bool loaded = rlMapObstacleVisualizer.ShowMapObstacles(GetSelectedRLCaseMapPath());
        return loaded
            ? rlMapObstacleVisualizer.LastPreviewMessage
            : rlMapObstacleVisualizer.LastPreviewMessage;
    }

    private void ToggleTaskQueueVisualization()
    {
        if (taskQueueVisualizer == null)
        {
            taskQueueVisualizer = RuntimeSceneRegistry.Get<TaskQueueVisualizer>(this);
        }

        if (taskQueueVisualizer == null)
        {
            transientMessage = "未找到任务队列可视化组件";
            RefreshAllLabels();
            return;
        }

        bool visible = !taskQueueVisualizer.ShowTaskQueues;
        taskQueueVisualizer.SetVisible(visible);
        transientMessage = visible ? "已开启任务队列可视化" : "已关闭任务队列可视化";
        RefreshAllLabels();
    }

    private void RecordEvaluationSnapshot()
    {
        AlgorithmEvaluationSnapshot snapshot = CaptureEvaluationSnapshot();
        evaluationHistory.Insert(0, snapshot);
        while (evaluationHistory.Count > MaxEvaluationHistoryCount)
        {
            evaluationHistory.RemoveAt(evaluationHistory.Count - 1);
        }

        transientMessage = "Recorded current algorithm evaluation";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ClearEvaluationHistory()
    {
        evaluationHistory.Clear();
        transientMessage = "Cleared algorithm evaluation records";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void OnPreviousVisualizationDroneClicked()
    {
        if (algorithmVisualizerManager == null)
        {
            transientMessage = "未找到算法可视化管理器";
            RefreshAllLabels();
            return;
        }

        algorithmVisualizerManager.SelectPreviousDrone();
        transientMessage = $"切换到 {algorithmVisualizerManager.GetSelectedDroneLabel()} 的规划轨迹";
        RefreshAllLabels();
    }

    private void OnNextVisualizationDroneClicked()
    {
        if (algorithmVisualizerManager == null)
        {
            transientMessage = "未找到算法可视化管理器";
            RefreshAllLabels();
            return;
        }

        algorithmVisualizerManager.SelectNextDrone();
        transientMessage = $"切换到 {algorithmVisualizerManager.GetSelectedDroneLabel()} 的规划轨迹";
        RefreshAllLabels();
    }

    private void OnPreviousVisualizationModeClicked()
    {
        if (algorithmVisualizerManager == null)
        {
            transientMessage = "未找到算法可视化管理器";
            RefreshAllLabels();
            return;
        }

        algorithmVisualizerManager.SelectPreviousMode();
        transientMessage = $"演示模式切换为 {algorithmVisualizerManager.GetModeDisplayName()}";
        RefreshAllLabels();
    }

    private void OnNextVisualizationModeClicked()
    {
        if (algorithmVisualizerManager == null)
        {
            transientMessage = "未找到算法可视化管理器";
            RefreshAllLabels();
            return;
        }

        algorithmVisualizerManager.SelectNextMode();
        transientMessage = $"演示模式切换为 {algorithmVisualizerManager.GetModeDisplayName()}";
        RefreshAllLabels();
    }

    private void OnDecreaseVisualizationSpeedClicked()
    {
        if (algorithmVisualizerManager == null)
        {
            transientMessage = "未找到算法可视化管理器";
            RefreshAllLabels();
            return;
        }

        algorithmVisualizerManager.SelectPreviousSpeed();
        transientMessage = $"演示速度调整为 {algorithmVisualizerManager.GetPlaybackSpeedLabel()}";
        RefreshAllLabels();
    }

    private void OnIncreaseVisualizationSpeedClicked()
    {
        if (algorithmVisualizerManager == null)
        {
            transientMessage = "未找到算法可视化管理器";
            RefreshAllLabels();
            return;
        }

        algorithmVisualizerManager.SelectNextSpeed();
        transientMessage = $"演示速度调整为 {algorithmVisualizerManager.GetPlaybackSpeedLabel()}";
        RefreshAllLabels();
    }

    private void PlayVisualization()
    {
        if (algorithmVisualizerManager == null)
        {
            transientMessage = "未找到算法可视化管理器";
            RefreshAllLabels();
            return;
        }

        algorithmVisualizerManager.Resume();
        transientMessage = "已开始/继续播放算法过程";
        RefreshAllLabels();
    }

    private void PauseVisualization()
    {
        if (algorithmVisualizerManager == null)
        {
            transientMessage = "未找到算法可视化管理器";
            RefreshAllLabels();
            return;
        }

        algorithmVisualizerManager.Pause();
        transientMessage = "已暂停算法过程播放";
        RefreshAllLabels();
    }

    private void StepVisualization()
    {
        if (algorithmVisualizerManager == null)
        {
            transientMessage = "未找到算法可视化管理器";
            RefreshAllLabels();
            return;
        }

        algorithmVisualizerManager.StepForward();
        transientMessage = "已单步推进算法过程";
        RefreshAllLabels();
    }

    private void ResetVisualizationPlayback()
    {
        if (algorithmVisualizerManager == null)
        {
            transientMessage = "未找到算法可视化管理器";
            RefreshAllLabels();
            return;
        }

        algorithmVisualizerManager.ResetPlayback();
        transientMessage = "已重置算法过程演示";
        RefreshAllLabels();
    }

    private void ToggleVisualizationObstacleTransparency()
    {
        if (algorithmVisualizerManager == null)
        {
            transientMessage = "未找到算法可视化管理器";
            RefreshAllLabels();
            return;
        }

        bool enabled = !algorithmVisualizerManager.ObstacleTransparencyEnabled;
        algorithmVisualizerManager.SetObstacleTransparencyEnabled(enabled);
        transientMessage = enabled
            ? "算法演示已开启建筑半透明"
            : "算法演示已关闭建筑半透明";
        RefreshAllLabels();
    }

    private void OnDecreaseDroneCountClicked()
    {
        configuredDroneCount = Mathf.Clamp(configuredDroneCount - 1, MinDroneCount, MaxDroneCount);
        hasPendingDroneCountChange = droneManager == null || configuredDroneCount != Mathf.Clamp(droneManager.droneCount, MinDroneCount, MaxDroneCount);
        transientMessage = "点重建后按当前数量重新生成机群";
        RefreshAllLabels();
    }

    private void OnIncreaseDroneCountClicked()
    {
        configuredDroneCount = Mathf.Clamp(configuredDroneCount + 1, MinDroneCount, MaxDroneCount);
        hasPendingDroneCountChange = droneManager == null || configuredDroneCount != Mathf.Clamp(droneManager.droneCount, MinDroneCount, MaxDroneCount);
        transientMessage = "点重建后按当前数量重新生成机群";
        RefreshAllLabels();
    }

    private void OnDecreaseDroneSpeedClicked()
    {
        configuredDroneSpeed = Mathf.Clamp(configuredDroneSpeed - DroneSpeedStep, MinDroneSpeed, MaxDroneSpeed);
        ApplySpeedSettings();
    }

    private void OnIncreaseDroneSpeedClicked()
    {
        configuredDroneSpeed = Mathf.Clamp(configuredDroneSpeed + DroneSpeedStep, MinDroneSpeed, MaxDroneSpeed);
        ApplySpeedSettings();
    }

    private void OnDecreaseTimeScaleClicked()
    {
        configuredTimeScale = Mathf.Clamp(configuredTimeScale - TimeScaleStep, MinTimeScale, MaxTimeScale);
        ApplyTimeScaleSettings();
    }

    private void OnIncreaseTimeScaleClicked()
    {
        configuredTimeScale = Mathf.Clamp(configuredTimeScale + TimeScaleStep, MinTimeScale, MaxTimeScale);
        ApplyTimeScaleSettings();
    }

    private void ApplySpeedSettings()
    {
        if (droneManager != null)
        {
            droneManager.ApplyDroneSpeedToAll(configuredDroneSpeed);
        }

        transientMessage = $"机群速度 {configuredDroneSpeed:0.0}m/s";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ApplyPlanningSettings()
    {
        NormalizePlanningBounds();

        if (droneManager != null)
        {
            droneManager.ApplyPlanningSettings(
                configuredPlanningGridCellSize,
                configuredAllowDiagonalPlanning,
                configuredAutoConfigureObstacles,
                BuildPlanningWorldMin(),
                BuildPlanningWorldMax());
        }

        transientMessage =
            $"规划 网格{configuredPlanningGridCellSize:0.0}m 边界X[{configuredPlanningMinX:0},{configuredPlanningMaxX:0}] Z[{configuredPlanningMinZ:0},{configuredPlanningMaxZ:0}] 检测Y[{configuredPlanningMinY:0},{configuredPlanningMaxY:0}]";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ApplyTimeScaleSettings()
    {
        Time.timeScale = configuredTimeScale;
        transientMessage = $"仿真倍速 {configuredTimeScale:0.00}x";
        RefreshAllLabels();
    }

    private void OnDecreaseFollowHeightClicked()
    {
        configuredFollowHeight = Mathf.Clamp(configuredFollowHeight - FollowHeightStep, MinFollowHeight, MaxFollowHeight);
        ApplyCameraFollowSettings();
    }

    private void OnIncreaseFollowHeightClicked()
    {
        configuredFollowHeight = Mathf.Clamp(configuredFollowHeight + FollowHeightStep, MinFollowHeight, MaxFollowHeight);
        ApplyCameraFollowSettings();
    }

    private void OnDecreaseFollowDistanceClicked()
    {
        configuredFollowDistance = Mathf.Clamp(configuredFollowDistance - FollowDistanceStep, MinFollowDistance, MaxFollowDistance);
        ApplyCameraFollowSettings();
    }

    private void OnIncreaseFollowDistanceClicked()
    {
        configuredFollowDistance = Mathf.Clamp(configuredFollowDistance + FollowDistanceStep, MinFollowDistance, MaxFollowDistance);
        ApplyCameraFollowSettings();
    }

    private void ApplyCameraFollowSettings()
    {
        if (cameraManager != null)
        {
            cameraManager.SetFollowOffset(new Vector3(
                cameraManager.followOffset.x,
                configuredFollowHeight,
                -configuredFollowDistance));
        }

        transientMessage = $"跟随镜头 高度{configuredFollowHeight:0.0}m 距离{configuredFollowDistance:0.0}m";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void TogglePlannedPath()
    {
        showPlannedPath = !showPlannedPath;
        ApplyPathVisibilitySettings();
    }

    private void ToggleTrailPath()
    {
        showTrail = !showTrail;
        ApplyPathVisibilitySettings();
    }

    private void ApplyPathVisibilitySettings()
    {
        if (droneManager != null)
        {
            droneManager.ApplyPathVisibilityToAll(showPlannedPath, showTrail);
        }

        transientMessage = $"显示 规划线{(showPlannedPath ? "开" : "关")} 航迹{(showTrail ? "开" : "关")}";
        RefreshAllLabels();
    }

    private void RebuildFleet()
    {
        if (droneManager == null)
        {
            transientMessage = "未找到 DroneManager";
            RefreshAllLabels();
            return;
        }

        if (simulationManager != null && simulationManager.currentState != SimulationState.Idle)
        {
            simulationManager.OnResetClicked();
        }

        droneManager.schedulerAlgorithm = schedulerOptions[schedulerIndex];
        droneManager.pathPlannerType = plannerOptions[plannerIndex];
        droneManager.ApplyPlanningSettings(
            configuredPlanningGridCellSize,
            configuredAllowDiagonalPlanning,
            configuredAutoConfigureObstacles,
            BuildPlanningWorldMin(),
            BuildPlanningWorldMax());
        droneManager.RespawnDrones(configuredDroneCount);
        hasPendingDroneCountChange = false;
        droneManager.ApplyDroneSpeedToAll(configuredDroneSpeed);
        droneManager.ApplyPathVisibilityToAll(showPlannedPath, showTrail);

        if (cameraManager == null)
        {
            cameraManager = RuntimeSceneRegistry.Get<CameraManager>(this);
        }

        if (cameraManager != null)
        {
            cameraManager.RefreshManagedDrones();
            cameraManager.SetFollowOffset(new Vector3(
                cameraManager.followOffset.x,
                configuredFollowHeight,
                -configuredFollowDistance));
        }

        transientMessage = $"机群已重建为 {configuredDroneCount} 架";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void SyncAndApplyToCurrentFleet()
    {
        if (droneManager != null)
        {
            droneManager.schedulerAlgorithm = schedulerOptions[schedulerIndex];
            droneManager.pathPlannerType = plannerOptions[plannerIndex];
            droneManager.ApplyPlanningSettings(
                configuredPlanningGridCellSize,
                configuredAllowDiagonalPlanning,
                configuredAutoConfigureObstacles,
                BuildPlanningWorldMin(),
                BuildPlanningWorldMax());
            droneManager.ApplyDroneSpeedToAll(configuredDroneSpeed);
            droneManager.ApplyPathVisibilityToAll(showPlannedPath, showTrail);
        }

        if (cameraManager != null)
        {
            cameraManager.SetFollowOffset(new Vector3(
                cameraManager.followOffset.x,
                configuredFollowHeight,
                -configuredFollowDistance));
        }

        transientMessage = "已同步到当前机群";
        RefreshAllLabels();
        RefreshSummary();
    }


    private void SwitchToOverviewCamera()
    {
        if (cameraManager == null)
        {
            return;
        }

        cameraManager.SwitchToOverview();
        transientMessage = "已切到总览视角";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void SwitchToFollowCamera()
    {
        if (cameraManager == null)
        {
            return;
        }

        cameraManager.SwitchToFollow();
        transientMessage = "已切到跟随视角";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void SwitchToTopDownCamera()
    {
        if (cameraManager == null)
        {
            return;
        }

        cameraManager.SwitchToTopDown2D();
        transientMessage = "已切到2D俯视轨迹视图";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void FocusNextDrone()
    {
        if (cameraManager == null)
        {
            return;
        }

        cameraManager.FocusNextDrone();
        transientMessage = "已切换到下一架";
        RefreshAllLabels();
        RefreshSummary();
    }
}
