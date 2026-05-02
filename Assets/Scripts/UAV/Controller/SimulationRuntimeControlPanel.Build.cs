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
    private void BuildPanelIfNeeded()
    {
        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            return;
        }

        Transform existing = canvas.transform.Find(PanelRootName);
        panelRoot = existing as RectTransform;
        if (panelRoot == null)
        {
            GameObject panelObject = new GameObject(
                PanelRootName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(Shadow));
            panelRoot = panelObject.GetComponent<RectTransform>();
            panelRoot.SetParent(canvas.transform, false);
        }

        lastCanvasSize = GetCanvasSize();
        panelRoot.anchorMin = new Vector2(1f, 1f);
        panelRoot.anchorMax = new Vector2(1f, 1f);
        panelRoot.pivot = new Vector2(1f, 1f);
        panelRoot.anchoredPosition = anchoredPosition;
        panelRoot.sizeDelta = GetTargetPanelSize();
        panelRoot.localScale = Vector3.one;
        panelRoot.SetAsLastSibling();

        Image panelImage = panelRoot.GetComponent<Image>();
        ConfigureImageGraphic(panelImage);
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;

        Outline outline = panelRoot.GetComponent<Outline>();
        outline.effectColor = new Color(0.14f, 0.42f, 0.56f, 0.26f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        Shadow shadow = panelRoot.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.30f);
        shadow.effectDistance = new Vector2(0f, -8f);
        shadow.useGraphicAlpha = true;

        ClearChildren(panelRoot);

        RectTransform header = CreatePanelArea("Header", panelRoot, new Vector2(12f, -10f), new Vector2(-12f, -44f));
        TMP_Text title = CreateText("Title", header, "RUNTIME", 18f, AccentColor, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.Left;
        title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        title.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        title.rectTransform.pivot = new Vector2(0f, 0.5f);
        title.rectTransform.anchoredPosition = new Vector2(4f, 0f);
        title.rectTransform.sizeDelta = new Vector2(120f, 24f);

        Button expandButton = CreateActionButton(header, "Expand", SecondaryButtonColor, ToggleExpanded, 64f);
        RectTransform expandRect = expandButton.GetComponent<RectTransform>();
        expandRect.anchorMin = new Vector2(1f, 0.5f);
        expandRect.anchorMax = new Vector2(1f, 0.5f);
        expandRect.pivot = new Vector2(1f, 0.5f);
        expandRect.anchoredPosition = new Vector2(-4f, 0f);
        expandButtonText = expandButton.GetComponentInChildren<TMP_Text>();

        Button quitButton = CreateActionButton(header, "Quit", QuitButtonColor, RequestQuitApplication, 64f);
        RectTransform quitRect = quitButton.GetComponent<RectTransform>();
        quitRect.anchorMin = new Vector2(1f, 0.5f);
        quitRect.anchorMax = new Vector2(1f, 0.5f);
        quitRect.pivot = new Vector2(1f, 0.5f);
        quitRect.anchoredPosition = new Vector2(-74f, 0f);

        TMP_Text quitButtonLabel = quitButton.GetComponentInChildren<TMP_Text>();
        if (quitButtonLabel != null)
        {
            quitButtonLabel.text = "退出";
        }

        summaryCardRoot = CreatePanelArea("Summary", panelRoot, new Vector2(12f, -50f), new Vector2(-12f, -108f));
        ConfigureSection(summaryCardRoot, SectionColor);
        summaryText = CreateText("SummaryText", summaryCardRoot, string.Empty, 14f, PrimaryTextColor, FontStyles.Normal);
        summaryText.enableWordWrapping = true;
        summaryText.overflowMode = TextOverflowModes.Overflow;
        summaryText.enableAutoSizing = true;
        summaryText.fontSizeMin = 11f;
        summaryText.fontSizeMax = 14f;
        summaryText.alignment = TextAlignmentOptions.TopLeft;
        summaryText.rectTransform.anchorMin = Vector2.zero;
        summaryText.rectTransform.anchorMax = Vector2.one;
        summaryText.rectTransform.offsetMin = new Vector2(10f, 8f);
        summaryText.rectTransform.offsetMax = new Vector2(-10f, -8f);

        bodyRoot = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
        bodyRoot.SetParent(panelRoot, false);
        bodyRoot.anchorMin = new Vector2(0f, 0f);
        bodyRoot.anchorMax = new Vector2(1f, 1f);
        bodyRoot.offsetMin = new Vector2(12f, 40f);
        bodyRoot.offsetMax = new Vector2(-12f, -116f);
        ConfigureSection(bodyRoot, new Color(0.04f, 0.11f, 0.17f, 0.94f));
        BuildScrollBody(bodyRoot);

        footerRoot = new GameObject("Footer", typeof(RectTransform)).GetComponent<RectTransform>();
        footerRoot.SetParent(panelRoot, false);
        footerRoot.anchorMin = new Vector2(0f, 0f);
        footerRoot.anchorMax = new Vector2(1f, 0f);
        footerRoot.offsetMin = new Vector2(12f, 10f);
        footerRoot.offsetMax = new Vector2(-12f, 32f);
        ConfigureSection(footerRoot, new Color(0.07f, 0.15f, 0.20f, 0.96f));
        footerText = CreateText("FooterText", footerRoot, string.Empty, 12f, SecondaryTextColor, FontStyles.Normal);
        footerText.enableWordWrapping = false;
        footerText.overflowMode = TextOverflowModes.Ellipsis;
        footerText.alignment = TextAlignmentOptions.Left;
        footerText.rectTransform.anchorMin = Vector2.zero;
        footerText.rectTransform.anchorMax = Vector2.one;
        footerText.rectTransform.offsetMin = new Vector2(10f, 4f);
        footerText.rectTransform.offsetMax = new Vector2(-10f, -4f);
    }

    private void BuildScrollBody(RectTransform parent)
    {
        RectTransform viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
        viewport.SetParent(parent, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(10f, 10f);
        viewport.offsetMax = new Vector2(-12f, -10f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        ConfigureImageGraphic(viewportImage);
        viewportImage.color = new Color(0.02f, 0.07f, 0.10f, 0.18f);
        viewportImage.maskable = true;
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);
        scrollContentRoot = content;

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = parent.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
        scrollRect.inertia = true;

        RectTransform scrollbarRect = new GameObject("Scrollbar", typeof(RectTransform)).GetComponent<RectTransform>();
        scrollbarRect.SetParent(parent, false);
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 1f);
        scrollbarRect.offsetMin = new Vector2(-10f, 10f);
        scrollbarRect.offsetMax = new Vector2(-4f, -10f);

        Image scrollbarTrack = scrollbarRect.gameObject.AddComponent<Image>();
        ConfigureImageGraphic(scrollbarTrack);
        scrollbarTrack.color = new Color(0.10f, 0.18f, 0.24f, 0.72f);

        Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        RectTransform slidingArea = new GameObject("SlidingArea", typeof(RectTransform)).GetComponent<RectTransform>();
        slidingArea.SetParent(scrollbarRect, false);
        slidingArea.anchorMin = Vector2.zero;
        slidingArea.anchorMax = Vector2.one;
        slidingArea.offsetMin = new Vector2(1f, 5f);
        slidingArea.offsetMax = new Vector2(-1f, -5f);

        RectTransform handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        handle.SetParent(slidingArea, false);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = Vector2.zero;
        handle.offsetMax = Vector2.zero;
        Image handleImage = handle.GetComponent<Image>();
        ConfigureImageGraphic(handleImage);
        handleImage.color = AccentColor;

        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handle;

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        RectTransform statsSection = CreateSectionCard(content, "统计概览", "实时摘要与单机运行状态。");
        CreateInfoCard(statsSection, "Stats", out statsText, out statsCardLayoutElement, StatsCardMinHeight);

        RectTransform experimentSection = CreateSectionCard(content, "实验中心", "预设矩阵、快速应用与批量运行。");
        CreateStepperRow(experimentSection, "分组", out experimentGroupValueText, OnPreviousExperimentGroupClicked, OnNextExperimentGroupClicked, 148f);
        CreateStepperRow(experimentSection, "预设", out experimentPresetValueText, OnPreviousExperimentPresetClicked, OnNextExperimentPresetClicked, 164f);
        CreateButtonStripRow(experimentSection, "实验矩阵", new[]
        {
            new ButtonAction("调度", new Color(0.14f, 0.44f, 0.70f, 0.98f), SelectSchedulingExperimentGroup, 48f),
            new ButtonAction("规划", new Color(0.16f, 0.48f, 0.66f, 0.98f), SelectPlanningExperimentGroup, 48f),
            new ButtonAction("机群", new Color(0.18f, 0.40f, 0.62f, 0.98f), SelectScalingExperimentGroup, 48f),
            new ButtonAction("密度", new Color(0.12f, 0.36f, 0.54f, 0.98f), SelectDensityExperimentGroup, 48f)
        });
        CreateInfoCard(experimentSection, "ExperimentPresetSummary", out experimentPresetSummaryText, out _, 112f);
        CreateButtonStripRow(experimentSection, "实验执行", new[]
        {
            new ButtonAction("应用预设", SecondaryButtonColor, ApplySelectedExperimentPreset, 72f),
            new ButtonAction("预设批量", PrimaryButtonColor, StartSelectedPresetBatch, 72f)
        });

        RectTransform resultSection = CreateSectionCard(content, "结果与导出", "导出目录、会话管理和当前批量状态。");
        CreateInfoCard(resultSection, "ExportDirectory", out exportDirectoryStatusText, out _, 96f);
        CreateInputButtonRow(resultSection, "目录", out exportDirectoryInputField, new[]
        {
            new ButtonAction("选择", new Color(0.13f, 0.44f, 0.64f, 0.98f), BrowseCustomExportDirectory, 48f),
            new ButtonAction("新会话", new Color(0.10f, 0.46f, 0.58f, 0.98f), StartNewExportSession, 60f),
            new ButtonAction("应用", PrimaryButtonColor, ApplyCustomExportDirectory, 48f),
            new ButtonAction("默认", SecondaryButtonColor, ResetExportDirectoryToDefault, 48f)
        });
        CreateButtonStripRow(resultSection, "导出结果", new[]
        {
            new ButtonAction("导出CSV", PrimaryButtonColor, ExportCurrentResultToCsv, 68f),
            new ButtonAction("导出JSON", new Color(0.08f, 0.48f, 0.62f, 0.98f), ExportCurrentResultToJson, 74f)
        });
        CreateInfoCard(resultSection, "BatchStatus", out batchStatusText, out _, 72f);
        CreateStepperRow(resultSection, "批次数", out batchRunCountValueText, OnDecreaseBatchRunCountClicked, OnIncreaseBatchRunCountClicked);
        CreateButtonStripRow(resultSection, "批量实验", new[]
        {
            new ButtonAction("开始", PrimaryButtonColor, StartBatchExperiments, 56f),
            new ButtonAction("停止", SecondaryButtonColor, StopBatchExperiments, 56f)
        });

        RectTransform spawnSection = CreateSectionCard(content, "起飞点", "新增、移动和删除起飞位置。");
        CreateButtonStripRow(spawnSection, "编辑模式", new[]
        {
            new ButtonAction("新增", PrimaryButtonColor, ToggleSpawnPointPlacement, 48f),
            new ButtonAction("移动", new Color(0.14f, 0.50f, 0.78f, 0.98f), ToggleSpawnPointMove, 48f),
            new ButtonAction("删除", new Color(0.78f, 0.48f, 0.14f, 0.98f), ToggleSpawnPointDeletion, 48f)
        });
        CreateButtonStripRow(spawnSection, "批量操作", new[]
        {
            new ButtonAction("清空", SecondaryButtonColor, ClearSpawnPoints, 56f)
        });

        RectTransform obstacleSection = CreateSectionCard(content, "障碍物", "会话级建筑编辑与障碍模板控制。");
        CreateButtonStripRow(obstacleSection, "编辑模式", new[]
        {
            new ButtonAction("绘制", PrimaryButtonColor, ToggleObstacleCreateMode, 56f),
            new ButtonAction("删除", new Color(0.78f, 0.48f, 0.14f, 0.98f), ToggleObstacleDeleteMode, 56f)
        });
        CreateButtonStripRow(obstacleSection, "批量操作", new[]
        {
            new ButtonAction("清空", SecondaryButtonColor, ClearCustomObstacles, 56f)
        });
        CreateStepperRow(obstacleSection, "样式", out obstacleStyleValueText, OnPreviousObstacleTemplateClicked, OnNextObstacleTemplateClicked, 156f);
        CreateStepperRow(obstacleSection, "缩放", out obstacleScaleValueText, OnDecreaseObstacleScaleClicked, OnIncreaseObstacleScaleClicked, 108f);
        CreateStepperRow(obstacleSection, "高度", out obstacleHeightValueText, OnDecreaseObstacleHeightClicked, OnIncreaseObstacleHeightClicked, 108f);

        RectTransform algorithmSection = CreateSectionCard(content, "算法与规划", "调度、路径规划和搜索边界。");
        CreateStepperRow(algorithmSection, "调度", out schedulerValueText, OnPreviousSchedulerClicked, OnNextSchedulerClicked, 124f);
        CreateStepperRow(algorithmSection, "路径", out plannerValueText, OnPreviousPlannerClicked, OnNextPlannerClicked, 124f);
        CreateStepperRow(algorithmSection, "网格", out planningGridValueText, OnDecreasePlanningGridClicked, OnIncreasePlanningGridClicked);
        CreateStepperRow(algorithmSection, "X最小", out planningMinXValueText, OnDecreasePlanningMinXClicked, OnIncreasePlanningMinXClicked);
        CreateStepperRow(algorithmSection, "X最大", out planningMaxXValueText, OnDecreasePlanningMaxXClicked, OnIncreasePlanningMaxXClicked);
        CreateStepperRow(algorithmSection, "Z最小", out planningMinZValueText, OnDecreasePlanningMinZClicked, OnIncreasePlanningMinZClicked);
        CreateStepperRow(algorithmSection, "Z最大", out planningMaxZValueText, OnDecreasePlanningMaxZClicked, OnIncreasePlanningMaxZClicked);
        CreateStepperRow(algorithmSection, "检测低", out planningMinYValueText, OnDecreasePlanningMinYClicked, OnIncreasePlanningMinYClicked);
        CreateStepperRow(algorithmSection, "检测高", out planningMaxYValueText, OnDecreasePlanningMaxYClicked, OnIncreasePlanningMaxYClicked);
        CreateToggleRow(algorithmSection, "对角搜索", out diagonalPlanningToggleButton, ToggleDiagonalPlanning);
        CreateToggleRow(algorithmSection, "障碍自动", out obstacleAutoConfigToggleButton, ToggleObstacleAutoConfiguration);
        CreateButtonStripRow(algorithmSection, "规划地图", new[]
        {
            new ButtonAction("适配边界", PrimaryButtonColor, FitPlanningBoundsToScene, 78f),
            new ButtonAction("边界", new Color(0.10f, 0.48f, 0.62f, 0.98f), TogglePlanningBoundsPreview, 56f),
            new ButtonAction("障碍格", SecondaryButtonColor, TogglePlanningBlockedCellPreview, 64f)
        });
        CreateStepperRow(algorithmSection, "RL案例", out rlCaseValueText, OnPreviousRLCaseClicked, OnNextRLCaseClicked, 176f);
        CreateButtonStripRow(algorithmSection, "RL地图", new[]
        {
            new ButtonAction("训练场景", new Color(0.10f, 0.46f, 0.58f, 0.98f), LoadRLTrainingScene, 78f),
            new ButtonAction("小地图", new Color(0.12f, 0.48f, 0.68f, 0.98f), ApplyRLTrainingMapPreset, 60f),
            new ButtonAction("导出地图", PrimaryButtonColor, ExportRLMapForFirstTask, 78f),
            new ButtonAction("验路径", SecondaryButtonColor, ValidateRLPathResult, 60f)
        });
        CreateButtonStripRow(algorithmSection, "RL训练", new[]
        {
            new ButtonAction("训练当前", PrimaryButtonColor, TrainSelectedRLCase, 78f),
            new ButtonAction("训练并显示", PositiveButtonColor, TrainSelectedRLCaseAndImport, 96f)
        });
        CreateButtonStripRow(algorithmSection, "RL运行", new[]
        {
            new ButtonAction("导入显示", new Color(0.10f, 0.50f, 0.68f, 0.98f), ImportAndShowRLPathForFirstTask, 82f),
            new ButtonAction("开始", PositiveButtonColor, StartSimulationFromPanel, 56f),
            new ButtonAction("暂停", SecondaryButtonColor, PauseSimulationFromPanel, 56f),
            new ButtonAction("重置", NegativeButtonColor, ResetSimulationFromPanel, 56f)
        });

        RectTransform schedulingSection = CreateSectionCard(content, "调度结果", "显示任务分配队列和多无人机执行进度。");
        CreateToggleRow(schedulingSection, "队列可视", out taskQueueVisualizationToggleButton, ToggleTaskQueueVisualization);
        CreateInfoCard(schedulingSection, "SchedulingResult", out schedulingResultText, out schedulingResultLayoutElement, 168f);

        RectTransform evaluationSection = CreateSectionCard(content, "算法评估", "记录当前路径规划与调度组合的运行指标。");
        CreateInfoCard(evaluationSection, "AlgorithmEvaluation", out evaluationText, out evaluationLayoutElement, EvaluationCardMinHeight);
        CreateButtonStripRow(evaluationSection, "对比记录", new[]
        {
            new ButtonAction("记录本轮", PrimaryButtonColor, RecordEvaluationSnapshot, 76f),
            new ButtonAction("清空记录", SecondaryButtonColor, ClearEvaluationHistory, 76f)
        });

        RectTransform visualizationSection = CreateSectionCard(content, "算法过程演示", "播放路径规划搜索过程，观察不同算法的扩展顺序、候选路径和回溯方式。");
        CreateStepperRow(visualizationSection, "无人机", out visualizationSelectedDroneValueText, OnPreviousVisualizationDroneClicked, OnNextVisualizationDroneClicked, 168f);
        CreateStepperRow(visualizationSection, "模式", out visualizationModeValueText, OnPreviousVisualizationModeClicked, OnNextVisualizationModeClicked, 168f);
        CreateStepperRow(visualizationSection, "速度", out visualizationSpeedValueText, OnDecreaseVisualizationSpeedClicked, OnIncreaseVisualizationSpeedClicked, 108f);
        CreateToggleRow(visualizationSection, "建筑半透", out visualizationObstacleTransparencyToggleButton, ToggleVisualizationObstacleTransparency);
        CreateButtonStripRow(visualizationSection, "播放控制", new[]
        {
            new ButtonAction("播放/继续", PrimaryButtonColor, PlayVisualization, 82f),
            new ButtonAction("暂停", SecondaryButtonColor, PauseVisualization, 56f),
            new ButtonAction("单步", new Color(0.10f, 0.48f, 0.62f, 0.98f), StepVisualization, 56f),
            new ButtonAction("重置", new Color(0.24f, 0.34f, 0.42f, 0.98f), ResetVisualizationPlayback, 56f)
        }, out visualizationPlayButton, out visualizationPauseButton, out visualizationStepButton, out visualizationResetButton);
        CreateInfoCard(visualizationSection, "VisualizationStatus", out visualizationStatusText, out _, 128f);
        CreateInfoCard(visualizationSection, "VisualizationDescription", out visualizationDescriptionText, out _, 92f);
        CreateInfoCard(visualizationSection, "VisualizationLegend", out visualizationLegendText, out _, 168f);

        RectTransform fleetSection = CreateSectionCard(content, "机群", "数量、速度和运行倍率。");
        CreateStepperRow(fleetSection, "数量", out droneCountValueText, OnDecreaseDroneCountClicked, OnIncreaseDroneCountClicked);
        CreateStepperRow(fleetSection, "速度", out droneSpeedValueText, OnDecreaseDroneSpeedClicked, OnIncreaseDroneSpeedClicked);
        CreateStepperRow(fleetSection, "倍速", out timeScaleValueText, OnDecreaseTimeScaleClicked, OnIncreaseTimeScaleClicked);
        CreateButtonStripRow(fleetSection, "应用到机群", new[]
        {
            new ButtonAction("同步", SecondaryButtonColor, SyncAndApplyToCurrentFleet, 64f),
            new ButtonAction("重建", PrimaryButtonColor, RebuildFleet, 64f)
        });

        RectTransform displaySection = CreateSectionCard(content, "显示与镜头", "路径显示和视角切换。");
        CreateToggleRow(displaySection, "规划线", out plannedPathToggleButton, TogglePlannedPath);
        CreateToggleRow(displaySection, "航迹", out trailToggleButton, ToggleTrailPath);
        CreateButtonStripRow(displaySection, "视角操作", new[]
        {
            new ButtonAction("总览", SecondaryButtonColor, SwitchToOverviewCamera, 52f),
            new ButtonAction("跟随", PrimaryButtonColor, SwitchToFollowCamera, 52f),
            new ButtonAction("2D俯视", new Color(0.10f, 0.48f, 0.62f, 0.98f), SwitchToTopDownCamera, 64f),
            new ButtonAction("下一架", PrimaryButtonColor, FocusNextDrone, 60f)
        });
        CreateStepperRow(displaySection, "跟随高", out followHeightValueText, OnDecreaseFollowHeightClicked, OnIncreaseFollowHeightClicked);
        CreateStepperRow(displaySection, "跟随距", out followDistanceValueText, OnDecreaseFollowDistanceClicked, OnIncreaseFollowDistanceClicked);

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scrollRect.verticalNormalizedPosition = 1f;
    }
}
