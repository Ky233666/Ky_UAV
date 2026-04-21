using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visualizes planned paths and actual flight trails for each drone.
/// </summary>
public class DronePathVisualizer : MonoBehaviour
{
    [Header("References")]
    public DroneController droneController;
    public DroneData droneData;

    [Header("Visibility")]
    public bool showPlannedPath = true;
    public bool showTrail = true;

    [Header("Projection")]
    public bool useTopDownProjection = false;
    public float topDownProjectionHeight = 12f;

    [Header("Top Down Alerts")]
    public Color buildingAlertColor = new Color(1f, 0.22f, 0.22f, 0.98f);
    public float alertWidthMultiplier = 1.35f;

    [Header("Planned Path")]
    public Color plannedPathColor = new Color(0.2f, 0.9f, 1f, 0.95f);
    public float plannedPathWidth = 0.18f;

    [Header("Flight Trail")]
    public Color trailColor = new Color(1f, 0.55f, 0.15f, 0.9f);
    public float trailWidth = 0.1f;
    public float trailPointSpacing = 0.5f;
    public int maxTrailPoints = 256;

    private readonly List<Vector3> trailWorldPoints = new List<Vector3>();
    private readonly List<Vector3> inspectionPoints = new List<Vector3>();
    private LineRenderer plannedPathRenderer;
    private LineRenderer trailRenderer;
    private bool hasBuildingOverlapAlert;
    private bool hasBuildingCrossingAlert;
    private bool lastLoggedBuildingAlertState;

    public bool HasBuildingAlert => hasBuildingOverlapAlert || hasBuildingCrossingAlert;
    public bool HasBuildingOverlapAlert => hasBuildingOverlapAlert;
    public bool HasBuildingCrossingAlert => hasBuildingCrossingAlert;

    void Awake()
    {
        if (droneController == null)
        {
            droneController = GetComponent<DroneController>();
        }

        EnsureRenderers();
        ResetVisuals();
    }

    void LateUpdate()
    {
        if (droneController == null)
        {
            return;
        }

        if (droneData == null && droneController.stateMachine != null)
        {
            droneData = droneController.stateMachine.droneData;
        }

        UpdatePlannedPathRenderer();
        UpdateTrailRenderer();
        EvaluateBuildingAlerts();
        ApplyDynamicStyle();
    }

    public void ResetVisuals()
    {
        trailWorldPoints.Clear();
        if (droneController != null)
        {
            trailWorldPoints.Add(droneController.transform.position);
        }

        if (plannedPathRenderer != null)
        {
            plannedPathRenderer.positionCount = 0;
        }

        if (trailRenderer != null)
        {
            trailRenderer.positionCount = trailWorldPoints.Count;
            if (trailWorldPoints.Count > 0)
            {
                trailRenderer.SetPosition(0, BuildDisplayPoint(trailWorldPoints[0], 0.06f));
            }
        }

        hasBuildingOverlapAlert = false;
        hasBuildingCrossingAlert = false;
        lastLoggedBuildingAlertState = false;
        ApplyVisibility();
    }

    public void SetVisibility(bool plannedVisible, bool trailVisible)
    {
        showPlannedPath = plannedVisible;
        showTrail = trailVisible;
        ApplyVisibility();
    }

    public void SetProjectionMode(bool enabled, float projectionHeight)
    {
        useTopDownProjection = enabled;
        topDownProjectionHeight = projectionHeight;
    }

    private void EnsureRenderers()
    {
        plannedPathRenderer = CreateRenderer("PlannedPath", plannedPathColor, plannedPathWidth, 10);
        trailRenderer = CreateRenderer("FlightTrail", trailColor, trailWidth, 5);
        ApplyVisibility();
    }

    private LineRenderer CreateRenderer(string childName, Color color, float width, int sortingOrder)
    {
        Transform child = transform.Find(childName);
        GameObject lineObject = child != null ? child.gameObject : new GameObject(childName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer renderer = lineObject.GetComponent<LineRenderer>();
        if (renderer == null)
        {
            renderer = lineObject.AddComponent<LineRenderer>();
        }

        renderer.useWorldSpace = true;
        renderer.loop = false;
        renderer.alignment = LineAlignment.View;
        renderer.numCapVertices = 4;
        renderer.numCornerVertices = 4;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.material = CreateLineMaterial();
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private Material CreateLineMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        return new Material(shader);
    }

    private void UpdatePlannedPathRenderer()
    {
        if (plannedPathRenderer == null)
        {
            return;
        }

        if (!showPlannedPath)
        {
            plannedPathRenderer.positionCount = 0;
            return;
        }

        if (droneData == null || droneData.plannedPath == null || droneData.plannedPath.Count == 0)
        {
            plannedPathRenderer.positionCount = 0;
            return;
        }

        int startIndex = Mathf.Clamp(droneData.currentWaypointIndex, 0, droneData.plannedPath.Count - 1);
        int pointCount = 1 + (droneData.plannedPath.Count - startIndex);
        plannedPathRenderer.positionCount = pointCount;
        plannedPathRenderer.SetPosition(0, BuildDisplayPoint(droneController.transform.position, 0.12f));

        for (int i = startIndex; i < droneData.plannedPath.Count; i++)
        {
            plannedPathRenderer.SetPosition(i - startIndex + 1, BuildDisplayPoint(droneData.plannedPath[i], 0.12f));
        }
    }

    private void UpdateTrailRenderer()
    {
        if (trailRenderer == null)
        {
            return;
        }

        if (!showTrail)
        {
            trailRenderer.positionCount = 0;
            return;
        }

        Vector3 currentPosition = droneController.transform.position;
        if (trailWorldPoints.Count == 0)
        {
            trailWorldPoints.Add(currentPosition);
        }
        else if (Vector3.Distance(trailWorldPoints[trailWorldPoints.Count - 1], currentPosition) >= trailPointSpacing)
        {
            trailWorldPoints.Add(currentPosition);
            if (trailWorldPoints.Count > maxTrailPoints)
            {
                trailWorldPoints.RemoveAt(0);
            }
        }

        trailRenderer.positionCount = trailWorldPoints.Count;
        for (int i = 0; i < trailWorldPoints.Count; i++)
        {
            trailRenderer.SetPosition(i, BuildDisplayPoint(trailWorldPoints[i], 0.06f));
        }
    }

    private Vector3 BuildDisplayPoint(Vector3 worldPoint, float worldOffsetY)
    {
        if (useTopDownProjection)
        {
            return new Vector3(worldPoint.x, topDownProjectionHeight + worldOffsetY, worldPoint.z);
        }

        return worldPoint + Vector3.up * worldOffsetY;
    }

    private void EvaluateBuildingAlerts()
    {
        hasBuildingOverlapAlert = false;
        hasBuildingCrossingAlert = false;

        if (!useTopDownProjection || DroneManager.Instance == null || droneController == null)
        {
            return;
        }

        hasBuildingOverlapAlert = DroneManager.Instance.IsPointInsideObstacleFootprint(droneController.transform.position);

        inspectionPoints.Clear();
        inspectionPoints.Add(droneController.transform.position);

        if (droneData != null && droneData.plannedPath != null && droneData.plannedPath.Count > 0)
        {
            int startIndex = Mathf.Clamp(droneData.currentWaypointIndex, 0, droneData.plannedPath.Count - 1);
            for (int i = startIndex; i < droneData.plannedPath.Count; i++)
            {
                inspectionPoints.Add(droneData.plannedPath[i]);
            }
        }

        if (inspectionPoints.Count > 1 && DroneManager.Instance.DoesPolylineCrossObstacleFootprint(inspectionPoints))
        {
            hasBuildingCrossingAlert = true;
            return;
        }

        if (trailWorldPoints.Count > 1 && DroneManager.Instance.DoesPolylineCrossObstacleFootprint(trailWorldPoints))
        {
            hasBuildingCrossingAlert = true;
        }

        bool hasAlert = HasBuildingAlert;
        if (hasAlert != lastLoggedBuildingAlertState)
        {
            lastLoggedBuildingAlertState = hasAlert;
            if (hasAlert)
            {
                string reason = hasBuildingOverlapAlert ? "当前位置进入建筑投影" : "轨迹穿越建筑投影";
                Debug.LogWarning($"[DronePathVisualizer] {droneController.droneName} 触发建筑告警：{reason}");
            }
            else if (droneController != null)
            {
                Debug.Log($"[DronePathVisualizer] {droneController.droneName} 已解除建筑告警");
            }
        }
    }

    private void ApplyDynamicStyle()
    {
        bool useAlertStyle = HasBuildingAlert;

        if (plannedPathRenderer != null)
        {
            Color plannedColor = useAlertStyle ? buildingAlertColor : plannedPathColor;
            plannedPathRenderer.startColor = plannedColor;
            plannedPathRenderer.endColor = plannedColor;

            float width = plannedPathWidth * (useAlertStyle ? alertWidthMultiplier : 1f);
            plannedPathRenderer.startWidth = width;
            plannedPathRenderer.endWidth = width;
        }

        if (trailRenderer != null)
        {
            Color currentTrailColor = useAlertStyle ? buildingAlertColor : trailColor;
            trailRenderer.startColor = currentTrailColor;
            trailRenderer.endColor = currentTrailColor;

            float width = trailWidth * (useAlertStyle ? alertWidthMultiplier : 1f);
            trailRenderer.startWidth = width;
            trailRenderer.endWidth = width;
        }
    }

    private void ApplyVisibility()
    {
        if (plannedPathRenderer != null)
        {
            plannedPathRenderer.enabled = showPlannedPath;
            if (!showPlannedPath)
            {
                plannedPathRenderer.positionCount = 0;
            }
        }

        if (trailRenderer != null)
        {
            trailRenderer.enabled = showTrail;
            if (!showTrail)
            {
                trailRenderer.positionCount = 0;
            }
        }
    }
}
