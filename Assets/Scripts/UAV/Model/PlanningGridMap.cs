using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Immutable-style occupancy grid used by path planners. It converts expensive
/// Unity scene obstacle checks into O(1) blocked-cell queries.
/// </summary>
[System.Serializable]
public class PlanningGridMap
{
    public Vector3 worldMin;
    public Vector3 worldMax;
    public float cellSize;
    public int width;
    public int height;
    public bool[] blocked;

    public int BlockedCellCount { get; private set; }

    public bool IsValid =>
        cellSize > 0f &&
        width > 0 &&
        height > 0 &&
        blocked != null &&
        blocked.Length == width * height;

    public PlanningGridMap(Vector3 worldMin, Vector3 worldMax, float cellSize)
    {
        this.worldMin = worldMin;
        this.worldMax = worldMax;
        this.cellSize = Mathf.Max(0.01f, cellSize);

        width = Mathf.Max(1, Mathf.CeilToInt((worldMax.x - worldMin.x) / this.cellSize) + 1);
        height = Mathf.Max(1, Mathf.CeilToInt((worldMax.z - worldMin.z) / this.cellSize) + 1);
        blocked = new bool[width * height];
    }

    public bool IsWorldInside(Vector3 worldPosition)
    {
        return worldPosition.x >= worldMin.x &&
               worldPosition.x <= worldMax.x &&
               worldPosition.z >= worldMin.z &&
               worldPosition.z <= worldMax.z;
    }

    public bool IsInside(Vector2Int gridPosition)
    {
        return gridPosition.x >= 0 &&
               gridPosition.x < width &&
               gridPosition.y >= 0 &&
               gridPosition.y < height;
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        float x = Mathf.Clamp(worldPosition.x, worldMin.x, worldMax.x);
        float z = Mathf.Clamp(worldPosition.z, worldMin.z, worldMax.z);

        int gx = Mathf.Clamp(Mathf.RoundToInt((x - worldMin.x) / cellSize), 0, width - 1);
        int gz = Mathf.Clamp(Mathf.RoundToInt((z - worldMin.z) / cellSize), 0, height - 1);
        return new Vector2Int(gx, gz);
    }

    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        int gx = Mathf.Clamp(gridPosition.x, 0, width - 1);
        int gz = Mathf.Clamp(gridPosition.y, 0, height - 1);
        float x = worldMin.x + gx * cellSize;
        float z = worldMin.z + gz * cellSize;
        float y = Mathf.Lerp(worldMin.y, worldMax.y, 0.5f);
        return new Vector3(x, y, z);
    }

    public bool IsBlocked(Vector2Int gridPosition)
    {
        if (!IsInside(gridPosition))
        {
            return true;
        }

        return blocked[ToIndex(gridPosition)];
    }

    public bool IsWalkable(Vector2Int gridPosition)
    {
        return IsInside(gridPosition) && !IsBlocked(gridPosition);
    }

    public void SetBlocked(Vector2Int gridPosition, bool value)
    {
        if (!IsInside(gridPosition))
        {
            return;
        }

        int index = ToIndex(gridPosition);
        if (blocked[index] == value)
        {
            return;
        }

        blocked[index] = value;
        BlockedCellCount += value ? 1 : -1;
    }

    public bool IsSegmentClear(Vector3 from, Vector3 to)
    {
        if (!IsValid)
        {
            return true;
        }

        Vector3 delta = to - from;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return !IsBlocked(WorldToGrid(from));
        }

        float sampleStep = Mathf.Max(cellSize * 0.35f, 0.1f);
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(distance / sampleStep));
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector3 sample = Vector3.Lerp(from, to, t);
            if (IsBlocked(WorldToGrid(sample)))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryFindNearestWalkableCell(
        Vector2Int origin,
        int maxRadius,
        out Vector2Int walkableCell)
    {
        if (IsWalkable(origin))
        {
            walkableCell = origin;
            return true;
        }

        int radiusLimit = Mathf.Max(0, maxRadius);
        for (int radius = 1; radius <= radiusLimit; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (TryAcceptWalkable(new Vector2Int(origin.x + dx, origin.y - radius), out walkableCell) ||
                    TryAcceptWalkable(new Vector2Int(origin.x + dx, origin.y + radius), out walkableCell))
                {
                    return true;
                }
            }

            for (int dz = -radius + 1; dz <= radius - 1; dz++)
            {
                if (TryAcceptWalkable(new Vector2Int(origin.x - radius, origin.y + dz), out walkableCell) ||
                    TryAcceptWalkable(new Vector2Int(origin.x + radius, origin.y + dz), out walkableCell))
                {
                    return true;
                }
            }
        }

        walkableCell = default;
        return false;
    }

    public List<Vector3> GetBlockedWorldPositions(int maxCount)
    {
        List<Vector3> positions = new List<Vector3>();
        int limit = Mathf.Max(0, maxCount);
        for (int z = 0; z < height && positions.Count < limit; z++)
        {
            for (int x = 0; x < width && positions.Count < limit; x++)
            {
                Vector2Int cell = new Vector2Int(x, z);
                if (IsBlocked(cell))
                {
                    positions.Add(GridToWorld(cell));
                }
            }
        }

        return positions;
    }

    private bool TryAcceptWalkable(Vector2Int candidate, out Vector2Int walkableCell)
    {
        if (IsWalkable(candidate))
        {
            walkableCell = candidate;
            return true;
        }

        walkableCell = default;
        return false;
    }

    private int ToIndex(Vector2Int gridPosition)
    {
        return gridPosition.y * width + gridPosition.x;
    }
}
