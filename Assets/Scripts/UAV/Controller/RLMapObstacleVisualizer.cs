using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RLMapObstacleVisualizer : MonoBehaviour
{
    [Header("References")]
    public DroneManager droneManager;

    [Header("Display")]
    public bool showObstacleCells = true;
    public Color obstacleCellColor = new Color(0.92f, 0.26f, 0.18f, 0.92f);
    public float obstacleCellHeight = 0.35f;
    public float obstacleCellScale = 0.92f;
    public float obstacleCellYOffset = 0.18f;

    public string LastPreviewMessage { get; private set; } = "RL map obstacle preview not loaded";

    private Transform previewRoot;
    private Material obstacleMaterial;
    private readonly List<GameObject> obstacleCells = new List<GameObject>();

    [ContextMenu("Show Default RL Map Obstacles")]
    public void ShowDefaultMapObstacles()
    {
        ShowMapObstacles(RLPathPlanningFileUtility.GetDefaultMapPath());
    }

    public bool ShowMapObstacles(string mapPath)
    {
        CacheReferences();
        ClearPreview();

        if (!showObstacleCells)
        {
            LastPreviewMessage = "RL obstacle preview is disabled";
            return false;
        }

        if (string.IsNullOrWhiteSpace(mapPath) || !File.Exists(mapPath))
        {
            LastPreviewMessage = $"RL map file not found: {mapPath}";
            Debug.LogWarning($"[RLMapObstacleVisualizer] {LastPreviewMessage}");
            return false;
        }

        try
        {
            RLMapJson map = JsonUtility.FromJson<RLMapJson>(File.ReadAllText(mapPath));
            if (map == null)
            {
                LastPreviewMessage = "RL map JSON parse failed";
                Debug.LogWarning($"[RLMapObstacleVisualizer] {LastPreviewMessage}");
                return false;
            }

            EnsurePreviewRoot();
            EnsureMaterial();
            RLCoordinateConverter converter = BuildConverter(map);
            float cellSize = ResolveCellSize(map);
            float footprint = Mathf.Max(0.05f, cellSize * Mathf.Clamp(obstacleCellScale, 0.1f, 1f));

            List<RLGridCoord> obstacles = map.obstacles ?? new List<RLGridCoord>();
            for (int i = 0; i < obstacles.Count; i++)
            {
                RLGridCoord obstacle = obstacles[i];
                if (obstacle == null)
                {
                    continue;
                }

                Vector3 center = converter.GridToWorld(obstacle);
                center.y = obstacleCellYOffset + obstacleCellHeight * 0.5f;

                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.name = $"RL_ObstacleCell_{obstacle.x}_{obstacle.y}";
                cell.transform.SetParent(previewRoot, false);
                cell.transform.position = center;
                cell.transform.localScale = new Vector3(footprint, obstacleCellHeight, footprint);

                Collider collider = cell.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                Renderer renderer = cell.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = obstacleMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
                if (ignoreRaycastLayer >= 0)
                {
                    cell.layer = ignoreRaycastLayer;
                }

                obstacleCells.Add(cell);
            }

            LastPreviewMessage = $"Loaded RL obstacle preview: {obstacleCells.Count} blocked cells";
            Debug.Log($"[RLMapObstacleVisualizer] {LastPreviewMessage}");
            return obstacleCells.Count > 0;
        }
        catch (System.Exception exception)
        {
            LastPreviewMessage = $"RL map obstacle preview failed: {exception.Message}";
            Debug.LogError($"[RLMapObstacleVisualizer] {LastPreviewMessage}");
            ClearPreview();
            return false;
        }
    }

    public void ClearPreview()
    {
        for (int i = obstacleCells.Count - 1; i >= 0; i--)
        {
            if (obstacleCells[i] != null)
            {
                Destroy(obstacleCells[i]);
            }
        }

        obstacleCells.Clear();
    }

    private void CacheReferences()
    {
        droneManager = RuntimeSceneRegistry.Resolve(droneManager, this);
    }

    private void EnsurePreviewRoot()
    {
        if (previewRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("RLMapObstaclePreview");
        if (existing != null)
        {
            previewRoot = existing;
            return;
        }

        GameObject rootObject = new GameObject("RLMapObstaclePreview");
        rootObject.transform.SetParent(transform, false);
        previewRoot = rootObject.transform;
    }

    private void EnsureMaterial()
    {
        if (obstacleMaterial != null)
        {
            obstacleMaterial.color = obstacleCellColor;
            return;
        }

        Shader shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        obstacleMaterial = new Material(shader);
        obstacleMaterial.color = obstacleCellColor;
    }

    private RLCoordinateConverter BuildConverter(RLMapJson map)
    {
        float cellSize = ResolveCellSize(map);
        float cruiseY = droneManager != null ? droneManager.cruiseHeight : 5f;
        Vector3 min = droneManager != null ? droneManager.planningWorldMin : Vector3.zero;
        Vector3 max = droneManager != null ? droneManager.planningWorldMax : new Vector3(map.width * cellSize, 0f, map.height * cellSize);

        if (map.world_transform != null && map.world_transform.cell_size > 0f)
        {
            min = new Vector3(map.world_transform.origin_x, min.y, map.world_transform.origin_z);
            cruiseY = Mathf.Approximately(map.world_transform.cruise_y, 0f)
                ? cruiseY
                : map.world_transform.cruise_y;
            max = new Vector3(
                min.x + Mathf.Max(0, map.width - 1) * cellSize,
                max.y,
                min.z + Mathf.Max(0, map.height - 1) * cellSize);
        }

        return new RLCoordinateConverter(min, max, cellSize, cruiseY);
    }

    private float ResolveCellSize(RLMapJson map)
    {
        if (map != null && map.world_transform != null && map.world_transform.cell_size > 0f)
        {
            return map.world_transform.cell_size;
        }

        if (map != null && map.grid_size > 0f)
        {
            return map.grid_size;
        }

        return droneManager != null ? Mathf.Max(0.01f, droneManager.planningGridCellSize) : 1f;
    }

    private void OnDestroy()
    {
        if (obstacleMaterial != null)
        {
            Destroy(obstacleMaterial);
        }
    }
}
