using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 统一管理路径规划过程可视化的记录、播放、步进和模式切换。
/// 规划算法只负责吐出步骤事件；播放与渲染全部在这里协调。
/// </summary>
public class AlgorithmVisualizerManager : MonoBehaviour
{
    private static readonly float[] PlaybackSpeedOptions = { 0.1f, 0.25f, 0.5f, 1f, 2f };
    private static readonly PathPlanningVisualizationMode[] PlaybackModeOptions =
    {
        PathPlanningVisualizationMode.FullProcess,
        PathPlanningVisualizationMode.FinalResultOnly
    };
    private static readonly Color[] DroneAccentPalette =
    {
        new Color(0.20f, 0.86f, 1.00f, 1f),
        new Color(1.00f, 0.66f, 0.22f, 1f),
        new Color(0.48f, 1.00f, 0.52f, 1f),
        new Color(1.00f, 0.44f, 0.44f, 1f),
        new Color(0.82f, 0.56f, 1.00f, 1f),
        new Color(1.00f, 0.82f, 0.28f, 1f)
    };

    [Header("References")]
    public SimulationManager simulationManager;
    public DroneManager droneManager;
    public CameraManager cameraManager;
    public PathPlanningProcessRenderer processRenderer;
    public ObstacleTransparencyController obstacleTransparencyController;

    [Header("Playback")]
    public PathPlanningVisualizationMode visualizationMode = PathPlanningVisualizationMode.FullProcess;
    public float playbackSpeed = 0.5f;
    public bool autoSelectLatestTrace = true;
    public bool autoplayOnNewTrace = false;
    public float baseSecondsPerStep = 0.35f;
    public bool capturePlanningTraces = true;
    public bool recordAllDroneTraces = false;
    public bool captureAllTaskSegmentsForSelectedDrone = true;
    public int maxRecordedStepsPerTrace = 2000;

    [Header("Visibility Aid")]
    public bool obstacleTransparencyEnabled = true;

    private readonly Dictionary<int, List<PathPlanningVisualizationTrace>> tracesByDroneId =
        new Dictionary<int, List<PathPlanningVisualizationTrace>>();
    private readonly List<PlaybackStepRef> playbackSequence = new List<PlaybackStepRef>();
    private readonly PlaybackRuntimeState runtimeState = new PlaybackRuntimeState();

    private int selectedDroneId = -1;
    private int currentSequenceCursor = -1;
    private int appliedUnderlyingStepIndex = -1;
    private int appliedTraceIndex = -1;
    private float playbackTimer;
    private PathPlanningVisualizationPlaybackState playbackState = PathPlanningVisualizationPlaybackState.Ready;

    public PathPlanningVisualizationPlaybackState PlaybackState => playbackState;
    public bool ObstacleTransparencyEnabled => obstacleTransparencyEnabled;
    public bool ObstacleTransparencyActive =>
        obstacleTransparencyController != null && obstacleTransparencyController.IsTransparent;

    private void Awake()
    {
        CacheReferences();
        EnsureRenderer();
        EnsureObstacleTransparencyController();
    }

    private void Start()
    {
        CacheReferences();
        EnsureRenderer();
        EnsureObstacleTransparencyController();
        RefreshRenderer();
    }

    private void Update()
    {
        CacheReferences();
        EnsureRenderer();
        EnsureObstacleTransparencyController();

        if (playbackState != PathPlanningVisualizationPlaybackState.Playing)
        {
            if (RefreshRendererProjection())
            {
                RefreshRenderer();
            }
            return;
        }

        PathPlanningVisualizationTrace trace = GetCurrentPlaybackTrace();
        if (trace == null || playbackSequence.Count == 0)
        {
            playbackState = PathPlanningVisualizationPlaybackState.Ready;
            RefreshRenderer();
            ApplyObstacleTransparencyState();
            return;
        }

        float speed = Mathf.Max(0.05f, playbackSpeed);
        float stepDuration = Mathf.Max(0.02f, baseSecondsPerStep / speed);
        playbackTimer += Time.unscaledDeltaTime;

        while (playbackTimer >= stepDuration)
        {
            playbackTimer -= stepDuration;
            if (!AdvanceToNextStep())
            {
                break;
            }
        }

        if (RefreshRendererProjection())
        {
            RefreshRenderer();
        }

        ApplyObstacleTransparencyState();
    }

    private void OnDisable()
    {
        if (obstacleTransparencyController != null)
        {
            obstacleTransparencyController.SetTransparent(false);
        }
    }

    public PathPlanningVisualizationRecorder CreateRecorder(
        PathPlannerType plannerType,
        int droneId,
        string droneName,
        PathPlanningRequest request)
    {
        if (!ShouldCapturePlanningTrace(droneId))
        {
            return null;
        }

        Color accent = ResolveDroneAccentColor(droneId);
        return new PathPlanningVisualizationRecorder(
            droneId,
            droneName,
            plannerType,
            UAVAlgorithmNames.GetPlannerIdentifier(plannerType),
            UAVAlgorithmNames.GetPlannerDisplayName(plannerType),
            accent,
            request,
            maxRecordedStepsPerTrace);
    }

    public bool ShouldCapturePlanningTrace(int droneId)
    {
        if (!capturePlanningTraces)
        {
            return false;
        }

        if (recordAllDroneTraces)
        {
            return true;
        }

        if (selectedDroneId >= 0)
        {
            return selectedDroneId == droneId || !HasPlayableTrace();
        }

        if (captureAllTaskSegmentsForSelectedDrone && HasTraceForDrone(droneId))
        {
            return true;
        }

        return tracesByDroneId.Count == 0;
    }

    public void RegisterPlanningTrace(PathPlanningVisualizationTrace trace)
    {
        if (trace == null)
        {
            return;
        }

        List<PathPlanningVisualizationTrace> traces = GetOrCreateTraceList(trace.droneId);
        traces.Add(trace);

        if (autoSelectLatestTrace || selectedDroneId < 0 || !HasTraceForDrone(selectedDroneId))
        {
            selectedDroneId = trace.droneId;
        }

        RebuildPlaybackSequence();
        if (visualizationMode == PathPlanningVisualizationMode.FinalResultOnly)
        {
            ShowCurrentModeDefault();
            return;
        }

        if (playbackState != PathPlanningVisualizationPlaybackState.Playing)
        {
            ResetPlayback();
        }

        if (autoplayOnNewTrace)
        {
            Play();
        }
    }

    public void ClearAllTraces()
    {
        tracesByDroneId.Clear();
        playbackSequence.Clear();
        selectedDroneId = -1;
        ResetPlayback();
    }

    public void Play()
    {
        if (!HasPlayableTrace())
        {
            return;
        }

        if (visualizationMode == PathPlanningVisualizationMode.FinalResultOnly)
        {
            ShowCurrentModeDefault();
            return;
        }

        if (currentSequenceCursor >= playbackSequence.Count - 1)
        {
            ResetPlayback();
        }

        playbackState = PathPlanningVisualizationPlaybackState.Playing;
        ApplyObstacleTransparencyState();
    }

    public void Pause()
    {
        if (playbackState == PathPlanningVisualizationPlaybackState.Playing)
        {
            playbackState = PathPlanningVisualizationPlaybackState.Paused;
        }

        ApplyObstacleTransparencyState();
    }

    public void Resume()
    {
        if (!HasPlayableTrace())
        {
            return;
        }

        if (playbackState == PathPlanningVisualizationPlaybackState.Paused)
        {
            playbackState = PathPlanningVisualizationPlaybackState.Playing;
        }
        else if (playbackState == PathPlanningVisualizationPlaybackState.Ready ||
                 playbackState == PathPlanningVisualizationPlaybackState.Completed)
        {
            Play();
        }

        ApplyObstacleTransparencyState();
    }

    public void ResetPlayback()
    {
        playbackTimer = 0f;
        currentSequenceCursor = -1;
        appliedUnderlyingStepIndex = -1;
        appliedTraceIndex = -1;
        playbackState = PathPlanningVisualizationPlaybackState.Ready;
        runtimeState.Reset();
        RefreshRenderer();
        ApplyObstacleTransparencyState();
    }

    public void StepForward()
    {
        if (!HasPlayableTrace())
        {
            return;
        }

        if (visualizationMode == PathPlanningVisualizationMode.FinalResultOnly)
        {
            ShowCurrentModeDefault();
            return;
        }

        if (playbackState == PathPlanningVisualizationPlaybackState.Playing)
        {
            playbackState = PathPlanningVisualizationPlaybackState.Paused;
        }

        AdvanceToNextStep();
        ApplyObstacleTransparencyState();
    }

    public void SelectPreviousDrone()
    {
        List<int> droneIds = GetSelectableDroneIds();
        if (droneIds.Count == 0)
        {
            return;
        }

        int currentIndex = Mathf.Max(0, droneIds.IndexOf(selectedDroneId));
        selectedDroneId = droneIds[(currentIndex - 1 + droneIds.Count) % droneIds.Count];
        OnTraceSelectionChanged();
    }

    public void SelectNextDrone()
    {
        List<int> droneIds = GetSelectableDroneIds();
        if (droneIds.Count == 0)
        {
            return;
        }

        int currentIndex = Mathf.Max(0, droneIds.IndexOf(selectedDroneId));
        selectedDroneId = droneIds[(currentIndex + 1) % droneIds.Count];
        OnTraceSelectionChanged();
    }

    public void SelectPreviousMode()
    {
        int index = ResolvePlaybackModeIndex();
        index = (index - 1 + PlaybackModeOptions.Length) % PlaybackModeOptions.Length;
        visualizationMode = PlaybackModeOptions[index];
        OnPlaybackModeChanged();
    }

    public void SelectNextMode()
    {
        int index = ResolvePlaybackModeIndex();
        index = (index + 1) % PlaybackModeOptions.Length;
        visualizationMode = PlaybackModeOptions[index];
        OnPlaybackModeChanged();
    }

    public void SelectPreviousSpeed()
    {
        int index = ResolveClosestPlaybackSpeedIndex();
        index = (index - 1 + PlaybackSpeedOptions.Length) % PlaybackSpeedOptions.Length;
        playbackSpeed = PlaybackSpeedOptions[index];
    }

    public void SelectNextSpeed()
    {
        int index = ResolveClosestPlaybackSpeedIndex();
        index = (index + 1) % PlaybackSpeedOptions.Length;
        playbackSpeed = PlaybackSpeedOptions[index];
    }

    public bool HasPlayableTrace()
    {
        List<PathPlanningVisualizationTrace> traces = GetActiveTraceList();
        if (traces == null)
        {
            return false;
        }

        for (int i = 0; i < traces.Count; i++)
        {
            if (traces[i] != null && traces[i].HasSteps())
            {
                return true;
            }
        }

        return false;
    }

    public void SetObstacleTransparencyEnabled(bool enabled)
    {
        obstacleTransparencyEnabled = enabled;
        ApplyObstacleTransparencyState();
    }

    public string GetObstacleTransparencyLabel()
    {
        if (!obstacleTransparencyEnabled)
        {
            return "关闭";
        }

        return ObstacleTransparencyActive ? "已半透明" : "待播放";
    }

    public string GetSelectedDroneLabel()
    {
        if (selectedDroneId < 0)
        {
            return "等待规划";
        }

        DroneData data = droneManager != null ? droneManager.GetDroneData(selectedDroneId) : null;
        string droneName = data != null && !string.IsNullOrWhiteSpace(data.droneName)
            ? data.droneName
            : $"无人机 {selectedDroneId:D2}";

        if (!HasTraceForDrone(selectedDroneId))
        {
            return $"{droneName} (暂无轨迹)";
        }

        return droneName;
    }

    public string GetModeDisplayName()
    {
        NormalizePlaybackMode();
        switch (visualizationMode)
        {
            case PathPlanningVisualizationMode.FinalResultOnly:
                return "仅最终结果";
            case PathPlanningVisualizationMode.FullProcess:
            default:
                return "完整过程";
        }
    }

    public string GetPlaybackSpeedLabel()
    {
        return $"{playbackSpeed:0.##}x";
    }

    public string GetPlaybackStateLabel()
    {
        switch (playbackState)
        {
            case PathPlanningVisualizationPlaybackState.Playing:
                return "运行中";
            case PathPlanningVisualizationPlaybackState.Paused:
                return "暂停";
            case PathPlanningVisualizationPlaybackState.Completed:
                return "完成";
            case PathPlanningVisualizationPlaybackState.Ready:
            default:
                return "就绪";
        }
    }

    public string GetCurrentAlgorithmLabel()
    {
        PathPlanningVisualizationTrace trace = GetCurrentPlaybackTrace();
        if (trace == null)
        {
            return "未生成规划轨迹";
        }

        return $"{trace.plannerDisplayName} / {trace.plannerName}  {GetCurrentSegmentLabel()}";
    }

    public string GetCurrentStepLabel()
    {
        if (playbackSequence.Count == 0)
        {
            return "0 / 0";
        }

        int current = Mathf.Clamp(currentSequenceCursor + 1, 0, playbackSequence.Count);
        return $"{current} / {playbackSequence.Count}";
    }

    public string GetStatusText()
    {
        PathPlanningVisualizationTrace trace = GetCurrentPlaybackTrace();
        if (trace == null)
        {
            return "算法: 等待规划\n步骤: 0 / 0\n状态: 暂无轨迹";
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("算法: ").Append(trace.plannerDisplayName)
            .Append("\n段落: ").Append(GetCurrentSegmentLabel())
            .Append("\n步骤: ").Append(GetCurrentStepLabel())
            .Append("\n状态: ").Append(GetPlaybackStateLabel())
            .Append("\n前沿: ").Append(runtimeState.CountNodes(PathPlanningVisualizationNodeRole.Frontier))
            .Append("  已关闭: ").Append(runtimeState.CountNodes(PathPlanningVisualizationNodeRole.Closed))
            .Append("  拒绝: ").Append(runtimeState.CountNodes(PathPlanningVisualizationNodeRole.Rejected));

        if (trace.truncated)
        {
            builder.Append("\n记录上限: ").Append(trace.recordedStepLimit).Append(" 步，超出部分未继续记录");
        }

        if (runtimeState.searchComplete)
        {
            builder.Append("\n结果: ").Append(runtimeState.searchSucceeded ? "成功" : "失败");
        }

        return builder.ToString();
    }

    public string GetCurrentDescription()
    {
        PathPlanningVisualizationTrace trace = GetCurrentPlaybackTrace();
        if (trace == null)
        {
            return "当前还没有可播放的规划过程。开始仿真或触发重新规划后，面板会记录最近一次搜索过程。";
        }

        if (!string.IsNullOrWhiteSpace(runtimeState.description))
        {
            return runtimeState.description;
        }

        return trace.resultMessage;
    }

    public string GetLegendText()
    {
        return
            "起点 / 终点: 绿色 / 红色\n" +
            "当前扩展节点: 黄色高亮\n" +
            "Open / Frontier: 青色半透明\n" +
            "Closed / 已确定: 深蓝色\n" +
            "Rejected / 放弃节点: 灰红色\n" +
            "Blocked / 不可通行: 深红色\n" +
            "探索树 / 接受边: 蓝青色细线\n" +
            "拒绝边: 暗红色细线\n" +
            "候选路径: 琥珀色\n" +
            "最终路径: 无人机专属高亮色";
    }

    private void CacheReferences()
    {
        simulationManager = RuntimeSceneRegistry.Resolve(simulationManager, this);
        droneManager = RuntimeSceneRegistry.Resolve(
            droneManager,
            simulationManager != null ? simulationManager.droneManager : null,
            this);
        cameraManager = RuntimeSceneRegistry.Resolve(cameraManager, this);
        obstacleTransparencyController = RuntimeSceneRegistry.Resolve(obstacleTransparencyController, this);
    }

    private void EnsureRenderer()
    {
        if (processRenderer == null)
        {
            processRenderer = GetComponent<PathPlanningProcessRenderer>();
            if (processRenderer == null)
            {
                processRenderer = gameObject.AddComponent<PathPlanningProcessRenderer>();
            }
        }
    }

    private void EnsureObstacleTransparencyController()
    {
        if (obstacleTransparencyController == null)
        {
            obstacleTransparencyController = GetComponent<ObstacleTransparencyController>();
            if (obstacleTransparencyController == null)
            {
                obstacleTransparencyController = gameObject.AddComponent<ObstacleTransparencyController>();
            }
        }

        obstacleTransparencyController.droneManager = droneManager;
        if (droneManager != null && droneManager.obstacleRoot != null)
        {
            obstacleTransparencyController.obstacleRoot = droneManager.obstacleRoot;
        }
    }

    private void OnTraceSelectionChanged()
    {
        RebuildPlaybackSequence();
        ShowCurrentModeDefault();
    }

    private void OnPlaybackModeChanged()
    {
        NormalizePlaybackMode();
        RebuildPlaybackSequence();
        ShowCurrentModeDefault();
    }

    private void ShowCurrentModeDefault()
    {
        ResetPlayback();

        if (visualizationMode == PathPlanningVisualizationMode.FinalResultOnly && playbackSequence.Count > 0)
        {
            ApplyAggregateFinalResult();
            playbackState = PathPlanningVisualizationPlaybackState.Completed;
            ApplyObstacleTransparencyState();
        }
    }

    private bool AdvanceToNextStep()
    {
        if (playbackSequence.Count == 0)
        {
            playbackState = PathPlanningVisualizationPlaybackState.Ready;
            RefreshRenderer();
            ApplyObstacleTransparencyState();
            return false;
        }

        int nextCursor = currentSequenceCursor + 1;
        if (nextCursor >= playbackSequence.Count)
        {
            playbackState = PathPlanningVisualizationPlaybackState.Completed;
            RefreshRenderer();
            ApplyObstacleTransparencyState();
            return false;
        }

        AdvanceToSequenceCursor(nextCursor);
        if (currentSequenceCursor >= playbackSequence.Count - 1)
        {
            playbackState = PathPlanningVisualizationPlaybackState.Completed;
        }
        else if (playbackState != PathPlanningVisualizationPlaybackState.Playing)
        {
            playbackState = PathPlanningVisualizationPlaybackState.Paused;
        }

        return true;
    }

    private void AdvanceToSequenceCursor(int targetCursor)
    {
        if (targetCursor < 0 || targetCursor >= playbackSequence.Count)
        {
            return;
        }

        PlaybackStepRef stepRef = playbackSequence[targetCursor];
        PathPlanningVisualizationTrace trace = GetTraceAt(stepRef.traceIndex);
        if (trace == null || stepRef.stepIndex < 0 || stepRef.stepIndex >= trace.steps.Count)
        {
            return;
        }

        if (appliedTraceIndex != stepRef.traceIndex || targetCursor < currentSequenceCursor)
        {
            runtimeState.Reset();
            appliedUnderlyingStepIndex = -1;
            appliedTraceIndex = stepRef.traceIndex;
        }

        int targetUnderlyingStep = stepRef.stepIndex;
        for (int i = appliedUnderlyingStepIndex + 1; i <= targetUnderlyingStep; i++)
        {
            ApplyStep(trace.steps[i]);
        }

        appliedUnderlyingStepIndex = targetUnderlyingStep;
        currentSequenceCursor = targetCursor;
        RefreshRenderer();
    }

    private void ApplyStep(PathPlanningVisualizationStep step)
    {
        ApplyStepToState(runtimeState, step);
    }

    private static void ApplyStepToState(PlaybackRuntimeState targetState, PathPlanningVisualizationStep step)
    {
        if (targetState == null || step == null)
        {
            return;
        }

        targetState.description = step.description;

        if (step.clearRejectedEdges)
        {
            targetState.ClearRejectedEdges();
        }

        if (ContainsCurrentNodeUpdate(step))
        {
            targetState.DemoteCurrentNodesToVisited();
        }

        for (int i = 0; i < step.nodeUpdates.Count; i++)
        {
            targetState.SetNode(step.nodeUpdates[i]);
        }

        for (int i = 0; i < step.edgeUpdates.Count; i++)
        {
            targetState.SetEdge(step.edgeUpdates[i]);
        }

        if (step.replaceCandidatePath)
        {
            targetState.candidatePath = new List<Vector3>(step.candidatePath);
        }

        if (step.replaceBacktrackPath)
        {
            targetState.backtrackPath = new List<Vector3>(step.backtrackPath);
        }

        if (step.replaceFinalPath)
        {
            targetState.finalPath = new List<Vector3>(step.finalPath);
        }

        if (step.markSearchComplete)
        {
            targetState.searchComplete = true;
            targetState.searchSucceeded = step.markSearchSucceeded;
        }
    }

    private void ApplyAggregateFinalResult()
    {
        List<PathPlanningVisualizationTrace> traces = GetActiveTraceList();
        if (traces == null || traces.Count == 0)
        {
            return;
        }

        runtimeState.Reset();
        int playableSegmentCount = 0;
        for (int traceIndex = 0; traceIndex < traces.Count; traceIndex++)
        {
            PathPlanningVisualizationTrace trace = traces[traceIndex];
            if (trace == null || !trace.HasSteps())
            {
                continue;
            }

            PlaybackRuntimeState segmentFinalState = BuildFinalStateForTrace(trace);
            runtimeState.MergeFrom(segmentFinalState, copyTransientPaths: true);
            playableSegmentCount++;
        }

        List<Vector3> fullPath = BuildAggregateFinalPath(traces);
        if (fullPath.Count >= 2)
        {
            runtimeState.finalPath = fullPath;
            runtimeState.SetNode(PathPlanningVisualizationBuilder.CreateNode(
                fullPath[0],
                PathPlanningVisualizationNodeRole.Start,
                label: "S"));
            runtimeState.SetNode(PathPlanningVisualizationBuilder.CreateNode(
                fullPath[fullPath.Count - 1],
                PathPlanningVisualizationNodeRole.Goal,
                label: "G"));
        }

        ApplyAggregateWaypointNodes(traces, fullPath);

        runtimeState.searchComplete = true;
        runtimeState.searchSucceeded = fullPath.Count >= 2 && AreAllPlayableTracesSuccessful(traces);
        runtimeState.description = fullPath.Count >= 2
            ? $"已快进并定格 {playableSegmentCount} 段规划过程的最后状态，显示最终采样/扩展/拒绝痕迹，并叠加从起飞点到最后任务点的完整路径。"
            : "当前还没有可显示的完整最终路径。";

        currentSequenceCursor = playbackSequence.Count > 0 ? playbackSequence.Count - 1 : -1;
        appliedUnderlyingStepIndex = -1;
        appliedTraceIndex = ResolveCurrentTraceIndex();
        RefreshRenderer();
    }

    private static PlaybackRuntimeState BuildFinalStateForTrace(PathPlanningVisualizationTrace trace)
    {
        PlaybackRuntimeState finalState = new PlaybackRuntimeState();
        if (trace == null || !trace.HasSteps())
        {
            return finalState;
        }

        for (int i = 0; i < trace.steps.Count; i++)
        {
            ApplyStepToState(finalState, trace.steps[i]);
        }

        return finalState;
    }

    private void ApplyAggregateWaypointNodes(
        List<PathPlanningVisualizationTrace> traces,
        List<Vector3> fullPath)
    {
        int playableCount = CountPlayableTraces(traces);
        int segmentNumber = 0;
        bool startMarked = false;

        if (traces != null)
        {
            for (int i = 0; i < traces.Count; i++)
            {
                PathPlanningVisualizationTrace trace = traces[i];
                if (trace == null || !trace.HasSteps())
                {
                    continue;
                }

                segmentNumber++;
                if (!startMarked && TryResolveTraceStart(trace, out Vector3 startPosition))
                {
                    runtimeState.SetNode(PathPlanningVisualizationBuilder.CreateNode(
                        startPosition,
                        PathPlanningVisualizationNodeRole.Start,
                        label: "S"));
                    startMarked = true;
                }

                if (TryResolveTraceGoal(trace, out Vector3 goalPosition))
                {
                    runtimeState.SetNode(PathPlanningVisualizationBuilder.CreateNode(
                        goalPosition,
                        PathPlanningVisualizationNodeRole.Goal,
                        label: segmentNumber == playableCount ? "G" : $"T{segmentNumber}"));
                }
            }
        }

        if (fullPath == null || fullPath.Count < 2)
        {
            return;
        }

        if (!startMarked)
        {
            runtimeState.SetNode(PathPlanningVisualizationBuilder.CreateNode(
                fullPath[0],
                PathPlanningVisualizationNodeRole.Start,
                label: "S"));
        }

        runtimeState.SetNode(PathPlanningVisualizationBuilder.CreateNode(
            fullPath[fullPath.Count - 1],
            PathPlanningVisualizationNodeRole.Goal,
            label: "G"));
    }

    private static bool TryResolveTraceStart(PathPlanningVisualizationTrace trace, out Vector3 position)
    {
        position = Vector3.zero;
        if (trace == null)
        {
            return false;
        }

        if (trace.request != null)
        {
            position = trace.request.startPosition;
            return true;
        }

        if (trace.finalPath != null && trace.finalPath.Count > 0)
        {
            position = trace.finalPath[0];
            return true;
        }

        return false;
    }

    private static bool TryResolveTraceGoal(PathPlanningVisualizationTrace trace, out Vector3 position)
    {
        position = Vector3.zero;
        if (trace == null)
        {
            return false;
        }

        if (trace.request != null)
        {
            position = trace.request.targetPosition;
            return true;
        }

        if (trace.finalPath != null && trace.finalPath.Count > 0)
        {
            position = trace.finalPath[trace.finalPath.Count - 1];
            return true;
        }

        return false;
    }

    private static List<Vector3> BuildAggregateFinalPath(List<PathPlanningVisualizationTrace> traces)
    {
        List<Vector3> fullPath = new List<Vector3>();
        if (traces == null)
        {
            return fullPath;
        }

        for (int i = 0; i < traces.Count; i++)
        {
            PathPlanningVisualizationTrace trace = traces[i];
            List<Vector3> segmentPath = trace != null ? trace.finalPath : null;
            if (segmentPath == null || segmentPath.Count == 0)
            {
                continue;
            }

            for (int pointIndex = 0; pointIndex < segmentPath.Count; pointIndex++)
            {
                Vector3 point = segmentPath[pointIndex];
                if (fullPath.Count > 0 &&
                    Vector3.SqrMagnitude(fullPath[fullPath.Count - 1] - point) <= 0.0001f)
                {
                    continue;
                }

                fullPath.Add(point);
            }
        }

        return fullPath;
    }

    private static bool AreAllPlayableTracesSuccessful(List<PathPlanningVisualizationTrace> traces)
    {
        if (traces == null)
        {
            return false;
        }

        bool hasTrace = false;
        for (int i = 0; i < traces.Count; i++)
        {
            PathPlanningVisualizationTrace trace = traces[i];
            if (trace == null || !trace.HasSteps())
            {
                continue;
            }

            hasTrace = true;
            if (!trace.success)
            {
                return false;
            }
        }

        return hasTrace;
    }

    private static bool ContainsCurrentNodeUpdate(PathPlanningVisualizationStep step)
    {
        if (step == null)
        {
            return false;
        }

        for (int i = 0; i < step.nodeUpdates.Count; i++)
        {
            if (step.nodeUpdates[i] != null &&
                step.nodeUpdates[i].role == PathPlanningVisualizationNodeRole.Current)
            {
                return true;
            }
        }

        return false;
    }

    private void RebuildPlaybackSequence()
    {
        NormalizePlaybackMode();
        playbackSequence.Clear();

        List<PathPlanningVisualizationTrace> traces = GetActiveTraceList();
        if (traces == null || traces.Count == 0)
        {
            return;
        }

        if (visualizationMode == PathPlanningVisualizationMode.FullProcess)
        {
            for (int traceIndex = 0; traceIndex < traces.Count; traceIndex++)
            {
                PathPlanningVisualizationTrace trace = traces[traceIndex];
                if (trace == null || !trace.HasSteps())
                {
                    continue;
                }

                for (int stepIndex = 0; stepIndex < trace.steps.Count; stepIndex++)
                {
                    playbackSequence.Add(new PlaybackStepRef(traceIndex, stepIndex));
                }
            }
            return;
        }

        if (visualizationMode == PathPlanningVisualizationMode.FinalResultOnly)
        {
            for (int traceIndex = 0; traceIndex < traces.Count; traceIndex++)
            {
                PathPlanningVisualizationTrace trace = traces[traceIndex];
                if (trace != null && trace.HasSteps())
                {
                    playbackSequence.Add(new PlaybackStepRef(traceIndex, trace.steps.Count - 1));
                }
            }
            return;
        }

        for (int traceIndex = 0; traceIndex < traces.Count; traceIndex++)
        {
            PathPlanningVisualizationTrace trace = traces[traceIndex];
            if (trace == null || !trace.HasSteps())
            {
                continue;
            }

            int interval = Mathf.Max(1, trace.steps.Count / 24);
            HashSet<int> selectedIndices = new HashSet<int>();
            selectedIndices.Add(0);
            selectedIndices.Add(trace.steps.Count - 1);

            for (int i = 1; i < trace.steps.Count - 1; i++)
            {
                PathPlanningVisualizationStep step = trace.steps[i];
                if (i % interval == 0 ||
                    step.stepType == PathPlanningVisualizationStepType.NodeExpanded ||
                    step.stepType == PathPlanningVisualizationStepType.BacktrackPathUpdated ||
                    step.stepType == PathPlanningVisualizationStepType.FinalPathConfirmed ||
                    step.stepType == PathPlanningVisualizationStepType.SearchFinished)
                {
                    selectedIndices.Add(i);
                }
            }

            List<int> sortedIndices = new List<int>(selectedIndices);
            sortedIndices.Sort();
            for (int i = 0; i < sortedIndices.Count; i++)
            {
                playbackSequence.Add(new PlaybackStepRef(traceIndex, sortedIndices[i]));
            }
        }
    }

    private void RefreshRenderer()
    {
        if (processRenderer == null)
        {
            ApplyObstacleTransparencyState();
            return;
        }

        PathPlanningVisualizationTrace trace = GetCurrentPlaybackTrace();
        if (trace == null)
        {
            processRenderer.ClearVisualization();
            ApplyObstacleTransparencyState();
            return;
        }

        RefreshRendererProjection();
        processRenderer.RenderTrace(trace, runtimeState);
        ApplyObstacleTransparencyState();
    }

    private bool RefreshRendererProjection()
    {
        if (processRenderer == null)
        {
            return false;
        }

        bool useTopDownProjection = cameraManager != null && cameraManager.isTopDown2D;
        float projectionHeight = droneManager != null
            ? droneManager.CalculatePathProjectionHeight() + 0.35f
            : 12f;
        return processRenderer.SetProjectionMode(useTopDownProjection, projectionHeight);
    }

    private void ApplyObstacleTransparencyState()
    {
        if (obstacleTransparencyController == null)
        {
            return;
        }

        bool shouldMakeTransparent =
            obstacleTransparencyEnabled &&
            HasPlayableTrace() &&
            playbackState != PathPlanningVisualizationPlaybackState.Ready;

        obstacleTransparencyController.SetTransparent(shouldMakeTransparent);
    }

    private List<PathPlanningVisualizationTrace> GetActiveTraceList()
    {
        if (selectedDroneId >= 0 &&
            tracesByDroneId.TryGetValue(selectedDroneId, out List<PathPlanningVisualizationTrace> traces))
        {
            return traces;
        }

        if (tracesByDroneId.Count == 0)
        {
            return null;
        }

        foreach (KeyValuePair<int, List<PathPlanningVisualizationTrace>> pair in tracesByDroneId)
        {
            selectedDroneId = pair.Key;
            return pair.Value;
        }

        return null;
    }

    private PathPlanningVisualizationTrace GetCurrentPlaybackTrace()
    {
        if (currentSequenceCursor >= 0 && currentSequenceCursor < playbackSequence.Count)
        {
            PathPlanningVisualizationTrace currentTrace = GetTraceAt(playbackSequence[currentSequenceCursor].traceIndex);
            if (currentTrace != null)
            {
                return currentTrace;
            }
        }

        return GetFirstPlayableTrace();
    }

    private PathPlanningVisualizationTrace GetFirstPlayableTrace()
    {
        List<PathPlanningVisualizationTrace> traces = GetActiveTraceList();
        if (traces == null)
        {
            return null;
        }

        for (int i = 0; i < traces.Count; i++)
        {
            if (traces[i] != null && traces[i].HasSteps())
            {
                return traces[i];
            }
        }

        return null;
    }

    private PathPlanningVisualizationTrace GetTraceAt(int traceIndex)
    {
        List<PathPlanningVisualizationTrace> traces = GetActiveTraceList();
        if (traces == null || traceIndex < 0 || traceIndex >= traces.Count)
        {
            return null;
        }

        return traces[traceIndex];
    }

    private List<PathPlanningVisualizationTrace> GetOrCreateTraceList(int droneId)
    {
        if (!tracesByDroneId.TryGetValue(droneId, out List<PathPlanningVisualizationTrace> traces) || traces == null)
        {
            traces = new List<PathPlanningVisualizationTrace>();
            tracesByDroneId[droneId] = traces;
        }

        return traces;
    }

    private string GetCurrentSegmentLabel()
    {
        List<PathPlanningVisualizationTrace> traces = GetActiveTraceList();
        if (traces == null || traces.Count == 0)
        {
            return "0 / 0";
        }

        int traceIndex = ResolveCurrentTraceIndex();
        int playableCount = CountPlayableTraces(traces);
        int playableIndex = CountPlayableTracesBefore(traces, traceIndex) + 1;
        return $"{Mathf.Clamp(playableIndex, 1, Mathf.Max(1, playableCount))} / {Mathf.Max(1, playableCount)}";
    }

    private int ResolveCurrentTraceIndex()
    {
        if (currentSequenceCursor >= 0 && currentSequenceCursor < playbackSequence.Count)
        {
            return playbackSequence[currentSequenceCursor].traceIndex;
        }

        List<PathPlanningVisualizationTrace> traces = GetActiveTraceList();
        if (traces == null)
        {
            return 0;
        }

        for (int i = 0; i < traces.Count; i++)
        {
            if (traces[i] != null && traces[i].HasSteps())
            {
                return i;
            }
        }

        return 0;
    }

    private static int CountPlayableTraces(List<PathPlanningVisualizationTrace> traces)
    {
        if (traces == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < traces.Count; i++)
        {
            if (traces[i] != null && traces[i].HasSteps())
            {
                count++;
            }
        }

        return count;
    }

    private static int CountPlayableTracesBefore(List<PathPlanningVisualizationTrace> traces, int traceIndex)
    {
        if (traces == null)
        {
            return 0;
        }

        int count = 0;
        int end = Mathf.Clamp(traceIndex, 0, traces.Count);
        for (int i = 0; i < end; i++)
        {
            if (traces[i] != null && traces[i].HasSteps())
            {
                count++;
            }
        }

        return count;
    }

    private bool HasTraceForDrone(int droneId)
    {
        return droneId >= 0 && tracesByDroneId.ContainsKey(droneId);
    }

    private bool HasPlayableTraceForDrone(int droneId)
    {
        if (droneId < 0 || !tracesByDroneId.TryGetValue(droneId, out List<PathPlanningVisualizationTrace> traces))
        {
            return false;
        }

        for (int i = 0; i < traces.Count; i++)
        {
            if (traces[i] != null && traces[i].HasSteps())
            {
                return true;
            }
        }

        return false;
    }

    private List<int> GetSelectableDroneIds()
    {
        List<int> droneIds = new List<int>();
        if (droneManager != null && droneManager.drones != null && droneManager.drones.Count > 0)
        {
            for (int i = 0; i < droneManager.drones.Count; i++)
            {
                DroneController drone = droneManager.drones[i];
                if (drone != null)
                {
                    droneIds.Add(drone.droneId);
                }
            }
        }

        if (droneIds.Count == 0)
        {
            foreach (int droneId in tracesByDroneId.Keys)
            {
                droneIds.Add(droneId);
            }
        }

        droneIds.Sort();
        return droneIds;
    }

    private int ResolveClosestPlaybackSpeedIndex()
    {
        int bestIndex = 0;
        float bestDistance = Mathf.Abs(PlaybackSpeedOptions[0] - playbackSpeed);
        for (int i = 1; i < PlaybackSpeedOptions.Length; i++)
        {
            float distance = Mathf.Abs(PlaybackSpeedOptions[i] - playbackSpeed);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private int ResolvePlaybackModeIndex()
    {
        NormalizePlaybackMode();
        for (int i = 0; i < PlaybackModeOptions.Length; i++)
        {
            if (PlaybackModeOptions[i] == visualizationMode)
            {
                return i;
            }
        }

        return 0;
    }

    private void NormalizePlaybackMode()
    {
        for (int i = 0; i < PlaybackModeOptions.Length; i++)
        {
            if (PlaybackModeOptions[i] == visualizationMode)
            {
                return;
            }
        }

        visualizationMode = PathPlanningVisualizationMode.FullProcess;
    }

    private static Color ResolveDroneAccentColor(int droneId)
    {
        int paletteIndex = Mathf.Abs(droneId) % DroneAccentPalette.Length;
        return DroneAccentPalette[paletteIndex];
    }

    private readonly struct PlaybackStepRef
    {
        public readonly int traceIndex;
        public readonly int stepIndex;

        public PlaybackStepRef(int traceIndex, int stepIndex)
        {
            this.traceIndex = traceIndex;
            this.stepIndex = stepIndex;
        }
    }

    public sealed class PlaybackRuntimeState
    {
        private readonly Dictionary<string, PathPlanningVisualizationNodeState> nodesByKey = new Dictionary<string, PathPlanningVisualizationNodeState>();
        private readonly Dictionary<string, PathPlanningVisualizationEdgeState> edgesByKey = new Dictionary<string, PathPlanningVisualizationEdgeState>();
        private readonly Queue<string> rejectedEdgeOrder = new Queue<string>();
        private const int MaxRejectedEdges = 256;

        public List<Vector3> candidatePath = new List<Vector3>();
        public List<Vector3> backtrackPath = new List<Vector3>();
        public List<Vector3> finalPath = new List<Vector3>();
        public string description = string.Empty;
        public bool searchComplete;
        public bool searchSucceeded;

        public IEnumerable<PathPlanningVisualizationNodeState> Nodes => nodesByKey.Values;
        public IEnumerable<PathPlanningVisualizationEdgeState> Edges => edgesByKey.Values;

        public void Reset()
        {
            nodesByKey.Clear();
            edgesByKey.Clear();
            rejectedEdgeOrder.Clear();
            candidatePath.Clear();
            backtrackPath.Clear();
            finalPath.Clear();
            description = string.Empty;
            searchComplete = false;
            searchSucceeded = false;
        }

        public void SetNode(PathPlanningVisualizationNodeState state)
        {
            if (state == null)
            {
                return;
            }

            nodesByKey[BuildNodeKey(state.position)] = state.Clone();
        }

        public void DemoteCurrentNodesToVisited()
        {
            List<string> keys = new List<string>(nodesByKey.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                PathPlanningVisualizationNodeState node = nodesByKey[keys[i]];
                if (node.role != PathPlanningVisualizationNodeRole.Current)
                {
                    continue;
                }

                node.role = PathPlanningVisualizationNodeRole.Visited;
                node.label = string.Empty;
                nodesByKey[keys[i]] = node;
            }
        }

        public void SetEdge(PathPlanningVisualizationEdgeState state)
        {
            if (state == null)
            {
                return;
            }

            string key = BuildEdgeKey(state.from, state.to);
            edgesByKey[key] = state.Clone();

            if (state.role != PathPlanningVisualizationEdgeRole.Rejected)
            {
                return;
            }

            rejectedEdgeOrder.Enqueue(key);
            while (rejectedEdgeOrder.Count > MaxRejectedEdges)
            {
                string oldestKey = rejectedEdgeOrder.Dequeue();
                if (edgesByKey.TryGetValue(oldestKey, out PathPlanningVisualizationEdgeState edge) &&
                    edge.role == PathPlanningVisualizationEdgeRole.Rejected)
                {
                    edgesByKey.Remove(oldestKey);
                }
            }
        }

        public void ClearRejectedEdges()
        {
            List<string> keysToRemove = new List<string>();
            foreach (KeyValuePair<string, PathPlanningVisualizationEdgeState> pair in edgesByKey)
            {
                if (pair.Value.role == PathPlanningVisualizationEdgeRole.Rejected)
                {
                    keysToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                edgesByKey.Remove(keysToRemove[i]);
            }

            rejectedEdgeOrder.Clear();
        }

        public void MergeFrom(PlaybackRuntimeState other, bool copyTransientPaths)
        {
            if (other == null)
            {
                return;
            }

            foreach (PathPlanningVisualizationNodeState node in other.Nodes)
            {
                SetNode(node);
            }

            foreach (PathPlanningVisualizationEdgeState edge in other.Edges)
            {
                SetEdge(edge);
            }

            if (!copyTransientPaths)
            {
                return;
            }

            if (other.candidatePath != null && other.candidatePath.Count > 0)
            {
                candidatePath = new List<Vector3>(other.candidatePath);
            }

            if (other.backtrackPath != null && other.backtrackPath.Count > 0)
            {
                backtrackPath = new List<Vector3>(other.backtrackPath);
            }

            if (other.finalPath != null && other.finalPath.Count > 0)
            {
                finalPath = new List<Vector3>(other.finalPath);
            }

            if (!string.IsNullOrWhiteSpace(other.description))
            {
                description = other.description;
            }

            if (other.searchComplete)
            {
                searchComplete = true;
                searchSucceeded = other.searchSucceeded;
            }
        }

        public int CountNodes(PathPlanningVisualizationNodeRole role)
        {
            int count = 0;
            foreach (PathPlanningVisualizationNodeState node in nodesByKey.Values)
            {
                if (node.role == role)
                {
                    count++;
                }
            }

            return count;
        }

        private static string BuildNodeKey(Vector3 position)
        {
            return $"{Mathf.RoundToInt(position.x * 100f)}_{Mathf.RoundToInt(position.y * 100f)}_{Mathf.RoundToInt(position.z * 100f)}";
        }

        private static string BuildEdgeKey(Vector3 from, Vector3 to)
        {
            return $"{BuildNodeKey(from)}__{BuildNodeKey(to)}";
        }
    }
}
