using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Visualizes scheduling results by drawing each drone's remaining task queue in the scene.
/// </summary>
public class TaskQueueVisualizer : MonoBehaviour
{
    private static readonly Color[] DroneQueuePalette =
    {
        new Color(0.20f, 0.86f, 1.00f, 0.95f),
        new Color(1.00f, 0.66f, 0.22f, 0.95f),
        new Color(0.48f, 1.00f, 0.52f, 0.95f),
        new Color(1.00f, 0.44f, 0.44f, 0.95f),
        new Color(0.82f, 0.56f, 1.00f, 0.95f),
        new Color(1.00f, 0.82f, 0.28f, 0.95f)
    };

    [Header("References")]
    public SimulationManager simulationManager;
    public DroneManager droneManager;
    public CameraManager cameraManager;
    public Camera labelCamera;

    [Header("Visibility")]
    public bool showTaskQueues = true;
    public bool showCompletedTaskLabels = true;

    [Header("Line Style")]
    public float routeLineWidth = 0.16f;
    public float markerRadius = 0.38f;
    public float worldLineHeightOffset = 0.38f;
    public float topDownHeightOffset = 0.85f;

    [Header("Label Style")]
    public float labelFontSize = 3.2f;
    public float labelWorldYOffset = 1.1f;
    public Color completedLabelColor = new Color(0.72f, 0.78f, 0.82f, 0.45f);

    private readonly Dictionary<int, LineRenderer> routeRenderers = new Dictionary<int, LineRenderer>();
    private readonly Dictionary<string, QueueLabel> labelsByKey = new Dictionary<string, QueueLabel>();
    private readonly List<string> inactiveLabelKeys = new List<string>();
    private Material lineMaterial;
    private Material markerMaterialTemplate;
    private Transform visualizationRoot;

    public bool ShowTaskQueues => showTaskQueues;

    private void Awake()
    {
        RuntimeSceneRegistry.Register(this);
        EnsureRoot();
        EnsureMaterials();
    }

    private void LateUpdate()
    {
        CacheReferences();
        RenderTaskQueues();
    }

    private void OnDisable()
    {
        ClearVisualization();
    }

    public void SetVisible(bool visible)
    {
        showTaskQueues = visible;
        if (!showTaskQueues)
        {
            ClearVisualization();
        }
    }

    public string BuildSchedulingSummaryText()
    {
        CacheReferences();
        if (droneManager == null || droneManager.droneDataList == null || droneManager.droneDataList.Count == 0)
        {
            return "调度结果：暂无无人机数据";
        }

        StringBuilder builder = new StringBuilder(512);
        builder.Append("调度算法: ")
            .Append(UAVAlgorithmNames.GetSchedulerDisplayName(droneManager.schedulerAlgorithm))
            .AppendLine();

        if (simulationManager != null && simulationManager.currentState == SimulationState.Idle)
        {
            builder.Append("调度结果: 点击开始仿真后显示任务分配队列。");
            return builder.ToString();
        }

        int assignedTaskCount = 0;
        int completedTaskCount = 0;
        int activeDroneCount = 0;

        for (int i = 0; i < droneManager.droneDataList.Count; i++)
        {
            DroneData data = droneManager.droneDataList[i];
            if (data == null)
            {
                continue;
            }

            int queueLength = data.taskQueue != null ? data.taskQueue.Length : 0;
            assignedTaskCount += queueLength;
            completedTaskCount += Mathf.Clamp(data.completedTasks, 0, queueLength);
            if (queueLength > 0)
            {
                activeDroneCount++;
            }
        }

        builder.Append("分配概况: ")
            .Append(assignedTaskCount)
            .Append(" 个任务 / ")
            .Append(activeDroneCount)
            .Append(" 架无人机")
            .Append("，已完成 ")
            .Append(completedTaskCount)
            .Append('/')
            .Append(assignedTaskCount)
            .AppendLine();

        bool hasAnyQueue = false;
        for (int i = 0; i < droneManager.droneDataList.Count; i++)
        {
            DroneData data = droneManager.droneDataList[i];
            if (data == null)
            {
                continue;
            }

            builder.Append('[').Append(data.droneId.ToString("D2")).Append("] ")
                .Append(string.IsNullOrWhiteSpace(data.droneName) ? $"无人机{data.droneId:D2}" : data.droneName)
                .Append(" | ")
                .Append(FormatDroneState(data.state))
                .Append(" | ");

            if (data.taskQueue == null || data.taskQueue.Length == 0)
            {
                builder.Append("未分配任务");
            }
            else
            {
                hasAnyQueue = true;
                int current = Mathf.Clamp(data.currentTaskIndex + 1, 1, data.taskQueue.Length);
                builder.Append("进度 ")
                    .Append(current)
                    .Append('/')
                    .Append(data.taskQueue.Length)
                    .Append(" | ")
                    .Append(BuildTaskQueueLabel(data));
            }

            if (i < droneManager.droneDataList.Count - 1)
            {
                builder.AppendLine();
            }
        }

        if (!hasAnyQueue)
        {
            builder.AppendLine();
            builder.Append("提示: 开始仿真后会显示每架无人机的任务分配队列。");
        }

        return builder.ToString();
    }

    private void RenderTaskQueues()
    {
        if (!showTaskQueues ||
            droneManager == null ||
            droneManager.droneDataList == null ||
            (simulationManager != null && simulationManager.currentState == SimulationState.Idle))
        {
            ClearVisualization();
            return;
        }

        EnsureRoot();
        EnsureMaterials();
        MarkLabelsInactive();

        HashSet<int> activeDroneIds = new HashSet<int>();
        for (int i = 0; i < droneManager.droneDataList.Count; i++)
        {
            DroneData data = droneManager.droneDataList[i];
            if (data == null)
            {
                continue;
            }

            activeDroneIds.Add(data.droneId);
            RenderDroneQueue(data);
        }

        HideInactiveDroneRoutes(activeDroneIds);
        DisableInactiveLabels();
    }

    private void RenderDroneQueue(DroneData data)
    {
        LineRenderer routeRenderer = GetRouteRenderer(data.droneId);
        Color accentColor = ResolveDroneColor(data.droneId);

        List<Vector3> routePoints = BuildRemainingRoutePoints(data);
        routeRenderer.startColor = accentColor;
        routeRenderer.endColor = accentColor;
        routeRenderer.startWidth = routeLineWidth;
        routeRenderer.endWidth = routeLineWidth;
        routeRenderer.positionCount = routePoints.Count;
        for (int i = 0; i < routePoints.Count; i++)
        {
            routeRenderer.SetPosition(i, routePoints[i]);
        }

        if (data.taskQueue == null)
        {
            return;
        }

        for (int i = 0; i < data.taskQueue.Length; i++)
        {
            TaskPoint taskPoint = data.taskQueue[i];
            if (taskPoint == null)
            {
                continue;
            }

            bool completed = i < data.currentTaskIndex || taskPoint.currentState == TaskState.Completed;
            if (completed && !showCompletedTaskLabels)
            {
                continue;
            }

            string key = BuildLabelKey(data.droneId, taskPoint, i);
            QueueLabel label = GetQueueLabel(key);
            label.root.SetActive(true);
            label.text.text = $"U{data.droneId:D2}-{i + 1}";
            label.text.color = completed ? completedLabelColor : Color.white;

            Color markerColor = completed ? completedLabelColor : accentColor;
            label.markerRenderer.sharedMaterial.color = markerColor;
            label.root.transform.position = BuildLabelPosition(taskPoint.transform.position);
            ScaleLabel(label, completed ? 0.72f : 1f);
            FaceCamera(label.root.transform);
            inactiveLabelKeys.Remove(key);
        }
    }

    private List<Vector3> BuildRemainingRoutePoints(DroneData data)
    {
        List<Vector3> points = new List<Vector3>();
        DroneController drone = droneManager.GetDrone(data.droneId);
        if (drone != null)
        {
            points.Add(BuildLinePoint(drone.transform.position));
        }

        if (data.taskQueue == null)
        {
            return points;
        }

        int startIndex = Mathf.Clamp(data.currentTaskIndex, 0, data.taskQueue.Length);
        for (int i = startIndex; i < data.taskQueue.Length; i++)
        {
            TaskPoint taskPoint = data.taskQueue[i];
            if (taskPoint != null && taskPoint.currentState != TaskState.Completed)
            {
                points.Add(BuildLinePoint(taskPoint.transform.position));
            }
        }

        if (points.Count <= 1)
        {
            points.Clear();
        }

        return points;
    }

    private Vector3 BuildLinePoint(Vector3 worldPoint)
    {
        if (cameraManager != null && cameraManager.isTopDown2D)
        {
            float projectionHeight = droneManager != null
                ? droneManager.CalculatePathProjectionHeight() + topDownHeightOffset
                : worldPoint.y + topDownHeightOffset;
            return new Vector3(worldPoint.x, projectionHeight, worldPoint.z);
        }

        return worldPoint + Vector3.up * worldLineHeightOffset;
    }

    private Vector3 BuildLabelPosition(Vector3 worldPoint)
    {
        Vector3 linePoint = BuildLinePoint(worldPoint);
        return linePoint + Vector3.up * labelWorldYOffset;
    }

    private LineRenderer GetRouteRenderer(int droneId)
    {
        if (routeRenderers.TryGetValue(droneId, out LineRenderer renderer) && renderer != null)
        {
            renderer.gameObject.SetActive(true);
            return renderer;
        }

        GameObject routeObject = new GameObject($"TaskQueueRoute_{droneId:D2}");
        routeObject.transform.SetParent(visualizationRoot, false);
        renderer = routeObject.AddComponent<LineRenderer>();
        renderer.useWorldSpace = true;
        renderer.loop = false;
        renderer.alignment = LineAlignment.View;
        renderer.numCapVertices = 4;
        renderer.numCornerVertices = 4;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.material = lineMaterial;
        renderer.sortingOrder = 18;
        routeRenderers[droneId] = renderer;
        return renderer;
    }

    private QueueLabel GetQueueLabel(string key)
    {
        if (labelsByKey.TryGetValue(key, out QueueLabel label) && label != null)
        {
            return label;
        }

        label = CreateQueueLabel(key);
        labelsByKey[key] = label;
        return label;
    }

    private QueueLabel CreateQueueLabel(string key)
    {
        GameObject root = new GameObject($"TaskQueueLabel_{key}");
        root.transform.SetParent(visualizationRoot, false);

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Marker";
        marker.transform.SetParent(root.transform, false);
        marker.transform.localScale = Vector3.one * markerRadius;
        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            DestroyRuntimeObject(markerCollider);
        }

        MeshRenderer markerRenderer = marker.GetComponent<MeshRenderer>();
        markerRenderer.material = new Material(markerMaterialTemplate);

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(root.transform, false);
        textObject.transform.localPosition = new Vector3(0f, markerRadius * 1.35f, 0f);
        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = labelFontSize;
        text.enableWordWrapping = false;
        text.color = Color.white;
        text.text = string.Empty;

        return new QueueLabel
        {
            root = root,
            markerRenderer = markerRenderer,
            text = text
        };
    }

    private void CacheReferences()
    {
        simulationManager = RuntimeSceneRegistry.Resolve(simulationManager, this);
        droneManager = RuntimeSceneRegistry.Resolve(droneManager, this);
        cameraManager = RuntimeSceneRegistry.Resolve(cameraManager, this);
        if (labelCamera == null)
        {
            labelCamera = Camera.main;
        }
    }

    private void EnsureRoot()
    {
        if (visualizationRoot != null)
        {
            return;
        }

        visualizationRoot = new GameObject("__TaskQueueVisualization").transform;
        visualizationRoot.SetParent(transform, false);
    }

    private void EnsureMaterials()
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            lineMaterial = new Material(shader)
            {
                name = "Task Queue Line Material"
            };
        }

        if (markerMaterialTemplate == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            markerMaterialTemplate = new Material(shader)
            {
                name = "Task Queue Marker Material"
            };
        }
    }

    private void MarkLabelsInactive()
    {
        inactiveLabelKeys.Clear();
        foreach (string key in labelsByKey.Keys)
        {
            inactiveLabelKeys.Add(key);
        }
    }

    private void DisableInactiveLabels()
    {
        for (int i = 0; i < inactiveLabelKeys.Count; i++)
        {
            string key = inactiveLabelKeys[i];
            if (labelsByKey.TryGetValue(key, out QueueLabel label) && label != null && label.root != null)
            {
                label.root.SetActive(false);
            }
        }
    }

    private void HideInactiveDroneRoutes(HashSet<int> activeDroneIds)
    {
        foreach (KeyValuePair<int, LineRenderer> pair in routeRenderers)
        {
            if (pair.Value == null)
            {
                continue;
            }

            if (!activeDroneIds.Contains(pair.Key))
            {
                pair.Value.positionCount = 0;
            }
        }
    }

    private void ClearVisualization()
    {
        foreach (KeyValuePair<int, LineRenderer> pair in routeRenderers)
        {
            if (pair.Value != null)
            {
                pair.Value.positionCount = 0;
            }
        }

        foreach (KeyValuePair<string, QueueLabel> pair in labelsByKey)
        {
            if (pair.Value != null && pair.Value.root != null)
            {
                pair.Value.root.SetActive(false);
            }
        }
    }

    private void FaceCamera(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (labelCamera == null)
        {
            labelCamera = Camera.main;
        }

        if (labelCamera != null)
        {
            target.rotation = labelCamera.transform.rotation;
        }
    }

    private void ScaleLabel(QueueLabel label, float scale)
    {
        if (label == null || label.root == null)
        {
            return;
        }

        label.root.transform.localScale = Vector3.one * scale;
    }

    private string BuildTaskQueueLabel(DroneData data)
    {
        if (data == null || data.taskQueue == null || data.taskQueue.Length == 0)
        {
            return "-";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < data.taskQueue.Length; i++)
        {
            TaskPoint taskPoint = data.taskQueue[i];
            if (taskPoint == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(" -> ");
            }

            if (i < data.currentTaskIndex || taskPoint.currentState == TaskState.Completed)
            {
                builder.Append('√');
            }
            else if (i == data.currentTaskIndex)
            {
                builder.Append('*');
            }

            builder.Append(FormatTaskName(taskPoint));
        }

        return builder.Length > 0 ? builder.ToString() : "-";
    }

    private static string FormatTaskName(TaskPoint taskPoint)
    {
        if (taskPoint == null)
        {
            return "-";
        }

        if (!string.IsNullOrWhiteSpace(taskPoint.taskName))
        {
            return taskPoint.taskName;
        }

        return $"T{taskPoint.taskId:D2}";
    }

    private static string BuildLabelKey(int droneId, TaskPoint taskPoint, int taskIndex)
    {
        int taskId = taskPoint != null ? taskPoint.taskId : -1;
        return $"{droneId:D2}_{taskId:D3}_{taskIndex:D2}";
    }

    private static Color ResolveDroneColor(int droneId)
    {
        int index = Mathf.Abs(droneId) % DroneQueuePalette.Length;
        return DroneQueuePalette[index];
    }

    private static string FormatDroneState(DroneState state)
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

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private sealed class QueueLabel
    {
        public GameObject root;
        public MeshRenderer markerRenderer;
        public TextMeshPro text;
    }
}
