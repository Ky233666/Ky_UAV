using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无人机路径可视化：
/// 1. 显示当前规划路径
/// 2. 显示实际飞行轨迹
/// </summary>
public class DronePathVisualizer : MonoBehaviour
{
    [Header("关联对象")]
    public DroneController droneController;
    public DroneData droneData;

    [Header("规划路径")]
    public Color plannedPathColor = new Color(0.2f, 0.9f, 1f, 0.95f);
    public float plannedPathWidth = 0.18f;

    [Header("飞行轨迹")]
    public Color trailColor = new Color(1f, 0.55f, 0.15f, 0.9f);
    public float trailWidth = 0.1f;
    public float trailPointSpacing = 0.5f;
    public int maxTrailPoints = 256;

    private readonly List<Vector3> trailPoints = new List<Vector3>();
    private LineRenderer plannedPathRenderer;
    private LineRenderer trailRenderer;

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
    }

    public void ResetVisuals()
    {
        trailPoints.Clear();
        if (droneController != null)
        {
            trailPoints.Add(droneController.transform.position);
        }

        if (plannedPathRenderer != null)
        {
            plannedPathRenderer.positionCount = 0;
        }

        if (trailRenderer != null)
        {
            trailRenderer.positionCount = trailPoints.Count;
            if (trailPoints.Count > 0)
            {
                trailRenderer.SetPosition(0, trailPoints[0]);
            }
        }
    }

    private void EnsureRenderers()
    {
        plannedPathRenderer = CreateRenderer("PlannedPath", plannedPathColor, plannedPathWidth, 10);
        trailRenderer = CreateRenderer("FlightTrail", trailColor, trailWidth, 5);
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

        if (droneData == null || droneData.plannedPath == null || droneData.plannedPath.Count == 0)
        {
            plannedPathRenderer.positionCount = 0;
            return;
        }

        int startIndex = Mathf.Clamp(droneData.currentWaypointIndex, 0, droneData.plannedPath.Count - 1);
        int pointCount = 1 + (droneData.plannedPath.Count - startIndex);
        plannedPathRenderer.positionCount = pointCount;
        plannedPathRenderer.SetPosition(0, droneController.transform.position + Vector3.up * 0.12f);

        for (int i = startIndex; i < droneData.plannedPath.Count; i++)
        {
            plannedPathRenderer.SetPosition(i - startIndex + 1, droneData.plannedPath[i] + Vector3.up * 0.12f);
        }
    }

    private void UpdateTrailRenderer()
    {
        if (trailRenderer == null)
        {
            return;
        }

        Vector3 currentPosition = droneController.transform.position + Vector3.up * 0.06f;
        if (trailPoints.Count == 0)
        {
            trailPoints.Add(currentPosition);
        }
        else if (Vector3.Distance(trailPoints[trailPoints.Count - 1], currentPosition) >= trailPointSpacing)
        {
            trailPoints.Add(currentPosition);
            if (trailPoints.Count > maxTrailPoints)
            {
                trailPoints.RemoveAt(0);
            }
        }

        trailRenderer.positionCount = trailPoints.Count;
        for (int i = 0; i < trailPoints.Count; i++)
        {
            trailRenderer.SetPosition(i, trailPoints[i]);
        }
    }
}
