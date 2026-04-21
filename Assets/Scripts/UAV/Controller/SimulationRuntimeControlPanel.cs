using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimulationRuntimeControlPanel : MonoBehaviour
{
    [Header("References")]
    public SimulationManager simulationManager;
    public DroneManager droneManager;
    public CameraManager cameraManager;
    public SimulationResultExporter resultExporter;
    public BatchExperimentRunner batchExperimentRunner;
    public DroneSpawnPointUIManager spawnPointManager;

    [Header("Panel Layout")]
    public Vector2 expandedSize = new Vector2(356f, 460f);
    public Vector2 collapsedSize = new Vector2(356f, 112f);
    public Vector2 anchoredPosition = new Vector2(-36f, -182f);
    public bool startExpanded = true;

    private const string PanelRootName = "__RuntimeControlPanel";
    private const int MinDroneCount = 1;
    private const int MaxDroneCount = 12;
    private const float MinDroneSpeed = 1f;
    private const float MaxDroneSpeed = 20f;
    private const float DroneSpeedStep = 0.5f;
    private const float MinPlanningGridCellSize = 0.5f;
    private const float MaxPlanningGridCellSize = 8f;
    private const float PlanningGridCellSizeStep = 0.5f;
    private const float MinPlanningBoundary = -200f;
    private const float MaxPlanningBoundary = 200f;
    private const float PlanningBoundaryStep = 2f;
    private const float MinPlanningHeight = -20f;
    private const float MaxPlanningHeight = 120f;
    private const float PlanningHeightStep = 1f;
    private const float MinTimeScale = 0.25f;
    private const float MaxTimeScale = 3f;
    private const float TimeScaleStep = 0.25f;
    private const float MinFollowHeight = 2f;
    private const float MaxFollowHeight = 12f;
    private const float FollowHeightStep = 0.5f;
    private const float MinFollowDistance = 4f;
    private const float MaxFollowDistance = 20f;
    private const float FollowDistanceStep = 1f;
    private const int MinBatchRunCount = 1;
    private const int MaxBatchRunCount = 50;
    private const float StatsCardMinHeight = 168f;
    private const float StatsCardVerticalPadding = 18f;

    private static readonly Color PanelColor = new Color(0.03f, 0.09f, 0.15f, 0.94f);
    private static readonly Color SectionColor = new Color(0.06f, 0.15f, 0.22f, 0.92f);
    private static readonly Color RowColor = new Color(0.10f, 0.18f, 0.26f, 0.96f);
    private static readonly Color AccentColor = new Color(0.16f, 0.78f, 0.82f, 1f);
    private static readonly Color PrimaryTextColor = new Color(0.94f, 0.98f, 1f, 1f);
    private static readonly Color SecondaryTextColor = new Color(0.68f, 0.81f, 0.88f, 1f);
    private static readonly Color PrimaryButtonColor = new Color(0.11f, 0.54f, 0.72f, 0.98f);
    private static readonly Color SecondaryButtonColor = new Color(0.18f, 0.28f, 0.36f, 0.98f);
    private static readonly Color PositiveButtonColor = new Color(0.08f, 0.58f, 0.42f, 0.98f);
    private static readonly Color NegativeButtonColor = new Color(0.37f, 0.42f, 0.48f, 0.98f);
    private static readonly Color QuitButtonColor = new Color(0.62f, 0.24f, 0.24f, 0.98f);

    private RectTransform panelRoot;
    private RectTransform bodyRoot;
    private RectTransform footerRoot;
    private RectTransform scrollContentRoot;
    private TMP_FontAsset runtimeFont;
    private TMP_Text schedulerValueText;
    private TMP_Text plannerValueText;
    private TMP_Text droneCountValueText;
    private TMP_Text droneSpeedValueText;
    private TMP_Text timeScaleValueText;
    private TMP_Text planningGridValueText;
    private TMP_Text planningMinXValueText;
    private TMP_Text planningMaxXValueText;
    private TMP_Text planningMinZValueText;
    private TMP_Text planningMaxZValueText;
    private TMP_Text planningMinYValueText;
    private TMP_Text planningMaxYValueText;
    private TMP_Text followHeightValueText;
    private TMP_Text followDistanceValueText;
    private TMP_Text exportDirectoryStatusText;
    private TMP_Text batchRunCountValueText;
    private TMP_Text batchStatusText;
    private TMP_Text experimentGroupValueText;
    private TMP_Text experimentPresetValueText;
    private TMP_Text experimentPresetSummaryText;
    private TMP_Text summaryText;
    private TMP_Text statsText;
    private TMP_Text footerText;
    private TMP_Text expandButtonText;
    private TMP_InputField exportDirectoryInputField;
    private LayoutElement statsCardLayoutElement;
    private Button plannedPathToggleButton;
    private Button trailToggleButton;
    private Button diagonalPlanningToggleButton;
    private Button obstacleAutoConfigToggleButton;
    private bool isExpanded;
    private float nextSummaryRefreshTime;

    private SchedulerAlgorithmType[] schedulerOptions;
    private PathPlannerType[] plannerOptions;
    private int schedulerIndex;
    private int plannerIndex;
    private int configuredDroneCount = 4;
    private float configuredDroneSpeed = 5f;
    private float configuredTimeScale = 1f;
    private float configuredPlanningGridCellSize = 2f;
    private float configuredPlanningMinX = -20f;
    private float configuredPlanningMaxX = 80f;
    private float configuredPlanningMinZ = -20f;
    private float configuredPlanningMaxZ = 80f;
    private float configuredPlanningMinY = 0f;
    private float configuredPlanningMaxY = 10f;
    private float configuredFollowHeight = 5f;
    private float configuredFollowDistance = 10f;
    private bool showPlannedPath = true;
    private bool showTrail = true;
    private bool configuredAllowDiagonalPlanning = true;
    private bool configuredAutoConfigureObstacles = true;
    private int configuredBatchRunCount = 5;
    private string transientMessage = "面板已就绪";
    private readonly List<ExperimentPresetGroupInfo> experimentPresetGroups = new List<ExperimentPresetGroupInfo>();
    private int selectedExperimentGroupIndex;
    private int selectedExperimentPresetIndex;

    private IEnumerator Start()
    {
        yield return null;

        isExpanded = startExpanded;
        CacheReferences();
        BuildPanelIfNeeded();
        SyncFromSystems();
        RefreshAllLabels();
        RefreshSummary();
        ApplyExpandState();
    }

    private void Update()
    {
        if (panelRoot == null)
        {
            CacheReferences();
            BuildPanelIfNeeded();
            SyncFromSystems();
            RefreshAllLabels();
            RefreshSummary();
            ApplyExpandState();
            return;
        }

        HandleRuntimeShortcuts();

        if (Time.unscaledTime >= nextSummaryRefreshTime)
        {
            nextSummaryRefreshTime = Time.unscaledTime + 0.35f;
            RefreshBatchStatus();
            RefreshSummary();
        }
    }

    private void CacheReferences()
    {
        if (simulationManager == null)
        {
            simulationManager = GetComponent<SimulationManager>();
            if (simulationManager == null)
            {
                simulationManager = FindObjectOfType<SimulationManager>();
            }
        }

        if (droneManager == null)
        {
            droneManager = simulationManager != null ? simulationManager.droneManager : null;
            if (droneManager == null)
            {
                droneManager = FindObjectOfType<DroneManager>();
            }
        }

        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<CameraManager>();
        }

        if (resultExporter == null)
        {
            resultExporter = simulationManager != null ? simulationManager.resultExporter : null;
            if (resultExporter == null)
            {
                resultExporter = FindObjectOfType<SimulationResultExporter>();
            }
        }

        if (batchExperimentRunner == null)
        {
            batchExperimentRunner = simulationManager != null ? simulationManager.batchExperimentRunner : null;
            if (batchExperimentRunner == null)
            {
                batchExperimentRunner = FindObjectOfType<BatchExperimentRunner>();
            }
        }

        if (spawnPointManager == null)
        {
            spawnPointManager = FindObjectOfType<DroneSpawnPointUIManager>();
        }

        if (runtimeFont == null)
        {
            TMP_Text referenceText = simulationManager != null ? simulationManager.statusText : null;
            if (referenceText == null)
            {
                referenceText = FindObjectOfType<TextMeshProUGUI>();
            }

            if (referenceText != null)
            {
                runtimeFont = referenceText.font;
            }
        }

        schedulerOptions = (SchedulerAlgorithmType[])Enum.GetValues(typeof(SchedulerAlgorithmType));
        plannerOptions = (PathPlannerType[])Enum.GetValues(typeof(PathPlannerType));
    }

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

        panelRoot.anchorMin = new Vector2(1f, 1f);
        panelRoot.anchorMax = new Vector2(1f, 1f);
        panelRoot.pivot = new Vector2(1f, 1f);
        panelRoot.anchoredPosition = anchoredPosition;
        panelRoot.sizeDelta = isExpanded ? expandedSize : collapsedSize;
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

        RectTransform summaryCard = CreatePanelArea("Summary", panelRoot, new Vector2(12f, -50f), new Vector2(-12f, -108f));
        ConfigureSection(summaryCard, SectionColor);
        summaryText = CreateText("SummaryText", summaryCard, string.Empty, 14f, PrimaryTextColor, FontStyles.Normal);
        summaryText.enableWordWrapping = true;
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
        viewport.offsetMin = new Vector2(8f, 8f);
        viewport.offsetMax = new Vector2(-12f, -8f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        ConfigureImageGraphic(viewportImage);
        viewportImage.color = new Color(0f, 0f, 0f, 0.02f);
        viewportImage.maskable = true;
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);
        scrollContentRoot = content;

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperCenter;
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
        scrollRect.scrollSensitivity = 28f;
        scrollRect.inertia = true;

        RectTransform scrollbarRect = new GameObject("Scrollbar", typeof(RectTransform)).GetComponent<RectTransform>();
        scrollbarRect.SetParent(parent, false);
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 1f);
        scrollbarRect.offsetMin = new Vector2(-10f, 8f);
        scrollbarRect.offsetMax = new Vector2(-6f, -8f);

        Image scrollbarTrack = scrollbarRect.gameObject.AddComponent<Image>();
        ConfigureImageGraphic(scrollbarTrack);
        scrollbarTrack.color = new Color(0.10f, 0.18f, 0.24f, 0.72f);

        Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        RectTransform slidingArea = new GameObject("SlidingArea", typeof(RectTransform)).GetComponent<RectTransform>();
        slidingArea.SetParent(scrollbarRect, false);
        slidingArea.anchorMin = Vector2.zero;
        slidingArea.anchorMax = Vector2.one;
        slidingArea.offsetMin = new Vector2(1f, 4f);
        slidingArea.offsetMax = new Vector2(-1f, -4f);

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

        CreateSectionLabel(content, "统计");
        CreateInfoCard(content, "Stats", out statsText, out statsCardLayoutElement, StatsCardMinHeight);

        CreateSectionLabel(content, "实验中心");
        CreateStepperRow(content, "分组", out experimentGroupValueText, OnPreviousExperimentGroupClicked, OnNextExperimentGroupClicked, 124f);
        CreateStepperRow(content, "预设", out experimentPresetValueText, OnPreviousExperimentPresetClicked, OnNextExperimentPresetClicked, 148f);
        CreateButtonStripRow(content, "矩阵", new[]
        {
            new ButtonAction("调度", new Color(0.14f, 0.44f, 0.70f, 0.98f), SelectSchedulingExperimentGroup, 48f),
            new ButtonAction("规划", new Color(0.16f, 0.48f, 0.66f, 0.98f), SelectPlanningExperimentGroup, 48f),
            new ButtonAction("机群", new Color(0.18f, 0.40f, 0.62f, 0.98f), SelectScalingExperimentGroup, 48f),
            new ButtonAction("密度", new Color(0.12f, 0.36f, 0.54f, 0.98f), SelectDensityExperimentGroup, 48f)
        });
        CreateInfoCard(content, "ExperimentPresetSummary", out experimentPresetSummaryText, out _, 104f);
        CreateButtonStripRow(content, "实验", new[]
        {
            new ButtonAction("应用预设", SecondaryButtonColor, ApplySelectedExperimentPreset, 72f),
            new ButtonAction("预设批量", PrimaryButtonColor, StartSelectedPresetBatch, 72f),
            new ButtonAction("当前批量", new Color(0.09f, 0.46f, 0.56f, 0.98f), StartBatchExperiments, 72f)
        });

        CreateSectionLabel(content, "结果");
        CreateInfoCard(content, "ExportDirectory", out exportDirectoryStatusText, out _, 92f);
        CreateInputButtonRow(content, "目录", out exportDirectoryInputField, new[]
        {
            new ButtonAction("选择", new Color(0.13f, 0.44f, 0.64f, 0.98f), BrowseCustomExportDirectory, 48f),
            new ButtonAction("新会话", new Color(0.10f, 0.46f, 0.58f, 0.98f), StartNewExportSession, 60f),
            new ButtonAction("应用", PrimaryButtonColor, ApplyCustomExportDirectory, 48f),
            new ButtonAction("默认", SecondaryButtonColor, ResetExportDirectoryToDefault, 48f)
        });
        CreateButtonStripRow(content, "导出", new[]
        {
            new ButtonAction("导出CSV", PrimaryButtonColor, ExportCurrentResultToCsv, 68f),
            new ButtonAction("导出JSON", new Color(0.08f, 0.48f, 0.62f, 0.98f), ExportCurrentResultToJson, 74f)
        });
        CreateInfoCard(content, "BatchStatus", out batchStatusText, out _, 64f);
        CreateStepperRow(content, "批次数", out batchRunCountValueText, OnDecreaseBatchRunCountClicked, OnIncreaseBatchRunCountClicked);
        CreateButtonStripRow(content, "批量", new[]
        {
            new ButtonAction("开始", PrimaryButtonColor, StartBatchExperiments, 56f),
            new ButtonAction("停止", SecondaryButtonColor, StopBatchExperiments, 56f)
        });

        CreateSectionLabel(content, "起飞点");
        CreateButtonStripRow(content, "编辑", new[]
        {
            new ButtonAction("新增", PrimaryButtonColor, ToggleSpawnPointPlacement, 48f),
            new ButtonAction("移动", new Color(0.14f, 0.50f, 0.78f, 0.98f), ToggleSpawnPointMove, 48f),
            new ButtonAction("删除", new Color(0.78f, 0.48f, 0.14f, 0.98f), ToggleSpawnPointDeletion, 48f)
        });
        CreateButtonStripRow(content, "操作", new[]
        {
            new ButtonAction("清空", SecondaryButtonColor, ClearSpawnPoints, 56f)
        });

        CreateSectionLabel(content, "算法");
        CreateStepperRow(content, "调度", out schedulerValueText, OnPreviousSchedulerClicked, OnNextSchedulerClicked);
        CreateStepperRow(content, "路径", out plannerValueText, OnPreviousPlannerClicked, OnNextPlannerClicked);

        CreateSectionLabel(content, "规划");
        CreateStepperRow(content, "网格", out planningGridValueText, OnDecreasePlanningGridClicked, OnIncreasePlanningGridClicked);
        CreateStepperRow(content, "X最小", out planningMinXValueText, OnDecreasePlanningMinXClicked, OnIncreasePlanningMinXClicked);
        CreateStepperRow(content, "X最大", out planningMaxXValueText, OnDecreasePlanningMaxXClicked, OnIncreasePlanningMaxXClicked);
        CreateStepperRow(content, "Z最小", out planningMinZValueText, OnDecreasePlanningMinZClicked, OnIncreasePlanningMinZClicked);
        CreateStepperRow(content, "Z最大", out planningMaxZValueText, OnDecreasePlanningMaxZClicked, OnIncreasePlanningMaxZClicked);
        CreateStepperRow(content, "高最小", out planningMinYValueText, OnDecreasePlanningMinYClicked, OnIncreasePlanningMinYClicked);
        CreateStepperRow(content, "高最大", out planningMaxYValueText, OnDecreasePlanningMaxYClicked, OnIncreasePlanningMaxYClicked);
        CreateToggleRow(content, "对角", out diagonalPlanningToggleButton, ToggleDiagonalPlanning);
        CreateToggleRow(content, "障碍", out obstacleAutoConfigToggleButton, ToggleObstacleAutoConfiguration);

        CreateSectionLabel(content, "机群");
        CreateStepperRow(content, "数量", out droneCountValueText, OnDecreaseDroneCountClicked, OnIncreaseDroneCountClicked);
        CreateStepperRow(content, "速度", out droneSpeedValueText, OnDecreaseDroneSpeedClicked, OnIncreaseDroneSpeedClicked);
        CreateStepperRow(content, "倍速", out timeScaleValueText, OnDecreaseTimeScaleClicked, OnIncreaseTimeScaleClicked);
        CreateButtonStripRow(content, "应用", new[]
        {
            new ButtonAction("同步", SecondaryButtonColor, SyncAndApplyToCurrentFleet, 64f),
            new ButtonAction("重建", PrimaryButtonColor, RebuildFleet, 64f)
        });

        CreateSectionLabel(content, "显示");
        CreateToggleRow(content, "规划线", out plannedPathToggleButton, TogglePlannedPath);
        CreateToggleRow(content, "航迹", out trailToggleButton, ToggleTrailPath);

        CreateSectionLabel(content, "镜头");
        CreateButtonStripRow(content, "相机", new[]
        {
            new ButtonAction("总览", SecondaryButtonColor, SwitchToOverviewCamera, 52f),
            new ButtonAction("跟随", PrimaryButtonColor, SwitchToFollowCamera, 52f),
            new ButtonAction("2D俯视", new Color(0.10f, 0.48f, 0.62f, 0.98f), SwitchToTopDownCamera, 64f),
            new ButtonAction("下一架", PrimaryButtonColor, FocusNextDrone, 60f)
        });
        CreateStepperRow(content, "跟随高", out followHeightValueText, OnDecreaseFollowHeightClicked, OnIncreaseFollowHeightClicked);
        CreateStepperRow(content, "跟随距", out followDistanceValueText, OnDecreaseFollowDistanceClicked, OnIncreaseFollowDistanceClicked);

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private void SyncFromSystems()
    {
        CacheReferences();

        if (droneManager != null)
        {
            schedulerIndex = Array.IndexOf(schedulerOptions, droneManager.schedulerAlgorithm);
            plannerIndex = Array.IndexOf(plannerOptions, droneManager.pathPlannerType);
            configuredDroneCount = Mathf.Clamp(droneManager.droneCount, MinDroneCount, MaxDroneCount);
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

        if (batchRunCountValueText != null)
        {
            batchRunCountValueText.text = configuredBatchRunCount.ToString();
        }

        RefreshExperimentCenterLabels();
        UpdateToggleButton(plannedPathToggleButton, showPlannedPath);
        UpdateToggleButton(trailToggleButton, showTrail);
        UpdateToggleButton(diagonalPlanningToggleButton, configuredAllowDiagonalPlanning);
        UpdateToggleButton(obstacleAutoConfigToggleButton, configuredAutoConfigureObstacles);

        if (expandButtonText != null)
        {
            expandButtonText.text = isExpanded ? "收起" : "展开";
        }

        if (footerText != null)
        {
            footerText.text = $"{transientMessage}  |  F5开始/继续 F6暂停 F7重置 F8重建 F10退出 Ctrl+Shift+C/J/B/X 导出/批量";
        }

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
        TaskPoint[] taskPoints = FindObjectsOfType<TaskPoint>();
        int totalTaskCount = taskPoints.Length;
        int completedTaskCount = CountTasksByState(taskPoints, TaskState.Completed);
        int waitingDroneCount = CountDronesByState(DroneState.Waiting);
        int totalConflictCount = CountTotalConflictEvents();
        int buildingWarningCount = droneManager != null ? droneManager.GetBuildingWarningCount() : 0;
        int spawnPointCount = spawnPointManager != null ? spawnPointManager.GetSpawnPointCount() : 0;
        string simulationState = simulationManager != null ? FormatSimulationState(simulationManager.currentState) : "未知";
        string elapsedTime = simulationManager != null ? FormatDuration(simulationManager.ElapsedSimulationTime) : "--:--";
        string cameraMode = "未连接";
        string cameraTarget = "-";

        if (cameraManager != null)
        {
            cameraMode = cameraManager.GetCurrentModeDisplayName();
            cameraTarget = cameraManager.targetDrone != null ? cameraManager.targetDrone.name : "-";
        }

        summaryText.text =
            $"状态 {simulationState}  用时 {elapsedTime}  任务 {completedTaskCount}/{totalTaskCount}\n" +
            $"镜头 {cameraMode}  目标 {cameraTarget}  等待 {waitingDroneCount}  冲突 {totalConflictCount}  建筑告警 {buildingWarningCount}  机群 {droneCount}  起点 {spawnPointCount}";

        RefreshStats(taskPoints, cameraTarget);
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

        if (simulationManager != null && Input.GetKeyDown(KeyCode.F6))
        {
            simulationManager.OnPauseClicked();
            transientMessage = "已通过快捷键触发暂停";
            RefreshAllLabels();
            RefreshSummary();
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

        panelRoot.sizeDelta = isExpanded ? expandedSize : collapsedSize;

        if (bodyRoot != null)
        {
            bodyRoot.gameObject.SetActive(isExpanded);
        }

        if (footerRoot != null)
        {
            footerRoot.gameObject.SetActive(isExpanded);
        }
    }

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

    private void OnDecreaseDroneCountClicked()
    {
        configuredDroneCount = Mathf.Clamp(configuredDroneCount - 1, MinDroneCount, MaxDroneCount);
        transientMessage = "点重建后按当前数量重新生成机群";
        RefreshAllLabels();
    }

    private void OnIncreaseDroneCountClicked()
    {
        configuredDroneCount = Mathf.Clamp(configuredDroneCount + 1, MinDroneCount, MaxDroneCount);
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
            $"规划 网格{configuredPlanningGridCellSize:0.0}m 边界X[{configuredPlanningMinX:0},{configuredPlanningMaxX:0}] Z[{configuredPlanningMinZ:0},{configuredPlanningMaxZ:0}] 高[{configuredPlanningMinY:0},{configuredPlanningMaxY:0}]";
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
        droneManager.ApplyDroneSpeedToAll(configuredDroneSpeed);
        droneManager.ApplyPathVisibilityToAll(showPlannedPath, showTrail);

        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<CameraManager>();
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

    private void EnsureExperimentPresetCatalogLoaded()
    {
        if (experimentPresetGroups.Count > 0)
        {
            ClampExperimentSelection();
            return;
        }

        ExperimentPreset[] presets = Resources.LoadAll<ExperimentPreset>(ExperimentPresetCatalog.ResourcesRoot);
        experimentPresetGroups.Clear();
        experimentPresetGroups.AddRange(ExperimentPresetCatalog.Build(presets));
        ClampExperimentSelection();
    }

    private void SyncExperimentSelectionFromSystems()
    {
        ExperimentPreset activePreset = ResolveConfiguredExperimentPreset();
        if (activePreset == null)
        {
            ClampExperimentSelection();
            return;
        }

        SelectExperimentPreset(activePreset);
    }

    private ExperimentPreset ResolveConfiguredExperimentPreset()
    {
        if (batchExperimentRunner == null)
        {
            return null;
        }

        if (batchExperimentRunner.ActivePreset != null)
        {
            return batchExperimentRunner.ActivePreset;
        }

        if (!string.IsNullOrWhiteSpace(batchExperimentRunner.experimentPresetResourcePath))
        {
            return Resources.Load<ExperimentPreset>(batchExperimentRunner.experimentPresetResourcePath);
        }

        return null;
    }

    private void SelectExperimentPreset(ExperimentPreset preset)
    {
        if (preset == null)
        {
            ClampExperimentSelection();
            return;
        }

        for (int groupIndex = 0; groupIndex < experimentPresetGroups.Count; groupIndex++)
        {
            ExperimentPresetGroupInfo group = experimentPresetGroups[groupIndex];
            for (int presetIndex = 0; presetIndex < group.Presets.Count; presetIndex++)
            {
                if (group.Presets[presetIndex] != preset)
                {
                    continue;
                }

                selectedExperimentGroupIndex = groupIndex;
                selectedExperimentPresetIndex = presetIndex;
                return;
            }
        }

        ClampExperimentSelection();
    }

    private void ClampExperimentSelection()
    {
        if (experimentPresetGroups.Count == 0)
        {
            selectedExperimentGroupIndex = 0;
            selectedExperimentPresetIndex = 0;
            return;
        }

        selectedExperimentGroupIndex = Mathf.Clamp(selectedExperimentGroupIndex, 0, experimentPresetGroups.Count - 1);
        ExperimentPresetGroupInfo selectedGroup = experimentPresetGroups[selectedExperimentGroupIndex];
        if (selectedGroup.Presets.Count == 0)
        {
            selectedExperimentPresetIndex = 0;
            return;
        }

        selectedExperimentPresetIndex = Mathf.Clamp(selectedExperimentPresetIndex, 0, selectedGroup.Presets.Count - 1);
    }

    private ExperimentPresetGroupInfo GetSelectedExperimentGroup()
    {
        if (experimentPresetGroups.Count == 0)
        {
            return null;
        }

        selectedExperimentGroupIndex = Mathf.Clamp(selectedExperimentGroupIndex, 0, experimentPresetGroups.Count - 1);
        return experimentPresetGroups[selectedExperimentGroupIndex];
    }

    private ExperimentPreset GetSelectedExperimentPreset()
    {
        ExperimentPresetGroupInfo selectedGroup = GetSelectedExperimentGroup();
        if (selectedGroup == null || selectedGroup.Presets.Count == 0)
        {
            return null;
        }

        selectedExperimentPresetIndex = Mathf.Clamp(selectedExperimentPresetIndex, 0, selectedGroup.Presets.Count - 1);
        return selectedGroup.Presets[selectedExperimentPresetIndex];
    }

    private string GetSelectedExperimentGroupDisplayName()
    {
        ExperimentPresetGroupInfo selectedGroup = GetSelectedExperimentGroup();
        return selectedGroup != null ? selectedGroup.DisplayName : "No Presets";
    }

    private void LoadPresetIntoRuntimeConfiguration(ExperimentPreset preset)
    {
        if (preset == null)
        {
            return;
        }

        schedulerIndex = IndexOfSchedulerOption(preset.scheduler);
        plannerIndex = IndexOfPlannerOption(preset.planner);
        configuredDroneCount = Mathf.Clamp(preset.droneCount, MinDroneCount, MaxDroneCount);
        configuredBatchRunCount = Mathf.Clamp(preset.batchRuns, MinBatchRunCount, MaxBatchRunCount);
        configuredPlanningMinX = preset.planningWorldMin.x;
        configuredPlanningMaxX = preset.planningWorldMax.x;
        configuredPlanningMinZ = preset.planningWorldMin.z;
        configuredPlanningMaxZ = preset.planningWorldMax.z;
        configuredPlanningMinY = preset.planningWorldMin.y;
        configuredPlanningMaxY = preset.planningWorldMax.y;
        NormalizePlanningBounds();
    }

    private void RefreshExperimentCenterLabels()
    {
        if (experimentGroupValueText != null)
        {
            experimentGroupValueText.text = GetCompactExperimentGroupLabel();
        }

        if (experimentPresetValueText != null)
        {
            experimentPresetValueText.text = GetCompactExperimentPresetLabel();
        }

        if (experimentPresetSummaryText == null)
        {
            return;
        }

        ExperimentPreset selectedPreset = GetSelectedExperimentPreset();
        if (selectedPreset == null)
        {
            experimentPresetSummaryText.text =
                "未找到 ExperimentPreset 资源。\n" +
                "请先执行 Tools/KY UAV/Bootstrap Delivery Assets。";
            return;
        }

        string groupName = GetSelectedExperimentGroupDisplayName();
        string schedulerName = FormatSchedulerName(selectedPreset.scheduler);
        string plannerName = FormatPlannerName(selectedPreset.planner);
        string batchSource = batchExperimentRunner != null && batchExperimentRunner.ActivePreset == selectedPreset
            ? "预设批量"
            : "可应用到当前运行时";

        StringBuilder builder = new StringBuilder(256);
        builder.Append("分组: ").Append(groupName).AppendLine();
        builder.Append("预设: ").Append(ExperimentPresetCatalog.GetPresetDisplayName(selectedPreset)).AppendLine();
        builder.Append("调度 / 规划: ").Append(schedulerName).Append(" / ").Append(plannerName).AppendLine();
        builder.Append("机群 / 轮次: ").Append(selectedPreset.droneCount).Append(" / ").Append(selectedPreset.batchRuns).AppendLine();
        builder.Append("边界 X[").Append(selectedPreset.planningWorldMin.x.ToString("0"))
            .Append(',').Append(selectedPreset.planningWorldMax.x.ToString("0"))
            .Append("] Z[").Append(selectedPreset.planningWorldMin.z.ToString("0"))
            .Append(',').Append(selectedPreset.planningWorldMax.z.ToString("0"))
            .Append("] Y[").Append(selectedPreset.planningWorldMin.y.ToString("0"))
            .Append(',').Append(selectedPreset.planningWorldMax.y.ToString("0"))
            .Append(']').AppendLine();
        builder.Append("备注: ").Append(string.IsNullOrWhiteSpace(selectedPreset.notePrefix) ? "-" : selectedPreset.notePrefix)
            .Append("    模式: ").Append(batchSource);
        experimentPresetSummaryText.text = builder.ToString();
    }

    private string GetCompactExperimentGroupLabel()
    {
        ExperimentPresetGroupInfo selectedGroup = GetSelectedExperimentGroup();
        if (selectedGroup == null)
        {
            return "--";
        }

        switch (selectedGroup.GroupKey)
        {
            case "scheduling":
                return "调度";
            case "planning":
                return "规划";
            case "scaling":
                return "机群";
            case "density":
                return "密度";
            default:
                return selectedGroup.DisplayName;
        }
    }

    private string GetCompactExperimentPresetLabel()
    {
        ExperimentPreset selectedPreset = GetSelectedExperimentPreset();
        if (selectedPreset == null)
        {
            return "--";
        }

        return ExperimentPresetCatalog.GetPresetShortLabel(selectedPreset);
    }

    private int IndexOfSchedulerOption(SchedulerAlgorithmType algorithmType)
    {
        int index = Array.IndexOf(schedulerOptions, algorithmType);
        return index >= 0 ? index : 0;
    }

    private int IndexOfPlannerOption(PathPlannerType plannerType)
    {
        int index = Array.IndexOf(plannerOptions, plannerType);
        return index >= 0 ? index : 0;
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

    private void ExportCurrentResultToCsv()
    {
        if (resultExporter == null)
        {
            transientMessage = "未找到结果导出器";
            RefreshAllLabels();
            return;
        }

        bool exported = resultExporter.ExportCurrentResult();
        transientMessage = exported
            ? resultExporter.LastExportMessage
            : resultExporter.LastExportMessage;
        RefreshAllLabels();
    }

    private void ExportCurrentResultToJson()
    {
        if (resultExporter == null)
        {
            transientMessage = "未找到结果导出器";
            RefreshAllLabels();
            return;
        }

        bool exported = resultExporter.ExportCurrentResultAsJson();
        transientMessage = exported
            ? resultExporter.LastExportMessage
            : resultExporter.LastExportMessage;
        RefreshAllLabels();
    }

    private void ApplyCustomExportDirectory()
    {
        if (resultExporter == null)
        {
            transientMessage = "未找到结果导出器";
            RefreshAllLabels();
            return;
        }

        if (exportDirectoryInputField == null)
        {
            transientMessage = "目录输入框未初始化";
            RefreshAllLabels();
            return;
        }

        bool success = resultExporter.SetCustomExportDirectory(exportDirectoryInputField.text, out string message);
        transientMessage = message;
        RefreshExportDirectoryUi(success);
        RefreshAllLabels();
    }

    private void BrowseCustomExportDirectory()
    {
        if (resultExporter == null)
        {
            transientMessage = "未找到结果导出器";
            RefreshAllLabels();
            return;
        }

        string initialDirectory = exportDirectoryInputField != null && !string.IsNullOrWhiteSpace(exportDirectoryInputField.text)
            ? exportDirectoryInputField.text
            : resultExporter.GetExportDirectoryPath();

        if (!TryOpenFolderPicker(initialDirectory, out string selectedDirectory))
        {
            transientMessage = "当前环境未能打开目录选择器，可手动输入路径";
            RefreshAllLabels();
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            transientMessage = "已取消目录选择";
            RefreshAllLabels();
            return;
        }

        if (exportDirectoryInputField != null)
        {
            exportDirectoryInputField.SetTextWithoutNotify(selectedDirectory);
        }

        bool success = resultExporter.SetCustomExportDirectory(selectedDirectory, out string message);
        transientMessage = message;
        RefreshExportDirectoryUi(success);
        RefreshAllLabels();
    }

    private void ResetExportDirectoryToDefault()
    {
        if (resultExporter == null)
        {
            transientMessage = "未找到结果导出器";
            RefreshAllLabels();
            return;
        }

        resultExporter.ResetExportDirectoryToDefault();
        transientMessage = resultExporter.LastExportMessage;
        RefreshExportDirectoryUi(true);
        RefreshAllLabels();
    }

    private void StartNewExportSession()
    {
        if (resultExporter == null)
        {
            transientMessage = "未找到结果导出器";
            RefreshAllLabels();
            return;
        }

        string sessionFolderName = resultExporter.BeginNewArchiveSession();
        transientMessage = $"已切换到新会话：{sessionFolderName}";
        RefreshExportDirectoryUi(false);
        RefreshAllLabels();
    }

    private void OnDecreaseBatchRunCountClicked()
    {
        configuredBatchRunCount = Mathf.Clamp(configuredBatchRunCount - 1, MinBatchRunCount, MaxBatchRunCount);
        if (batchExperimentRunner != null)
        {
            batchExperimentRunner.SetBatchRunCount(configuredBatchRunCount);
        }

        transientMessage = $"批量实验轮数 {configuredBatchRunCount}";
        RefreshAllLabels();
    }

    private void OnIncreaseBatchRunCountClicked()
    {
        configuredBatchRunCount = Mathf.Clamp(configuredBatchRunCount + 1, MinBatchRunCount, MaxBatchRunCount);
        if (batchExperimentRunner != null)
        {
            batchExperimentRunner.SetBatchRunCount(configuredBatchRunCount);
        }

        transientMessage = $"批量实验轮数 {configuredBatchRunCount}";
        RefreshAllLabels();
    }

    private void StartBatchExperiments()
    {
        if (batchExperimentRunner == null)
        {
            transientMessage = "未找到批量实验执行器";
            RefreshAllLabels();
            return;
        }

        batchExperimentRunner.UseCurrentRuntimeConfiguration();
        batchExperimentRunner.SetBatchRunCount(configuredBatchRunCount);
        batchExperimentRunner.StartBatch();
        transientMessage = batchExperimentRunner.LastBatchMessage;
        RefreshBatchStatus();
        RefreshAllLabels();
    }

    private void StopBatchExperiments()
    {
        if (batchExperimentRunner == null)
        {
            transientMessage = "未找到批量实验执行器";
            RefreshAllLabels();
            return;
        }

        batchExperimentRunner.StopBatch();
        transientMessage = batchExperimentRunner.LastBatchMessage;
        RefreshBatchStatus();
        RefreshAllLabels();
    }

    private void OnPreviousExperimentGroupClicked()
    {
        ChangeExperimentGroup(-1);
    }

    private void OnNextExperimentGroupClicked()
    {
        ChangeExperimentGroup(1);
    }

    private void OnPreviousExperimentPresetClicked()
    {
        ChangeExperimentPreset(-1);
    }

    private void OnNextExperimentPresetClicked()
    {
        ChangeExperimentPreset(1);
    }

    private void SelectSchedulingExperimentGroup()
    {
        SelectExperimentGroup("scheduling");
    }

    private void SelectPlanningExperimentGroup()
    {
        SelectExperimentGroup("planning");
    }

    private void SelectScalingExperimentGroup()
    {
        SelectExperimentGroup("scaling");
    }

    private void SelectDensityExperimentGroup()
    {
        SelectExperimentGroup("density");
    }

    private void ChangeExperimentGroup(int delta)
    {
        EnsureExperimentPresetCatalogLoaded();
        if (experimentPresetGroups.Count == 0)
        {
            transientMessage = "未找到实验预设资源";
            RefreshAllLabels();
            return;
        }

        selectedExperimentGroupIndex = WrapIndex(selectedExperimentGroupIndex + delta, experimentPresetGroups.Count);
        selectedExperimentPresetIndex = 0;
        transientMessage = $"实验分组切换为 {GetSelectedExperimentGroupDisplayName()}";
        RefreshAllLabels();
    }

    private void ChangeExperimentPreset(int delta)
    {
        EnsureExperimentPresetCatalogLoaded();
        ExperimentPresetGroupInfo selectedGroup = GetSelectedExperimentGroup();
        if (selectedGroup == null || selectedGroup.Presets.Count == 0)
        {
            transientMessage = "当前分组没有可用预设";
            RefreshAllLabels();
            return;
        }

        selectedExperimentPresetIndex = WrapIndex(selectedExperimentPresetIndex + delta, selectedGroup.Presets.Count);
        transientMessage = $"实验预设切换为 {ExperimentPresetCatalog.GetPresetShortLabel(GetSelectedExperimentPreset())}";
        RefreshAllLabels();
    }

    private void SelectExperimentGroup(string groupKey)
    {
        EnsureExperimentPresetCatalogLoaded();
        string normalizedGroupKey = ExperimentPresetCatalog.NormalizeGroupKey(groupKey);
        for (int i = 0; i < experimentPresetGroups.Count; i++)
        {
            if (!string.Equals(experimentPresetGroups[i].GroupKey, normalizedGroupKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            selectedExperimentGroupIndex = i;
            selectedExperimentPresetIndex = 0;
            transientMessage = $"实验分组切换为 {experimentPresetGroups[i].DisplayName}";
            RefreshAllLabels();
            return;
        }

        transientMessage = $"未找到分组 {groupKey}";
        RefreshAllLabels();
    }

    private void ApplySelectedExperimentPreset()
    {
        ExperimentPreset selectedPreset = GetSelectedExperimentPreset();
        if (selectedPreset == null)
        {
            transientMessage = "未选择实验预设";
            RefreshAllLabels();
            return;
        }

        LoadPresetIntoRuntimeConfiguration(selectedPreset);

        if (batchExperimentRunner != null)
        {
            batchExperimentRunner.UseCurrentRuntimeConfiguration();
            batchExperimentRunner.SetBatchRunCount(configuredBatchRunCount);
        }

        RebuildFleet();
        transientMessage = $"已将预设 {ExperimentPresetCatalog.GetPresetShortLabel(selectedPreset)} 应用到当前机群";
        RefreshAllLabels();
    }

    private void StartSelectedPresetBatch()
    {
        ExperimentPreset selectedPreset = GetSelectedExperimentPreset();
        if (selectedPreset == null)
        {
            transientMessage = "未选择实验预设";
            RefreshAllLabels();
            return;
        }

        if (batchExperimentRunner == null)
        {
            transientMessage = "未找到批量实验执行器";
            RefreshAllLabels();
            return;
        }

        configuredBatchRunCount = Mathf.Clamp(selectedPreset.batchRuns, MinBatchRunCount, MaxBatchRunCount);
        batchExperimentRunner.SetExperimentPreset(selectedPreset);
        batchExperimentRunner.SetBatchRunCount(configuredBatchRunCount);
        if (!string.IsNullOrWhiteSpace(selectedPreset.notePrefix))
        {
            batchExperimentRunner.batchNotePrefix = selectedPreset.notePrefix;
        }

        batchExperimentRunner.StartBatch();
        transientMessage = batchExperimentRunner.LastBatchMessage;
        RefreshBatchStatus();
        RefreshAllLabels();
    }

    private void ToggleSpawnPointPlacement()
    {
        if (spawnPointManager == null)
        {
            transientMessage = "未找到起飞点管理器";
            RefreshAllLabels();
            return;
        }

        spawnPointManager.TogglePlacementMode();
        transientMessage = spawnPointManager.IsPlacementMode
            ? "已进入起飞点放置模式，点击地面放置"
            : "已取消起飞点放置模式";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ToggleSpawnPointDeletion()
    {
        if (spawnPointManager == null)
        {
            transientMessage = "未找到起飞点管理器";
            RefreshAllLabels();
            return;
        }

        spawnPointManager.ToggleDeleteMode();
        transientMessage = spawnPointManager.IsDeleteMode
            ? "已进入起飞点删除模式，点击已有起点删除"
            : "已取消起飞点删除模式";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ToggleSpawnPointMove()
    {
        if (spawnPointManager == null)
        {
            transientMessage = "未找到起飞点管理器";
            RefreshAllLabels();
            return;
        }

        spawnPointManager.ToggleMoveMode();
        transientMessage = spawnPointManager.IsMoveMode
            ? "已进入起飞点移动模式，先点起点再点新位置"
            : "已取消起飞点移动模式";
        RefreshAllLabels();
        RefreshSummary();
    }

    private void ClearSpawnPoints()
    {
        if (spawnPointManager == null)
        {
            transientMessage = "未找到起飞点管理器";
            RefreshAllLabels();
            return;
        }

        spawnPointManager.ClearSpawnPoints();
        transientMessage = "已清空手动起飞点";
        RefreshAllLabels();
        RefreshSummary();
    }

    private Canvas ResolveCanvas()
    {
        if (simulationManager != null && simulationManager.statusText != null)
        {
            return simulationManager.statusText.canvas;
        }

        return FindObjectOfType<Canvas>();
    }

    private void CreateSectionLabel(RectTransform parent, string label)
    {
        RectTransform labelRoot = new GameObject("Section_" + label, typeof(RectTransform)).GetComponent<RectTransform>();
        labelRoot.SetParent(parent, false);

        LayoutElement layout = labelRoot.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 18f;

        TMP_Text labelText = CreateText("Label", labelRoot, label, 12f, AccentColor, FontStyles.Bold);
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = Vector2.one;
        labelText.rectTransform.offsetMin = new Vector2(2f, 0f);
        labelText.rectTransform.offsetMax = new Vector2(-2f, 0f);
    }

    private void CreateStepperRow(
        RectTransform parent,
        string label,
        out TMP_Text valueText,
        Action onDecreaseClicked,
        Action onIncreaseClicked)
    {
        CreateStepperRow(parent, label, out valueText, onDecreaseClicked, onIncreaseClicked, 84f);
    }

    private void CreateStepperRow(
        RectTransform parent,
        string label,
        out TMP_Text valueText,
        Action onDecreaseClicked,
        Action onIncreaseClicked,
        float valueWidth)
    {
        RectTransform row = CreateRow(parent, label);
        CreateActionButton(row, "-", SecondaryButtonColor, onDecreaseClicked, 24f);
        valueText = CreateValueBadge(row, "Value", valueWidth);
        CreateActionButton(row, "+", PrimaryButtonColor, onIncreaseClicked, 24f);
    }

    private void CreateToggleRow(RectTransform parent, string label, out Button toggleButton, Action onClicked)
    {
        RectTransform row = CreateRow(parent, label);
        toggleButton = CreateActionButton(row, "ON", PositiveButtonColor, onClicked, 60f);
    }

    private void CreateButtonStripRow(RectTransform parent, string label, ButtonAction[] actions)
    {
        RectTransform row = CreateRow(parent, label);
        for (int i = 0; i < actions.Length; i++)
        {
            CreateActionButton(row, actions[i].label, actions[i].color, actions[i].callback, actions[i].width);
        }
    }

    private void CreateInputButtonRow(
        RectTransform parent,
        string label,
        out TMP_InputField inputField,
        ButtonAction[] actions)
    {
        GameObject rowObject = new GameObject("Row_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.SetParent(parent, false);

        LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 62f;
        rowLayout.minHeight = 62f;

        Image rowImage = row.GetComponent<Image>();
        ConfigureImageGraphic(rowImage);
        rowImage.color = RowColor;
        rowImage.raycastTarget = false;

        VerticalLayoutGroup verticalLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
        verticalLayout.padding = new RectOffset(8, 8, 4, 4);
        verticalLayout.spacing = 4f;
        verticalLayout.childAlignment = TextAnchor.UpperLeft;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = false;

        RectTransform inputRow = new GameObject("InputRow", typeof(RectTransform)).GetComponent<RectTransform>();
        inputRow.SetParent(row, false);
        HorizontalLayoutGroup inputLayout = inputRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        inputLayout.spacing = 4f;
        inputLayout.childAlignment = TextAnchor.MiddleLeft;
        inputLayout.childControlWidth = true;
        inputLayout.childControlHeight = true;
        inputLayout.childForceExpandWidth = false;
        inputLayout.childForceExpandHeight = false;
        LayoutElement inputRowLayout = inputRow.gameObject.AddComponent<LayoutElement>();
        inputRowLayout.preferredHeight = 24f;

        TMP_Text labelText = CreateText("Label", inputRow, label, 14f, PrimaryTextColor, FontStyles.Normal);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 40f;
        labelLayout.minWidth = 40f;
        labelLayout.flexibleWidth = 0f;
        labelText.alignment = TextAlignmentOptions.Left;

        inputField = CreateInputField(inputRow, label + "_Input", "输入导出目录");

        RectTransform buttonRow = new GameObject("ButtonRow", typeof(RectTransform)).GetComponent<RectTransform>();
        buttonRow.SetParent(row, false);
        HorizontalLayoutGroup buttonLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 4f;
        buttonLayout.childAlignment = TextAnchor.MiddleRight;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = false;
        LayoutElement buttonRowLayout = buttonRow.gameObject.AddComponent<LayoutElement>();
        buttonRowLayout.preferredHeight = 24f;

        GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(buttonRow, false);
        LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleWidth = 1f;
        spacerLayout.minWidth = 0f;

        for (int i = 0; i < actions.Length; i++)
        {
            CreateActionButton(buttonRow, actions[i].label, actions[i].color, actions[i].callback, actions[i].width);
        }
    }

    private void CreateInfoCard(
        RectTransform parent,
        string name,
        out TMP_Text bodyText,
        out LayoutElement layoutElement,
        float preferredHeight)
    {
        GameObject cardObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform card = cardObject.GetComponent<RectTransform>();
        card.SetParent(parent, false);

        layoutElement = card.gameObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = preferredHeight;
        layoutElement.preferredHeight = preferredHeight;

        Image cardImage = card.GetComponent<Image>();
        ConfigureImageGraphic(cardImage);
        cardImage.color = RowColor;
        cardImage.raycastTarget = false;

        bodyText = CreateText("BodyText", card, string.Empty, 12.5f, PrimaryTextColor, FontStyles.Normal);
        bodyText.enableWordWrapping = true;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.rectTransform.anchorMin = Vector2.zero;
        bodyText.rectTransform.anchorMax = Vector2.one;
        bodyText.rectTransform.offsetMin = new Vector2(10f, 8f);
        bodyText.rectTransform.offsetMax = new Vector2(-10f, -8f);
    }

    private RectTransform CreateRow(RectTransform parent, string label)
    {
        GameObject rowObject = new GameObject("Row_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.SetParent(parent, false);

        LayoutElement layoutElement = row.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 34f;

        Image rowImage = row.GetComponent<Image>();
        ConfigureImageGraphic(rowImage);
        rowImage.color = RowColor;
        rowImage.raycastTarget = false;

        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TMP_Text labelText = CreateText("Label", row, label, 14f, PrimaryTextColor, FontStyles.Normal);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 40f;
        labelLayout.minWidth = 40f;
        labelLayout.flexibleWidth = 0f;
        labelText.alignment = TextAlignmentOptions.Left;

        return row;
    }

    private TMP_Text CreateValueBadge(RectTransform parent, string name, float width)
    {
        GameObject badgeObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform badge = badgeObject.GetComponent<RectTransform>();
        badge.SetParent(parent, false);

        Image badgeImage = badge.GetComponent<Image>();
        ConfigureImageGraphic(badgeImage);
        badgeImage.color = new Color(0.11f, 0.23f, 0.31f, 0.98f);
        badgeImage.raycastTarget = false;

        LayoutElement layout = badge.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;
        layout.preferredHeight = 24f;

        TMP_Text valueText = CreateText("ValueText", badge, "-", 13f, AccentColor, FontStyles.Bold);
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.rectTransform.anchorMin = Vector2.zero;
        valueText.rectTransform.anchorMax = Vector2.one;
        valueText.rectTransform.offsetMin = Vector2.zero;
        valueText.rectTransform.offsetMax = Vector2.zero;
        return valueText;
    }

    private TMP_InputField CreateInputField(RectTransform parent, string name, string placeholderText)
    {
        GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        RectTransform rect = inputObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        LayoutElement layout = inputObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 130f;
        layout.preferredHeight = 24f;

        Image image = inputObject.GetComponent<Image>();
        ConfigureImageGraphic(image);
        image.color = new Color(0.09f, 0.20f, 0.28f, 0.98f);
        image.raycastTarget = true;

        TMP_InputField inputField = inputObject.GetComponent<TMP_InputField>();
        inputField.transition = Selectable.Transition.ColorTint;
        inputField.colors = BuildColorBlock(new Color(0.09f, 0.20f, 0.28f, 0.98f));
        inputField.contentType = TMP_InputField.ContentType.Standard;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = 260;
        inputField.customCaretColor = true;
        inputField.caretColor = AccentColor;

        RectTransform textArea = new GameObject("TextArea", typeof(RectTransform)).GetComponent<RectTransform>();
        textArea.SetParent(rect, false);
        textArea.anchorMin = Vector2.zero;
        textArea.anchorMax = Vector2.one;
        textArea.offsetMin = new Vector2(8f, 3f);
        textArea.offsetMax = new Vector2(-8f, -3f);

        TextMeshProUGUI textComponent = (TextMeshProUGUI)CreateText("Text", textArea, string.Empty, 11f, PrimaryTextColor, FontStyles.Normal);
        textComponent.alignment = TextAlignmentOptions.Left;
        textComponent.enableWordWrapping = false;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        textComponent.rectTransform.anchorMin = Vector2.zero;
        textComponent.rectTransform.anchorMax = Vector2.one;
        textComponent.rectTransform.offsetMin = Vector2.zero;
        textComponent.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI placeholder = (TextMeshProUGUI)CreateText(
            "Placeholder",
            textArea,
            placeholderText,
            11f,
            new Color(0.60f, 0.73f, 0.80f, 0.68f),
            FontStyles.Normal);
        placeholder.alignment = TextAlignmentOptions.Left;
        placeholder.enableWordWrapping = false;
        placeholder.overflowMode = TextOverflowModes.Ellipsis;
        placeholder.rectTransform.anchorMin = Vector2.zero;
        placeholder.rectTransform.anchorMax = Vector2.one;
        placeholder.rectTransform.offsetMin = Vector2.zero;
        placeholder.rectTransform.offsetMax = Vector2.zero;

        inputField.textViewport = textArea;
        inputField.textComponent = textComponent;
        inputField.placeholder = placeholder;
        return inputField;
    }

    private Button CreateActionButton(RectTransform parent, string label, Color color, Action onClicked, float width)
    {
        GameObject buttonObject = new GameObject(label + "_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = 24f;

        Image image = buttonObject.GetComponent<Image>();
        ConfigureImageGraphic(image);
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = BuildColorBlock(color);
        if (onClicked != null)
        {
            button.onClick.AddListener(() => onClicked());
        }

        TMP_Text buttonText = CreateText("Label", rect, label, 12f, PrimaryTextColor, FontStyles.Bold);
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.rectTransform.anchorMin = Vector2.zero;
        buttonText.rectTransform.anchorMax = Vector2.one;
        buttonText.rectTransform.offsetMin = Vector2.zero;
        buttonText.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private ColorBlock BuildColorBlock(Color baseColor)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = baseColor;
        colors.highlightedColor = new Color(
            Mathf.Clamp01(baseColor.r * 1.08f),
            Mathf.Clamp01(baseColor.g * 1.08f),
            Mathf.Clamp01(baseColor.b * 1.08f),
            baseColor.a);
        colors.pressedColor = new Color(
            Mathf.Clamp01(baseColor.r * 0.84f),
            Mathf.Clamp01(baseColor.g * 0.84f),
            Mathf.Clamp01(baseColor.b * 0.84f),
            baseColor.a);
        colors.selectedColor = baseColor;
        colors.disabledColor = new Color(baseColor.r * 0.45f, baseColor.g * 0.45f, baseColor.b * 0.45f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private TMP_Text CreateText(string name, RectTransform parent, string content, float fontSize, Color color, FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        if (runtimeFont != null)
        {
            text.font = runtimeFont;
        }

        return text;
    }

    private RectTransform CreatePanelArea(string name, RectTransform parent, Vector2 topLeftOffset, Vector2 bottomRightOffset)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(topLeftOffset.x, bottomRightOffset.y);
        rect.offsetMax = new Vector2(bottomRightOffset.x, topLeftOffset.y);
        return rect;
    }

    private void ConfigureSection(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        ConfigureImageGraphic(image);
        image.color = color;
        image.raycastTarget = false;
    }

    private void UpdateToggleButton(Button button, bool isEnabled)
    {
        if (button == null)
        {
            return;
        }

        Color baseColor = isEnabled ? PositiveButtonColor : NegativeButtonColor;
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = baseColor;
        }

        button.colors = BuildColorBlock(baseColor);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = isEnabled ? "ON" : "OFF";
        }
    }

    private void ConfigureImageGraphic(Image image)
    {
        image.sprite = null;
        image.type = Image.Type.Simple;
    }

    private void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    private int WrapIndex(int value, int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        if (value < 0)
        {
            return length - 1;
        }

        if (value >= length)
        {
            return 0;
        }

        return value;
    }

    private bool TryOpenFolderPicker(string initialDirectory, out string selectedDirectory)
    {
        selectedDirectory = string.Empty;

        if (TryOpenEditorFolderPanel(initialDirectory, out selectedDirectory))
        {
            return true;
        }

        return TryOpenWindowsFolderBrowser(initialDirectory, out selectedDirectory);
    }

    private bool TryOpenEditorFolderPanel(string initialDirectory, out string selectedDirectory)
    {
        selectedDirectory = string.Empty;

        try
        {
            Type editorUtilityType = Type.GetType("UnityEditor.EditorUtility, UnityEditor");
            if (editorUtilityType == null)
            {
                return false;
            }

            MethodInfo openFolderPanelMethod = editorUtilityType.GetMethod(
                "OpenFolderPanel",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string), typeof(string) },
                null);

            if (openFolderPanelMethod == null)
            {
                return false;
            }

            object result = openFolderPanelMethod.Invoke(
                null,
                new object[] { "选择导出目录", initialDirectory ?? string.Empty, string.Empty });

            selectedDirectory = result as string ?? string.Empty;
            return true;
        }
        catch
        {
            selectedDirectory = string.Empty;
            return false;
        }
    }

    private bool TryOpenWindowsFolderBrowser(string initialDirectory, out string selectedDirectory)
    {
        selectedDirectory = string.Empty;

        try
        {
            string dialogResultName = null;
            string resolvedSelectedDirectory = string.Empty;
            Exception threadException = null;
            Thread pickerThread = new Thread(() =>
            {
                try
                {
                    Assembly winFormsAssembly = Assembly.Load("System.Windows.Forms");
                    Type folderDialogType = winFormsAssembly.GetType("System.Windows.Forms.FolderBrowserDialog");
                    Type dialogResultType = winFormsAssembly.GetType("System.Windows.Forms.DialogResult");
                    if (folderDialogType == null || dialogResultType == null)
                    {
                        return;
                    }

                    using (IDisposable dialog = Activator.CreateInstance(folderDialogType) as IDisposable)
                    {
                        if (dialog == null)
                        {
                            return;
                        }

                        folderDialogType.GetProperty("Description")?.SetValue(dialog, "选择导出目录");
                        folderDialogType.GetProperty("ShowNewFolderButton")?.SetValue(dialog, true);

                        if (!string.IsNullOrWhiteSpace(initialDirectory))
                        {
                            folderDialogType.GetProperty("SelectedPath")?.SetValue(dialog, initialDirectory);
                        }

                        object result = folderDialogType.GetMethod("ShowDialog", Type.EmptyTypes)?.Invoke(dialog, null);
                        if (result == null)
                        {
                            return;
                        }

                        dialogResultName = Enum.GetName(dialogResultType, result);
                        if (string.Equals(dialogResultName, "OK", StringComparison.OrdinalIgnoreCase))
                        {
                            resolvedSelectedDirectory = folderDialogType.GetProperty("SelectedPath")?.GetValue(dialog) as string ?? string.Empty;
                        }
                    }
                }
                catch (Exception exception)
                {
                    threadException = exception;
                }
            });

            pickerThread.SetApartmentState(ApartmentState.STA);
            pickerThread.Start();
            pickerThread.Join();

            if (threadException != null)
            {
                return false;
            }

            selectedDirectory = resolvedSelectedDirectory;
            return true;
        }
        catch
        {
            selectedDirectory = string.Empty;
            return false;
        }
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

    private readonly struct ButtonAction
    {
        public readonly string label;
        public readonly Color color;
        public readonly Action callback;
        public readonly float width;

        public ButtonAction(string label, Color color, Action callback, float width)
        {
            this.label = label;
            this.color = color;
            this.callback = callback;
            this.width = width;
        }
    }
}
