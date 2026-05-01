using UnityEngine;

public class RLTrainingMapBoundsVisualizer : MonoBehaviour
{
    [Header("References")]
    public DroneManager droneManager;

    [Header("Display")]
    public bool showBounds = true;
    public Color boundsColor = new Color(0.18f, 0.85f, 0.92f, 0.95f);
    public float lineWidth = 0.12f;
    public float heightOffset = 0.18f;

    private LineRenderer boundsRenderer;

    private void LateUpdate()
    {
        CacheReferences();
        EnsureRenderer();
        UpdateBoundsRenderer();
    }

    private void CacheReferences()
    {
        droneManager = RuntimeSceneRegistry.Resolve(droneManager, this);
    }

    private void EnsureRenderer()
    {
        if (boundsRenderer != null)
        {
            return;
        }

        GameObject lineObject = new GameObject("RLTrainingMapBounds");
        lineObject.transform.SetParent(transform, false);
        boundsRenderer = lineObject.AddComponent<LineRenderer>();
        boundsRenderer.useWorldSpace = true;
        boundsRenderer.loop = false;
        boundsRenderer.positionCount = 5;
        boundsRenderer.alignment = LineAlignment.View;
        boundsRenderer.numCapVertices = 4;
        boundsRenderer.numCornerVertices = 4;
        boundsRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        boundsRenderer.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        boundsRenderer.material = new Material(shader);
    }

    private void UpdateBoundsRenderer()
    {
        if (boundsRenderer == null)
        {
            return;
        }

        bool visible = showBounds && droneManager != null;
        boundsRenderer.enabled = visible;
        if (!visible)
        {
            return;
        }

        Vector3 min = droneManager.planningWorldMin;
        Vector3 max = droneManager.planningWorldMax;
        float y = min.y + heightOffset;

        boundsRenderer.startColor = boundsColor;
        boundsRenderer.endColor = boundsColor;
        boundsRenderer.startWidth = lineWidth;
        boundsRenderer.endWidth = lineWidth;
        boundsRenderer.SetPosition(0, new Vector3(min.x, y, min.z));
        boundsRenderer.SetPosition(1, new Vector3(max.x, y, min.z));
        boundsRenderer.SetPosition(2, new Vector3(max.x, y, max.z));
        boundsRenderer.SetPosition(3, new Vector3(min.x, y, max.z));
        boundsRenderer.SetPosition(4, new Vector3(min.x, y, min.z));
    }
}
