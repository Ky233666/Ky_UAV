using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds an occupancy grid from precomputed scene obstacle bounds.
/// </summary>
public static class PlanningMapBuilder
{
    public static PlanningGridMap BuildFromBounds(
        Vector3 worldMin,
        Vector3 worldMax,
        float cellSize,
        IList<Bounds> obstacleBounds,
        float safetyPadding)
    {
        PlanningGridMap map = new PlanningGridMap(worldMin, worldMax, cellSize);
        if (obstacleBounds == null || obstacleBounds.Count == 0)
        {
            return map;
        }

        float padding = Mathf.Max(0f, safetyPadding);
        for (int i = 0; i < obstacleBounds.Count; i++)
        {
            MarkBounds(map, obstacleBounds[i], padding);
        }

        return map;
    }

    private static void MarkBounds(PlanningGridMap map, Bounds bounds, float padding)
    {
        if (map == null || !map.IsValid || bounds.size.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float minX = bounds.min.x - padding;
        float maxX = bounds.max.x + padding;
        float minZ = bounds.min.z - padding;
        float maxZ = bounds.max.z + padding;

        if (maxX < map.worldMin.x ||
            minX > map.worldMax.x ||
            maxZ < map.worldMin.z ||
            minZ > map.worldMax.z)
        {
            return;
        }

        Vector2Int minCell = map.WorldToGrid(new Vector3(minX, 0f, minZ));
        Vector2Int maxCell = map.WorldToGrid(new Vector3(maxX, 0f, maxZ));
        int startX = Mathf.Max(0, Mathf.Min(minCell.x, maxCell.x));
        int endX = Mathf.Min(map.width - 1, Mathf.Max(minCell.x, maxCell.x));
        int startZ = Mathf.Max(0, Mathf.Min(minCell.y, maxCell.y));
        int endZ = Mathf.Min(map.height - 1, Mathf.Max(minCell.y, maxCell.y));

        for (int z = startZ; z <= endZ; z++)
        {
            for (int x = startX; x <= endX; x++)
            {
                Vector3 center = map.GridToWorld(new Vector2Int(x, z));
                if (center.x >= minX &&
                    center.x <= maxX &&
                    center.z >= minZ &&
                    center.z <= maxZ)
                {
                    map.SetBlocked(new Vector2Int(x, z), true);
                }
            }
        }
    }
}
