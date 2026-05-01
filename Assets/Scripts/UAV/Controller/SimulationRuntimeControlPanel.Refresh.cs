using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class SimulationRuntimeControlPanel
{
    private void SyncFromSystems()
    {
        CacheReferences();

        if (droneManager != null)
        {
            schedulerIndex = Array.IndexOf(schedulerOptions, droneManager.schedulerAlgorithm);
            plannerIndex = Array.IndexOf(plannerOptions, droneManager.pathPlannerType);
            int managerDroneCount = Mathf.Clamp(droneManager.droneCount, MinDroneCount, MaxDroneCount);
            if (!hasPendingDroneCountChange || managerDroneCount == configuredDroneCount)
            {
                configuredDroneCount = managerDroneCount;
                hasPendingDroneCountChange = false;
            }
            else
            {
                configuredDroneCount = Mathf.Clamp(configuredDroneCount, MinDroneCount, MaxDroneCount);
            }

            configuredPlanningGridCellSize = Mathf.Clamp(droneManager.planningGridCellSize, MinPlanningGridCellSize, MaxPlanningGridCellSize);
            configuredPlanningMinX = droneManager.planningWorldMin.x;
            configuredPlanningMaxX = droneManager.planningWorldMax.x;
            configuredPlanningMinZ = droneManager.planningWorldMin.z;
            configuredPlanningMaxZ = droneManager.planningWorldMax.z;
            configuredPlanningMinY = droneManager.planningWorldMin.y;
            configuredPlanningMaxY = droneManager.planningWorldMax.y;
            configuredAllowDiagonalPlanning = droneManager.allowDiagonalPlanning;
            configuredAutoConfigureObstacles = droneManager.autoConfigurePlanningObstacles;

            if (droneManager.drones.Count > 0 && droneManager.drones[0] != null)
            {
                configuredDroneSpeed = droneManager.drones[0].speed;
                DronePathVisualizer visualizer = droneManager.drones[0].GetComponent<DronePathVisualizer>();
                if (visualizer != null)
                {
                    showPlannedPath = visualizer.showPlannedPath;
                    showTrail = visualizer.showTrail;
                }
            }
            else if (droneManager.dronePrefab != null)
            {
                configuredDroneSpeed = droneManager.dronePrefab.speed;
            }
        }

        configuredDroneSpeed = Mathf.Clamp(configuredDroneSpeed <= 0f ? 5f : configuredDroneSpeed, MinDroneSpeed, MaxDroneSpeed);
        configuredTimeScale = Mathf.Clamp(Time.timeScale <= 0f ? 1f : Time.timeScale, MinTimeScale, MaxTimeScale);
        NormalizePlanningBounds();

        if (cameraManager != null)
        {
            configuredFollowHeight = Mathf.Clamp(cameraManager.followOffset.y, MinFollowHeight, MaxFollowHeight);
            configuredFollowDistance = Mathf.Clamp(Mathf.Abs(cameraManager.followOffset.z), MinFollowDistance, MaxFollowDistance);
        }
        else
        {
            configuredFollowHeight = Mathf.Clamp(configuredFollowHeight, MinFollowHeight, MaxFollowHeight);
            configuredFollowDistance = Mathf.Clamp(configuredFollowDistance, MinFollowDistance, MaxFollowDistance);
        }

        if (obstacleEditor != null)
        {
            configuredObstacleHeight = Mathf.Clamp(obstacleEditor.defaultObstacleHeight, MinObstacleHeight, MaxObstacleHeight);
            configuredObstacleScale = Mathf.Clamp(obstacleEditor.GetDefaultObstacleScaleMultiplier(), MinObstacleScale, MaxObstacleScale);
            configuredObstacleTemplateName = obstacleEditor.GetCurrentTemplateDisplayName();
        }
        else
        {
            configuredObstacleHeight = Mathf.Clamp(configuredObstacleHeight, MinObstacleHeight, MaxObstacleHeight);
            configuredObstacleScale = Mathf.Clamp(configuredObstacleScale, MinObstacleScale, MaxObstacleScale);
            configuredObstacleTemplateName = "长方体";
        }

        if (schedulerIndex < 0)
        {
            schedulerIndex = 0;
        }

        if (plannerIndex < 0)
        {
            plannerIndex = 0;
        }

        if (batchExperimentRunner != null)
        {
            configuredBatchRunCount = Mathf.Clamp(batchExperimentRunner.batchRunCount, MinBatchRunCount, MaxBatchRunCount);
        }

        EnsureExperimentPresetCatalogLoaded();
        SyncExperimentSelectionFromSystems();
        RefreshExportDirectoryUi(true);
    }

    private void RefreshAllLabels()
    {
        if (schedulerValueText != null)
        {
            schedulerValueText.text = FormatSchedulerName(schedulerOptions[schedulerIndex]);
        }

        if (plannerValueText != null)
        {
            plannerValueText.text = FormatPlannerName(plannerOptions[plannerIndex]);
        }

        if (droneCountValueText != null)
        {
            droneCountValueText.text = configuredDroneCount.ToString();
        }

        if (droneSpeedValueText != null)
        {
            droneSpeedValueText.text = $"{configuredDroneSpeed:0.0}m/s";
        }

        if (planningGridValueText != null)
        {
            planningGridValueText.text = $"{configuredPlanningGridCellSize:0.0}m";
        }

        if (planningMinXValueText != null)
        {
            planningMinXValueText.text = configuredPlanningMinX.ToString("0");
        }

        if (planningMaxXValueText != null)
        {
            planningMaxXValueText.text = configuredPlanningMaxX.ToString("0");
        }

        if (planningMinZValueText != null)
        {
            planningMinZValueText.text = configuredPlanningMinZ.ToString("0");
        }

        if (planningMaxZValueText != null)
        {
            planningMaxZValueText.text = configuredPlanningMaxZ.ToString("0");
        }

        if (planningMinYValueText != null)
        {
            planningMinYValueText.text = configuredPlanningMinY.ToString("0");
        }

        if (planningMaxYValueText != null)
        {
            planningMaxYValueText.text = configuredPlanningMaxY.ToString("0");
        }

        RefreshRLCaseCatalog();
        if (rlCaseValueText != null)
        {
            rlCaseValueText.text = rlCaseNames.Count > 0
                ? rlCaseNames[Mathf.Clamp(selectedRLCaseIndex, 0, rlCaseNames.Count - 1)]
                : "无案例";
        }

        if (timeScaleValueText != null)
        {
            timeScaleValueText.text = $"{configuredTimeScale:0.00}x";
        }

        if (followHeightValueText != null)
        {
            followHeightValueText.text = $"{configuredFollowHeight:0.0}m";
        }

        if (followDistanceValueText != null)
        {
            followDistanceValueText.text = $"{configuredFollowDistance:0.0}m";
        }

        if (obstacleHeightValueText != null)
        {
            obstacleHeightValueText.text = $"{configuredObstacleHeight:0.0}m";
        }

        if (obstacleScaleValueText != null)
        {
            obstacleScaleValueText.text = $"{configuredObstacleScale:0.00}x";
        }

        if (obstacleStyleValueText != null)
        {
            obstacleStyleValueText.text = configuredObstacleTemplateName;
        }

        if (batchRunCountValueText != null)
        {
            batchRunCountValueText.text = configuredBatchRunCount.ToString();
        }

        RefreshExperimentCenterLabels();
        UpdateToggleButton(plannedPathToggleButton, showPlannedPath);
        UpdateToggleButton(trailToggleButton, showTrail);
        UpdateToggleButton(diagonalPlanningToggleButton, configuredAllowDiagonalPlanning);
        UpdateToggleButton(obstacleAutoConfigToggleButton, configuredAutoConfigureObstacles);
        UpdateToggleButton(
            taskQueueVisualizationToggleButton,
            taskQueueVisualizer != null && taskQueueVisualizer.ShowTaskQueues);
        UpdateToggleButton(
            visualizationObstacleTransparencyToggleButton,
            algorithmVisualizerManager != null && algorithmVisualizerManager.ObstacleTransparencyEnabled);

        if (expandButtonText != null)
        {
            expandButtonText.text = isExpanded ? "收起" : "展开";
        }

        if (footerText != null)
        {
            footerText.text = $"{transientMessage}  |  F5开始/继续 F6暂停 F7重置 F8重建 F9演示播放 F10退出 F11演示单步 F12演示重置 Ctrl+Shift+C/J/B/X 导出/批量";
        }

        RefreshVisualizationPanel();
        RefreshSchedulingResultPanel();
        RefreshEvaluationPanel();
        RefreshExportDirectoryUi(false);
        RefreshBatchStatus();
    }

    private void RefreshSummary()
    {
        if (summaryText == null)
        {
            return;
        }

        int droneCount = droneManager != null ? droneManager.drones.Count : 0;
        TaskPoint[] taskPoints = SimulationContext.GetOrCreate(this).GetTaskPoints();
        int totalTaskCount = taskPoints.Length;
        int completedTaskCount = CountTasksByState(taskPoints, TaskState.Completed);
        int waitingDroneCount = CountDronesByState(DroneState.Waiting);
        int totalConflictCount = CountTotalConflictEvents();
        int buildingWarningCount = droneManager != null ? droneManager.GetBuildingWarningCount() : 0;
        int spawnPointCount = spawnPointManager != null ? spawnPointManager.GetSpawnPointCount() : 0;
        int customObstacleCount = obstacleEditor != null ? obstacleEditor.GetCustomObstacleCount() : 0;
        string simulationState = simulationManager != null ? FormatSimulationState(simulationManager.currentState) : "未知";
        string elapsedTime = simulationManager != null ? FormatDuration(simulationManager.ElapsedSimulationTime) : "--:--";
        string cameraMode = "未连接";
        string cameraTarget = "-";

        if (cameraManager != null)
        {
            cameraMode = cameraManager.GetCurrentModeDisplayName();
            cameraTarget = cameraManager.targetDrone != null ? cameraManager.targetDrone.name : "-";
        }

        if (!isExpanded)
        {
            summaryText.enableWordWrapping = false;
            summaryText.overflowMode = TextOverflowModes.Ellipsis;
            summaryText.alignment = TextAlignmentOptions.Left;
            summaryText.text = $"状态 {simulationState}  用时 {elapsedTime}  任务 {completedTaskCount}/{totalTaskCount}  机群 {droneCount}  冲突 {totalConflictCount}";
            RefreshStats(taskPoints, cameraTarget);
            return;
        }

        summaryText.enableWordWrapping = true;
        summaryText.overflowMode = TextOverflowModes.Overflow;
        summaryText.alignment = TextAlignmentOptions.TopLeft;
        summaryText.text =
            $"状态 {simulationState}  用时 {elapsedTime}  任务 {completedTaskCount}/{totalTaskCount}\n" +
            $"镜头 {cameraMode}  目标 {cameraTarget}  等待 {waitingDroneCount}  冲突 {totalConflictCount}  建筑告警 {buildingWarningCount}  机群 {droneCount}  起点 {spawnPointCount}  自定义障碍 {customObstacleCount}";

        RefreshStats(taskPoints, cameraTarget);
    }

    private void RefreshVisualizationPanel()
    {
        string selectedDroneLabel = algorithmVisualizerManager != null
            ? algorithmVisualizerManager.GetSelectedDroneLabel()
            : "等待规划";
        string modeLabel = algorithmVisualizerManager != null
            ? algorithmVisualizerManager.GetModeDisplayName()
            : "完整过程";
        string speedLabel = algorithmVisualizerManager != null
            ? algorithmVisualizerManager.GetPlaybackSpeedLabel()
            : "1x";

        if (visualizationSelectedDroneValueText != null)
        {
            visualizationSelectedDroneValueText.text = selectedDroneLabel;
        }

        if (visualizationModeValueText != null)
        {
            visualizationModeValueText.text = modeLabel;
        }

        if (visualizationSpeedValueText != null)
        {
            visualizationSpeedValueText.text = speedLabel;
        }

        if (visualizationStatusText != null)
        {
            visualizationStatusText.text = algorithmVisualizerManager != null
                ? $"无人机: {selectedDroneLabel}\n算法: {algorithmVisualizerManager.GetCurrentAlgorithmLabel()}\n步骤: {algorithmVisualizerManager.GetCurrentStepLabel()}\n状态: {algorithmVisualizerManager.GetPlaybackStateLabel()}\n建筑: {algorithmVisualizerManager.GetObstacleTransparencyLabel()}"
                : "无人机: 等待规划\n算法: 未连接\n步骤: 0 / 0\n状态: 暂无轨迹";
        }

        if (visualizationDescriptionText != null)
        {
            visualizationDescriptionText.text = algorithmVisualizerManager != null
                ? algorithmVisualizerManager.GetCurrentDescription()
                : "当前场景还没有可播放的路径规划过程。开始仿真后，这里会显示节点扩展、候选路径变化和最终回溯说明。";
        }

        if (visualizationLegendText != null)
        {
            visualizationLegendText.text = algorithmVisualizerManager != null
                ? algorithmVisualizerManager.GetLegendText()
                : "图例将在连接算法可视化管理器后显示。";
        }

        UpdateVisualizationButtons();
    }

    private void RefreshSchedulingResultPanel()
    {
        if (schedulingResultText != null)
        {
            schedulingResultText.text = taskQueueVisualizer != null
                ? taskQueueVisualizer.BuildSchedulingSummaryText()
                : "调度结果：任务队列可视化组件未连接";
        }

        if (taskQueueVisualizationToggleButton != null)
        {
            taskQueueVisualizationToggleButton.interactable = taskQueueVisualizer != null;
        }

        RefreshSchedulingResultCardLayout();
    }

    private void RefreshSchedulingResultCardLayout()
    {
        if (schedulingResultText == null || schedulingResultLayoutElement == null)
        {
            return;
        }

        float textWidth = schedulingResultText.rectTransform.rect.width;
        if (textWidth <= 1f)
        {
            textWidth = 280f;
        }

        Vector2 preferredSize = schedulingResultText.GetPreferredValues(schedulingResultText.text, textWidth, 0f);
        float targetHeight = Mathf.Max(168f, preferredSize.y + StatsCardVerticalPadding);
        schedulingResultLayoutElement.preferredHeight = targetHeight;

        if (scrollContentRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRoot);
        }
    }

    private void RefreshEvaluationPanel()
    {
        if (evaluationText == null)
        {
            return;
        }

        AlgorithmEvaluationSnapshot snapshot = CaptureEvaluationSnapshot();
        StringBuilder builder = new StringBuilder(768);
        builder.Append("当前组合: 调度 ")
            .Append(snapshot.schedulerName)
            .Append(" / 路径 ")
            .Append(snapshot.plannerName)
            .AppendLine();

        builder.Append("任务完成: ")
            .Append(snapshot.completedTaskCount)
            .Append('/')
            .Append(snapshot.totalTaskCount)
            .Append("  ")
            .Append(snapshot.completionRate.ToString("0"))
            .Append('%')
            .Append("    用时 ")
            .Append(FormatDuration(snapshot.elapsedSeconds))
            .AppendLine();

        builder.Append("任务分配: 总 ")
            .Append(snapshot.assignedTaskCount)
            .Append("  平均 ")
            .Append(snapshot.averageAssignedTasks.ToString("0.0"))
            .Append("/机  均衡差 ")
            .Append(snapshot.assignmentSpread)
            .AppendLine();

        builder.Append("路径指标: 总 ")
            .Append(snapshot.totalFlightDistance.ToString("0.0"))
            .Append("m  平均 ")
            .Append(snapshot.averageFlightDistance.ToString("0.0"))
            .Append("m  最长 ")
            .Append(snapshot.longestDroneLabel)
            .Append(' ')
            .Append(snapshot.longestDroneDistance.ToString("0.0"))
            .Append('m')
            .AppendLine();

        builder.Append("协同指标: 等待 ")
            .Append(snapshot.totalWaitCount)
            .Append("  冲突 ")
            .Append(snapshot.totalConflictCount)
            .Append("  建筑告警 ")
            .Append(snapshot.buildingWarningCount);

        if (evaluationHistory.Count == 0)
        {
            builder.AppendLine()
                .AppendLine()
                .Append("提示: 完成或暂停一轮仿真后，可点击“记录本轮”保留当前算法组合，便于后续切换算法进行对比。");
        }
        else
        {
            builder.AppendLine()
                .AppendLine()
                .AppendLine("最近记录:");

            for (int i = 0; i < evaluationHistory.Count; i++)
            {
                AlgorithmEvaluationSnapshot item = evaluationHistory[i];
                builder.Append(i + 1)
                    .Append(". ")
                    .Append(item.timeLabel)
                    .Append("  ")
                    .Append(item.schedulerName)
                    .Append('/')
                    .Append(item.plannerName)
                    .Append("  任务 ")
                    .Append(item.completedTaskCount)
                    .Append('/')
                    .Append(item.totalTaskCount)
                    .Append("  距离 ")
                    .Append(item.totalFlightDistance.ToString("0.0"))
                    .Append("m  均衡差 ")
                    .Append(item.assignmentSpread)
                    .Append("  等待 ")
                    .Append(item.totalWaitCount)
                    .Append("  冲突 ")
                    .Append(item.totalConflictCount);

                if (i < evaluationHistory.Count - 1)
                {
                    builder.AppendLine();
                }
            }
        }

        evaluationText.text = builder.ToString();
        RefreshEvaluationCardLayout();
    }

    private AlgorithmEvaluationSnapshot CaptureEvaluationSnapshot()
    {
        TaskPoint[] taskPoints = SimulationContext.GetOrCreate(this).GetTaskPoints();
        int totalTaskCount = taskPoints != null ? taskPoints.Length : 0;
        int completedTaskCount = CountTasksByState(taskPoints, TaskState.Completed);
        int assignedTaskCount = 0;
        int droneCount = 0;
        int minAssigned = int.MaxValue;
        int maxAssigned = 0;
        float totalFlightDistance = 0f;
        float longestDroneDistance = 0f;
        string longestDroneLabel = "-";
        int totalWaitCount = 0;
        int totalConflictCount = 0;

        if (droneManager != null && droneManager.droneDataList != null)
        {
            for (int i = 0; i < droneManager.droneDataList.Count; i++)
            {
                DroneData data = droneManager.droneDataList[i];
                if (data == null)
                {
                    continue;
                }

                int queueLength = data.taskQueue != null ? data.taskQueue.Length : 0;
                assignedTaskCount += queueLength;
                minAssigned = Mathf.Min(minAssigned, queueLength);
                maxAssigned = Mathf.Max(maxAssigned, queueLength);
                totalFlightDistance += data.totalFlightDistance;
                totalWaitCount += data.waitCount;
                totalConflictCount += data.conflictCount;
                droneCount++;

                if (data.totalFlightDistance >= longestDroneDistance)
                {
                    longestDroneDistance = data.totalFlightDistance;
                    longestDroneLabel = string.IsNullOrWhiteSpace(data.droneName)
                        ? $"U{data.droneId:D2}"
                        : data.droneName;
                }
            }
        }

        if (droneCount == 0)
        {
            minAssigned = 0;
        }

        string schedulerName = schedulerOptions != null && schedulerOptions.Length > 0
            ? FormatSchedulerName(schedulerOptions[schedulerIndex])
            : (droneManager != null ? UAVAlgorithmNames.GetSchedulerDisplayName(droneManager.schedulerAlgorithm) : "-");
        string plannerName = plannerOptions != null && plannerOptions.Length > 0
            ? FormatPlannerName(plannerOptions[plannerIndex])
            : (droneManager != null ? UAVAlgorithmNames.GetPlannerDisplayName(droneManager.pathPlannerType) : "-");

        float completionRate = totalTaskCount > 0
            ? completedTaskCount * 100f / totalTaskCount
            : 0f;

        return new AlgorithmEvaluationSnapshot
        {
            timeLabel = DateTime.Now.ToString("HH:mm:ss"),
            schedulerName = schedulerName,
            plannerName = plannerName,
            totalTaskCount = totalTaskCount,
            completedTaskCount = completedTaskCount,
            assignedTaskCount = assignedTaskCount,
            droneCount = droneCount,
            assignmentSpread = Mathf.Max(0, maxAssigned - minAssigned),
            averageAssignedTasks = droneCount > 0 ? assignedTaskCount / (float)droneCount : 0f,
            totalFlightDistance = totalFlightDistance,
            averageFlightDistance = droneCount > 0 ? totalFlightDistance / droneCount : 0f,
            longestDroneLabel = longestDroneLabel,
            longestDroneDistance = longestDroneDistance,
            totalWaitCount = totalWaitCount,
            totalConflictCount = totalConflictCount,
            buildingWarningCount = droneManager != null ? droneManager.GetBuildingWarningCount() : 0,
            elapsedSeconds = simulationManager != null ? simulationManager.ElapsedSimulationTime : 0f,
            completionRate = completionRate
        };
    }

    private void RefreshEvaluationCardLayout()
    {
        if (evaluationText == null || evaluationLayoutElement == null)
        {
            return;
        }

        float textWidth = evaluationText.rectTransform.rect.width;
        if (textWidth <= 1f)
        {
            textWidth = 280f;
        }

        Vector2 preferredSize = evaluationText.GetPreferredValues(evaluationText.text, textWidth, 0f);
        float targetHeight = Mathf.Max(EvaluationCardMinHeight, preferredSize.y + StatsCardVerticalPadding);
        evaluationLayoutElement.preferredHeight = targetHeight;

        if (scrollContentRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRoot);
        }
    }

    private void UpdateVisualizationButtons()
    {
        bool hasTrace = algorithmVisualizerManager != null && algorithmVisualizerManager.HasPlayableTrace();
        bool isPlaying = algorithmVisualizerManager != null &&
                         algorithmVisualizerManager.PlaybackState == PathPlanningVisualizationPlaybackState.Playing;

        if (visualizationPlayButton != null)
        {
            visualizationPlayButton.interactable = hasTrace;
        }

        if (visualizationPauseButton != null)
        {
            visualizationPauseButton.interactable = hasTrace && isPlaying;
        }

        if (visualizationStepButton != null)
        {
            visualizationStepButton.interactable = hasTrace;
        }

        if (visualizationResetButton != null)
        {
            visualizationResetButton.interactable = hasTrace;
        }

        if (visualizationObstacleTransparencyToggleButton != null)
        {
            visualizationObstacleTransparencyToggleButton.interactable = algorithmVisualizerManager != null;
        }
    }

    private void ToggleExpanded()
    {
        isExpanded = !isExpanded;
        ApplyExpandState();
        transientMessage = isExpanded ? "已展开运行面板" : "已收起运行面板";
        RefreshAllLabels();
    }

    private void HandleRuntimeShortcuts()
    {
        bool inputFocused = exportDirectoryInputField != null && exportDirectoryInputField.isFocused;
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool quitRequested =
            Input.GetKeyDown(KeyCode.F10) ||
            (ctrlHeld && Input.GetKeyDown(KeyCode.Q));

        if (quitRequested)
        {
            if (inputFocused)
            {
                return;
            }

            RequestQuitApplication();
            return;
        }

        if (inputFocused)
        {
            return;
        }

        if (ctrlHeld && shiftHeld)
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                ExportCurrentResultToCsv();
                return;
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                ExportCurrentResultToJson();
                return;
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                StartBatchExperiments();
                return;
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                StopBatchExperiments();
                return;
            }

            if (Input.GetKeyDown(KeyCode.N))
            {
                StartNewExportSession();
                return;
            }
        }

        if (simulationManager != null && Input.GetKeyDown(KeyCode.F5))
        {
            simulationManager.OnStartClicked();
            transientMessage = "已通过快捷键触发开始/继续";
            RefreshAllLabels();
            RefreshSummary();
            return;
        }

        if (algorithmVisualizerManager != null && Input.GetKeyDown(KeyCode.F9))
        {
            if (algorithmVisualizerManager.PlaybackState == PathPlanningVisualizationPlaybackState.Playing)
            {
                algorithmVisualizerManager.Pause();
                transientMessage = "已通过快捷键暂停算法过程演示";
            }
            else
            {
                algorithmVisualizerManager.Resume();
                transientMessage = "已通过快捷键播放算法过程演示";
            }

            RefreshAllLabels();
            return;
        }

        if (simulationManager != null && Input.GetKeyDown(KeyCode.F6))
        {
            simulationManager.OnPauseClicked();
            transientMessage = "已通过快捷键触发暂停";
            RefreshAllLabels();
            RefreshSummary();
            return;
        }

        if (algorithmVisualizerManager != null && Input.GetKeyDown(KeyCode.F11))
        {
            algorithmVisualizerManager.StepForward();
            transientMessage = "已通过快捷键单步推进算法过程";
            RefreshAllLabels();
            return;
        }

        if (algorithmVisualizerManager != null && Input.GetKeyDown(KeyCode.F12))
        {
            algorithmVisualizerManager.ResetPlayback();
            transientMessage = "已通过快捷键重置算法过程演示";
            RefreshAllLabels();
            return;
        }

        if (simulationManager != null && Input.GetKeyDown(KeyCode.F7))
        {
            simulationManager.OnResetClicked();
            transientMessage = "已通过快捷键触发重置";
            RefreshAllLabels();
            RefreshSummary();
            return;
        }

        if (Input.GetKeyDown(KeyCode.F8))
        {
            RebuildFleet();
        }
    }

    private void RequestQuitApplication()
    {
        transientMessage = "正在退出应用";
        RefreshAllLabels();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ApplyExpandState()
    {
        if (panelRoot == null)
        {
            return;
        }

        panelRoot.sizeDelta = GetTargetPanelSize();

        if (summaryCardRoot != null)
        {
            summaryCardRoot.offsetMin = new Vector2(12f, isExpanded ? -108f : -116f);
            summaryCardRoot.offsetMax = new Vector2(-12f, -50f);
        }

        if (bodyRoot != null)
        {
            bodyRoot.gameObject.SetActive(isExpanded);
        }

        if (footerRoot != null)
        {
            footerRoot.gameObject.SetActive(isExpanded);
        }

        if (!isExpanded && summaryText != null)
        {
            summaryText.enableWordWrapping = false;
            summaryText.overflowMode = TextOverflowModes.Ellipsis;
            summaryText.alignment = TextAlignmentOptions.Left;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
    }

    private void RefreshExportDirectoryUi(bool syncInputText)
    {
        if (resultExporter == null)
        {
            return;
        }

        if (exportDirectoryStatusText != null)
        {
            string mode = resultExporter.IsUsingCustomExportDirectory ? "当前: 自定义目录" : "当前: 默认目录";
            exportDirectoryStatusText.text =
                $"{mode}\n" +
                $"根目录: {resultExporter.GetExportDirectoryPath()}\n" +
                $"归档: {resultExporter.GetArchiveDirectoryPath()}";
        }

        if (syncInputText && exportDirectoryInputField != null)
        {
            string pathForInput = resultExporter.IsUsingCustomExportDirectory
                ? resultExporter.customExportDirectory
                : resultExporter.GetExportDirectoryPath();
            exportDirectoryInputField.SetTextWithoutNotify(pathForInput);
        }
    }

    private void RefreshBatchStatus()
    {
        if (batchStatusText == null)
        {
            return;
        }

        if (batchExperimentRunner == null)
        {
            batchStatusText.text = "批量实验未连接";
            return;
        }

        string state = batchExperimentRunner.IsBatchRunning ? "运行中" : "空闲";
        int totalRunCount = Mathf.Clamp(batchExperimentRunner.batchRunCount, MinBatchRunCount, MaxBatchRunCount);
        batchStatusText.text =
            $"状态: {state}\n" +
            $"预设: {batchExperimentRunner.ActivePresetName}\n" +
            $"进度: {batchExperimentRunner.CompletedRunCount}/{totalRunCount}  当前轮: {Mathf.Max(batchExperimentRunner.CurrentRunIndex, 0)}\n" +
            batchExperimentRunner.LastBatchMessage;
    }

    private string FormatSchedulerName(SchedulerAlgorithmType algorithmType)
    {
        return UAVAlgorithmNames.GetSchedulerDisplayName(algorithmType);
    }

    private string FormatPlannerName(PathPlannerType plannerType)
    {
        return UAVAlgorithmNames.GetPlannerDisplayName(plannerType);
    }

    private string FormatSimulationState(SimulationState state)
    {
        switch (state)
        {
            case SimulationState.Running:
                return "运行中";
            case SimulationState.Paused:
                return "已暂停";
            case SimulationState.Idle:
            default:
                return "就绪";
        }
    }

    private void NormalizePlanningBounds()
    {
        float minimumSpan = Mathf.Max(configuredPlanningGridCellSize * 2f, 4f);
        float minimumHeightSpan = 1f;

        configuredPlanningMinX = Mathf.Clamp(configuredPlanningMinX, MinPlanningBoundary, MaxPlanningBoundary - minimumSpan);
        configuredPlanningMaxX = Mathf.Clamp(configuredPlanningMaxX, configuredPlanningMinX + minimumSpan, MaxPlanningBoundary);
        configuredPlanningMinZ = Mathf.Clamp(configuredPlanningMinZ, MinPlanningBoundary, MaxPlanningBoundary - minimumSpan);
        configuredPlanningMaxZ = Mathf.Clamp(configuredPlanningMaxZ, configuredPlanningMinZ + minimumSpan, MaxPlanningBoundary);
        configuredPlanningMinY = Mathf.Clamp(configuredPlanningMinY, MinPlanningHeight, MaxPlanningHeight - minimumHeightSpan);
        configuredPlanningMaxY = Mathf.Clamp(configuredPlanningMaxY, configuredPlanningMinY + minimumHeightSpan, MaxPlanningHeight);
    }

    private Vector3 BuildPlanningWorldMin()
    {
        return new Vector3(configuredPlanningMinX, configuredPlanningMinY, configuredPlanningMinZ);
    }

    private Vector3 BuildPlanningWorldMax()
    {
        return new Vector3(configuredPlanningMaxX, configuredPlanningMaxY, configuredPlanningMaxZ);
    }

    private void RefreshStats(TaskPoint[] taskPoints, string cameraTarget)
    {
        if (statsText == null)
        {
            return;
        }

        int totalTaskCount = taskPoints != null ? taskPoints.Length : 0;
        int pendingTaskCount = CountTasksByState(taskPoints, TaskState.Pending);
        int inProgressTaskCount = CountTasksByState(taskPoints, TaskState.InProgress);
        int completedTaskCount = CountTasksByState(taskPoints, TaskState.Completed);

        int idleDroneCount = CountDronesByState(DroneState.Idle);
        int movingDroneCount = CountDronesByState(DroneState.Moving);
        int waitingDroneCount = CountDronesByState(DroneState.Waiting);
        int finishedDroneCount = CountDronesByState(DroneState.Finished);

        float totalFlightDistance = 0f;
        int totalWaitCount = 0;
        int totalConflictCount = 0;
        int droneCount = 0;
        if (droneManager != null && droneManager.droneDataList != null)
        {
            foreach (DroneData data in droneManager.droneDataList)
            {
                if (data == null)
                {
                    continue;
                }

                totalFlightDistance += data.totalFlightDistance;
                totalWaitCount += data.waitCount;
                totalConflictCount += data.conflictCount;
                droneCount++;
            }
        }

        float averageFlightDistance = droneCount > 0 ? totalFlightDistance / droneCount : 0f;
        string schedulerName = schedulerOptions != null && schedulerOptions.Length > 0
            ? FormatSchedulerName(schedulerOptions[schedulerIndex])
            : "-";
        string plannerName = plannerOptions != null && plannerOptions.Length > 0
            ? FormatPlannerName(plannerOptions[plannerIndex])
            : "-";
        string elapsedTime = simulationManager != null ? FormatDuration(simulationManager.ElapsedSimulationTime) : "--:--";

        StringBuilder builder = new StringBuilder(512);
        builder.Append("调度算法: ").Append(schedulerName)
            .Append("    路径规划: ").Append(plannerName).AppendLine();
        builder.Append("任务进度: ").Append(completedTaskCount).Append(" / ").Append(totalTaskCount).AppendLine();
        builder.Append("待执行 / 执行中 / 已完成: ")
            .Append(pendingTaskCount).Append(" / ")
            .Append(inProgressTaskCount).Append(" / ")
            .Append(completedTaskCount).AppendLine();
        builder.Append("无人机状态: 空闲 ").Append(idleDroneCount)
            .Append("  移动 ").Append(movingDroneCount)
            .Append("  等待 ").Append(waitingDroneCount)
            .Append("  完成 ").Append(finishedDroneCount).AppendLine();
        builder.Append("总飞行距离: ").Append(totalFlightDistance.ToString("0.0"))
            .Append(" m    平均单机: ").Append(averageFlightDistance.ToString("0.0")).Append(" m").AppendLine();
        builder.Append("等待次数: ").Append(totalWaitCount)
            .Append("    冲突次数: ").Append(totalConflictCount).AppendLine();
        builder.Append("仿真耗时: ").Append(elapsedTime)
            .Append("    当前跟随: ").Append(string.IsNullOrWhiteSpace(cameraTarget) ? "-" : cameraTarget);

        if (droneManager != null && droneManager.droneDataList != null && droneManager.droneDataList.Count > 0)
        {
            builder.AppendLine().AppendLine();
            for (int i = 0; i < droneManager.droneDataList.Count; i++)
            {
                DroneData data = droneManager.droneDataList[i];
                if (data == null)
                {
                    continue;
                }

                bool hasBuildingAlert = false;
                DroneController drone = droneManager.GetDrone(data.droneId);
                if (drone != null)
                {
                    DronePathVisualizer visualizer = drone.GetComponent<DronePathVisualizer>();
                    hasBuildingAlert = visualizer != null && visualizer.HasBuildingAlert;
                }

                int assignedTaskCount = data.taskQueue != null ? data.taskQueue.Length : 0;
                builder.Append('[').Append(data.droneId.ToString("D2")).Append("] ")
                    .Append(FormatDroneState(data.state))
                    .Append(" | 完成 ").Append(data.completedTasks).Append('/').Append(assignedTaskCount)
                    .Append(" | 距离 ").Append(data.totalFlightDistance.ToString("0.0")).Append(" m")
                    .Append(" | 等待 ").Append(data.waitCount)
                    .Append(" | 冲突 ").Append(data.conflictCount);

                if (!string.IsNullOrWhiteSpace(data.currentPlannerName))
                {
                    builder.Append(" | ").Append(data.currentPlannerName);
                }

                if (data.state == DroneState.Waiting && !string.IsNullOrWhiteSpace(data.waitReason))
                {
                    builder.Append(" | ").Append(data.waitReason);
                }

                if (hasBuildingAlert)
                {
                    builder.Append(" | 建筑告警");
                }

                if (i < droneManager.droneDataList.Count - 1)
                {
                    builder.AppendLine();
                }
            }
        }

        statsText.text = builder.ToString();
        RefreshStatsCardLayout();
    }

    private void RefreshStatsCardLayout()
    {
        if (statsText == null || statsCardLayoutElement == null)
        {
            return;
        }

        float textWidth = statsText.rectTransform.rect.width;
        if (textWidth <= 1f)
        {
            textWidth = 280f;
        }

        Vector2 preferredSize = statsText.GetPreferredValues(statsText.text, textWidth, 0f);
        float targetHeight = Mathf.Max(StatsCardMinHeight, preferredSize.y + StatsCardVerticalPadding);
        statsCardLayoutElement.preferredHeight = targetHeight;

        if (scrollContentRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRoot);
        }
    }

    private int CountTasksByState(TaskPoint[] taskPoints, TaskState state)
    {
        if (taskPoints == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < taskPoints.Length; i++)
        {
            if (taskPoints[i] != null && taskPoints[i].currentState == state)
            {
                count++;
            }
        }

        return count;
    }

    private int CountTotalConflictEvents()
    {
        if (droneManager == null || droneManager.droneDataList == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < droneManager.droneDataList.Count; i++)
        {
            DroneData data = droneManager.droneDataList[i];
            if (data != null)
            {
                total += data.conflictCount;
            }
        }

        return total;
    }

    private int CountDronesByState(DroneState state)
    {
        if (droneManager == null || droneManager.droneDataList == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < droneManager.droneDataList.Count; i++)
        {
            DroneData data = droneManager.droneDataList[i];
            if (data != null && data.state == state)
            {
                count++;
            }
        }

        return count;
    }

    private string FormatDroneState(DroneState state)
    {
        switch (state)
        {
            case DroneState.Moving:
                return "移动中";
            case DroneState.Waiting:
                return "等待中";
            case DroneState.Finished:
                return "已完成";
            case DroneState.Idle:
            default:
                return "空闲";
        }
    }

    private string FormatDuration(float seconds)
    {
        TimeSpan duration = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        if (duration.TotalHours >= 1d)
        {
            return duration.ToString(@"hh\:mm\:ss");
        }

        return duration.ToString(@"mm\:ss");
    }
}
