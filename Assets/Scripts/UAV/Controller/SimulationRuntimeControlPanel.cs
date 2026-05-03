using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class SimulationRuntimeControlPanel : MonoBehaviour
{
    [Header("References")]
    public SimulationManager simulationManager;
    public DroneManager droneManager;
    public CameraManager cameraManager;
    public SimulationResultExporter resultExporter;
    public BatchExperimentRunner batchExperimentRunner;
    public DroneSpawnPointUIManager spawnPointManager;
    public RuntimeObstacleEditor obstacleEditor;
    public AlgorithmVisualizerManager algorithmVisualizerManager;
    public TaskQueueVisualizer taskQueueVisualizer;
    public PlanningMapVisualizer planningMapVisualizer;
    public PlanningBoundsSelector planningBoundsSelector;
    public RLMapExporter rlMapExporter;
    public RLPathResultImporter rlPathResultImporter;
    public RLMapObstacleVisualizer rlMapObstacleVisualizer;
    public RLQlearningTrainingRunner rlTrainingRunner;

    [Header("Panel Layout")]
    public Vector2 expandedSize = new Vector2(384f, 460f);
    public Vector2 collapsedSize = new Vector2(384f, 116f);
    public Vector2 anchoredPosition = new Vector2(-24f, -96f);
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
    private const float MinPlanningBoundary = -1000f;
    private const float MaxPlanningBoundary = 1000f;
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
    private const float MinObstacleHeight = 2f;
    private const float MaxObstacleHeight = 30f;
    private const float ObstacleHeightStep = 1f;
    private const float MinObstacleScale = 0.5f;
    private const float MaxObstacleScale = 5f;
    private const float ObstacleScaleStep = 0.25f;
    private const int MinBatchRunCount = 1;
    private const int MaxBatchRunCount = 50;
    private const float StatsCardMinHeight = 168f;
    private const float StatsCardVerticalPadding = 18f;
    private const float EvaluationCardMinHeight = 172f;
    private const int MaxEvaluationHistoryCount = 4;

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
    private RectTransform summaryCardRoot;
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
    private TMP_Text planningMinYValueText;
    private TMP_Text planningMaxYValueText;
    private TMP_Text rlCaseValueText;
    private TMP_Text followHeightValueText;
    private TMP_Text followDistanceValueText;
    private TMP_Text obstacleHeightValueText;
    private TMP_Text obstacleStyleValueText;
    private TMP_Text obstacleScaleValueText;
    private TMP_Text exportDirectoryStatusText;
    private TMP_Text batchRunCountValueText;
    private TMP_Text batchStatusText = null;
    private TMP_Text experimentGroupValueText = null;
    private TMP_Text experimentPresetValueText = null;
    private TMP_Text experimentPresetSummaryText = null;
    private TMP_Text summaryText;
    private TMP_Text statsText;
    private TMP_Text footerText;
    private TMP_Text expandButtonText;
    private TMP_Text visualizationSelectedDroneValueText;
    private TMP_Text visualizationModeValueText;
    private TMP_Text visualizationSpeedValueText;
    private TMP_Text visualizationStatusText;
    private TMP_Text visualizationDescriptionText;
    private TMP_Text visualizationLegendText;
    private TMP_Text schedulingResultText;
    private TMP_Text evaluationText;
    private TMP_InputField exportDirectoryInputField;
    private LayoutElement statsCardLayoutElement;
    private LayoutElement schedulingResultLayoutElement;
    private LayoutElement evaluationLayoutElement;
    private Button plannedPathToggleButton;
    private Button trailToggleButton;
    private Button diagonalPlanningToggleButton;
    private Button obstacleAutoConfigToggleButton;
    private Button planningBoundsSelectionButton;
    private Button taskQueueVisualizationToggleButton;
    private Button visualizationObstacleTransparencyToggleButton;
    private Button visualizationPlayButton;
    private Button visualizationPauseButton;
    private Button visualizationStepButton;
    private Button visualizationResetButton;
    private SimulationContext simulationContext;
    private bool isExpanded;
    private bool wasPlanningBoundsSelecting;
    private float nextSummaryRefreshTime;
    private Vector2 lastCanvasSize;

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
    private float configuredObstacleHeight = 10f;
    private float configuredObstacleScale = 1f;
    private string configuredObstacleTemplateName = "长方体";
    private bool showPlannedPath = true;
    private bool showTrail = true;
    private bool configuredAllowDiagonalPlanning = true;
    private bool configuredAutoConfigureObstacles = true;
    private bool hasPendingDroneCountChange;
    private int configuredBatchRunCount = 5;
    private string transientMessage = "面板已就绪";
    private readonly List<ExperimentPresetGroupInfo> experimentPresetGroups = new List<ExperimentPresetGroupInfo>();
    private readonly List<AlgorithmEvaluationSnapshot> evaluationHistory = new List<AlgorithmEvaluationSnapshot>();
    private readonly List<string> rlCaseNames = new List<string>();
    private int selectedExperimentGroupIndex;
    private int selectedExperimentPresetIndex;
    private int selectedRLCaseIndex;

    private sealed class AlgorithmEvaluationSnapshot
    {
        public string timeLabel = "";
        public string schedulerName = "";
        public string plannerName = "";
        public int totalTaskCount;
        public int completedTaskCount;
        public int assignedTaskCount;
        public int droneCount;
        public int assignmentSpread;
        public float averageAssignedTasks;
        public float totalFlightDistance;
        public float averageFlightDistance;
        public string longestDroneLabel = "";
        public float longestDroneDistance;
        public int totalWaitCount;
        public int totalConflictCount;
        public int buildingWarningCount;
        public float elapsedSeconds;
        public float completionRate;
    }

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

    private void OnEnable()
    {
        simulationContext = SimulationContext.GetOrCreate(this);
        simulationContext.TasksChanged += HandleSimulationContextChanged;
        simulationContext.SpawnPointsChanged += HandleSimulationContextChanged;
        simulationContext.ObstaclesChanged += HandleSimulationContextChanged;
    }

    private void OnDisable()
    {
        if (simulationContext == null)
        {
            return;
        }

        simulationContext.TasksChanged -= HandleSimulationContextChanged;
        simulationContext.SpawnPointsChanged -= HandleSimulationContextChanged;
        simulationContext.ObstaclesChanged -= HandleSimulationContextChanged;
        simulationContext = null;
    }

    private void HandleSimulationContextChanged()
    {
        if (!isActiveAndEnabled || panelRoot == null)
        {
            return;
        }

        RefreshAllLabels();
        RefreshSummary();
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

        Vector2 canvasSize = GetCanvasSize();
        if ((canvasSize - lastCanvasSize).sqrMagnitude > 0.01f)
        {
            lastCanvasSize = canvasSize;
            ApplyExpandState();
            RefreshStatsCardLayout();
        }

        if (Time.unscaledTime >= nextSummaryRefreshTime)
        {
            nextSummaryRefreshTime = Time.unscaledTime + 0.35f;
            SyncFromSystems();
            RefreshAllLabels();
            RefreshBatchStatus();
            RefreshSummary();
        }
    }

    private void CacheReferences()
    {
        simulationManager = RuntimeSceneRegistry.Resolve(simulationManager, this);
        droneManager = RuntimeSceneRegistry.Resolve(
            droneManager,
            simulationManager != null ? simulationManager.droneManager : null,
            this);
        cameraManager = RuntimeSceneRegistry.Resolve(cameraManager, this);
        resultExporter = RuntimeSceneRegistry.Resolve(
            resultExporter,
            simulationManager != null ? simulationManager.resultExporter : null,
            this);
        batchExperimentRunner = RuntimeSceneRegistry.Resolve(
            batchExperimentRunner,
            simulationManager != null ? simulationManager.batchExperimentRunner : null,
            this);
        spawnPointManager = RuntimeSceneRegistry.Resolve(spawnPointManager, this);
        obstacleEditor = RuntimeSceneRegistry.Resolve(obstacleEditor, this);
        algorithmVisualizerManager = RuntimeSceneRegistry.Resolve(
            algorithmVisualizerManager,
            simulationManager != null ? simulationManager.algorithmVisualizerManager : null,
            this);
        taskQueueVisualizer = RuntimeSceneRegistry.Resolve(
            taskQueueVisualizer,
            simulationManager != null ? simulationManager.taskQueueVisualizer : null,
            this);
        planningMapVisualizer = RuntimeSceneRegistry.Resolve(
            planningMapVisualizer,
            simulationManager != null ? simulationManager.planningMapVisualizer : null,
            this);
        planningBoundsSelector = RuntimeSceneRegistry.Resolve(planningBoundsSelector, this);
        rlMapExporter = RuntimeSceneRegistry.Resolve(
            rlMapExporter,
            simulationManager != null ? simulationManager.rlMapExporter : null,
            this);
        rlPathResultImporter = RuntimeSceneRegistry.Resolve(
            rlPathResultImporter,
            simulationManager != null ? simulationManager.rlPathResultImporter : null,
            this);
        rlMapObstacleVisualizer = RuntimeSceneRegistry.Resolve(rlMapObstacleVisualizer, this);
        rlTrainingRunner = RuntimeSceneRegistry.Resolve(
            rlTrainingRunner,
            simulationManager != null ? simulationManager.rlTrainingRunner : null,
            this);

        if (runtimeFont == null)
        {
            runtimeFont = ResolveRuntimeFont();
        }

        schedulerOptions = (SchedulerAlgorithmType[])Enum.GetValues(typeof(SchedulerAlgorithmType));
        plannerOptions = (PathPlannerType[])Enum.GetValues(typeof(PathPlannerType));
    }

    private TMP_FontAsset ResolveRuntimeFont()
    {
        TMP_FontAsset candidate = simulationManager != null && simulationManager.statusText != null
            ? simulationManager.statusText.font
            : null;

        if (SupportsChineseGlyphs(candidate))
        {
            return candidate;
        }

        TMP_Text[] sceneTexts = FindObjectsOfType<TMP_Text>(true);
        foreach (TMP_Text text in sceneTexts)
        {
            if (text == null || text.font == null)
            {
                continue;
            }

            if (candidate == null)
            {
                candidate = text.font;
            }

            if (SupportsChineseGlyphs(text.font))
            {
                return text.font;
            }
        }

        return candidate;
    }

    private static bool SupportsChineseGlyphs(TMP_FontAsset font)
    {
        if (font == null)
        {
            return false;
        }

        return font.HasCharacter('中') || font.HasCharacter('态') || font.HasCharacter('仿');
    }
}
