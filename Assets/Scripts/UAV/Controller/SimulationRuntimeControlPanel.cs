using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimulationRuntimeControlPanel : MonoBehaviour
{
    [Header("References")]
    public SimulationManager simulationManager;
    public DroneManager droneManager;
    public CameraManager cameraManager;

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
    private const float MinTimeScale = 0.25f;
    private const float MaxTimeScale = 3f;
    private const float TimeScaleStep = 0.25f;

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

    private RectTransform panelRoot;
    private RectTransform bodyRoot;
    private RectTransform footerRoot;
    private TMP_FontAsset runtimeFont;
    private TMP_Text schedulerValueText;
    private TMP_Text plannerValueText;
    private TMP_Text droneCountValueText;
    private TMP_Text droneSpeedValueText;
    private TMP_Text timeScaleValueText;
    private TMP_Text summaryText;
    private TMP_Text footerText;
    private TMP_Text expandButtonText;
    private Button plannedPathToggleButton;
    private Button trailToggleButton;
    private bool isExpanded;
    private float nextSummaryRefreshTime;

    private SchedulerAlgorithmType[] schedulerOptions;
    private PathPlannerType[] plannerOptions;
    private int schedulerIndex;
    private int plannerIndex;
    private int configuredDroneCount = 4;
    private float configuredDroneSpeed = 5f;
    private float configuredTimeScale = 1f;
    private bool showPlannedPath = true;
    private bool showTrail = true;
    private string transientMessage = "面板已就绪";

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

        if (Time.unscaledTime >= nextSummaryRefreshTime)
        {
            nextSummaryRefreshTime = Time.unscaledTime + 0.35f;
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

        Button expandButton = CreateActionButton(header, "Expand", SecondaryButtonColor, ToggleExpanded, 70f);
        RectTransform expandRect = expandButton.GetComponent<RectTransform>();
        expandRect.anchorMin = new Vector2(1f, 0.5f);
        expandRect.anchorMax = new Vector2(1f, 0.5f);
        expandRect.pivot = new Vector2(1f, 0.5f);
        expandRect.anchoredPosition = new Vector2(-4f, 0f);
        expandButtonText = expandButton.GetComponentInChildren<TMP_Text>();

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

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
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

        CreateSectionLabel(content, "算法");
        CreateStepperRow(content, "调度", out schedulerValueText, OnPreviousSchedulerClicked, OnNextSchedulerClicked);
        CreateStepperRow(content, "路径", out plannerValueText, OnPreviousPlannerClicked, OnNextPlannerClicked);

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
            new ButtonAction("下一架", PrimaryButtonColor, FocusNextDrone, 60f)
        });

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

        if (schedulerIndex < 0)
        {
            schedulerIndex = 0;
        }

        if (plannerIndex < 0)
        {
            plannerIndex = 0;
        }
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

        if (timeScaleValueText != null)
        {
            timeScaleValueText.text = $"{configuredTimeScale:0.00}x";
        }

        UpdateToggleButton(plannedPathToggleButton, showPlannedPath);
        UpdateToggleButton(trailToggleButton, showTrail);

        if (expandButtonText != null)
        {
            expandButtonText.text = isExpanded ? "收起" : "展开";
        }

        if (footerText != null)
        {
            footerText.text = transientMessage;
        }
    }

    private void RefreshSummary()
    {
        if (summaryText == null)
        {
            return;
        }

        int droneCount = droneManager != null ? droneManager.drones.Count : 0;
        int taskCount = FindObjectsOfType<TaskPoint>().Length;
        string simulationState = simulationManager != null ? FormatSimulationState(simulationManager.currentState) : "未知";
        string cameraMode = "未连接";
        string cameraTarget = "-";

        if (cameraManager != null)
        {
            cameraMode = cameraManager.isOverview ? "总览" : "跟随";
            cameraTarget = cameraManager.targetDrone != null ? cameraManager.targetDrone.name : "-";
        }

        summaryText.text = $"状态 {simulationState}  机群 {droneCount}  任务 {taskCount}\n镜头 {cameraMode}  目标 {cameraTarget}";
    }

    private void ToggleExpanded()
    {
        isExpanded = !isExpanded;
        ApplyExpandState();
        transientMessage = isExpanded ? "已展开运行面板" : "已收起运行面板";
        RefreshAllLabels();
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

    private void ApplyTimeScaleSettings()
    {
        Time.timeScale = configuredTimeScale;
        transientMessage = $"仿真倍速 {configuredTimeScale:0.00}x";
        RefreshAllLabels();
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
            simulationManager.SetState(SimulationState.Idle);
        }

        droneManager.schedulerAlgorithm = schedulerOptions[schedulerIndex];
        droneManager.pathPlannerType = plannerOptions[plannerIndex];
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
            droneManager.ApplyDroneSpeedToAll(configuredDroneSpeed);
            droneManager.ApplyPathVisibilityToAll(showPlannedPath, showTrail);
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
        RectTransform row = CreateRow(parent, label);
        CreateActionButton(row, "-", SecondaryButtonColor, onDecreaseClicked, 24f);
        valueText = CreateValueBadge(row, "Value");
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

    private TMP_Text CreateValueBadge(RectTransform parent, string name)
    {
        GameObject badgeObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform badge = badgeObject.GetComponent<RectTransform>();
        badge.SetParent(parent, false);

        Image badgeImage = badge.GetComponent<Image>();
        ConfigureImageGraphic(badgeImage);
        badgeImage.color = new Color(0.11f, 0.23f, 0.31f, 0.98f);
        badgeImage.raycastTarget = false;

        LayoutElement layout = badge.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 84f;
        layout.minWidth = 84f;
        layout.preferredHeight = 24f;

        TMP_Text valueText = CreateText("ValueText", badge, "-", 13f, AccentColor, FontStyles.Bold);
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.rectTransform.anchorMin = Vector2.zero;
        valueText.rectTransform.anchorMax = Vector2.one;
        valueText.rectTransform.offsetMin = Vector2.zero;
        valueText.rectTransform.offsetMax = Vector2.zero;
        return valueText;
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

    private string FormatSchedulerName(SchedulerAlgorithmType algorithmType)
    {
        switch (algorithmType)
        {
            case SchedulerAlgorithmType.GreedyNearest:
                return "最近优先";
            case SchedulerAlgorithmType.EvenSplit:
            default:
                return "均分任务";
        }
    }

    private string FormatPlannerName(PathPlannerType plannerType)
    {
        switch (plannerType)
        {
            case PathPlannerType.AStar:
                return "A*";
            case PathPlannerType.StraightLine:
            default:
                return "直线";
        }
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
