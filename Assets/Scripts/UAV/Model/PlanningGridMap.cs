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

        if (!IsWorldInside(from) || !IsWorldInside(to))
        {
            return false;
        }

        Vector3 delta = to - from;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return !IsBlocked(WorldToGrid(from));
        }

        Vector2Int current = WorldToTraversalGrid(from);
        Vector2Int end = WorldToTraversalGrid(to);
        if (IsBlocked(current))
        {
            return false;
        }

        if (current == end)
        {
            return !IsBlocked(end);
        }

        float startX = WorldToTraversalX(from.x);
        float startZ = WorldToTraversalZ(from.z);
        float endX = WorldToTraversalX(to.x);
        float endZ = WorldToTraversalZ(to.z);
        float gridDeltaX = endX - startX;
        float gridDeltaZ = endZ - startZ;

        int stepX = gridDeltaX > 0f ? 1 : gridDeltaX < 0f ? -1 : 0;
        int stepZ = gridDeltaZ > 0f ? 1 : gridDeltaZ < 0f ? -1 : 0;
        float tMaxX = CalculateInitialBoundaryT(startX, current.x, stepX, gridDeltaX);
        float tMaxZ = CalculateInitialBoundaryT(startZ, current.y, stepZ, gridDeltaZ);
        float tDeltaX = stepX != 0 ? Mathf.Abs(1f / gridDeltaX) : float.PositiveInfinity;
        float tDeltaZ = stepZ != 0 ? Mathf.Abs(1f / gridDeltaZ) : float.PositiveInfinity;
        int maxIterations = Mathf.Max(width + height + 4, 16);

        for (int i = 0; i < maxIterations && current != end; i++)
        {
            if (tMaxX < tMaxZ)
            {
                current.x += stepX;
                tMaxX += tDeltaX;
                if (IsBlocked(current))
                {
                    return false;
                }
            }
            else if (tMaxZ < tMaxX)
            {
                current.y += stepZ;
                tMaxZ += tDeltaZ;
                if (IsBlocked(current))
                {
                    return false;
                }
            }
            else
            {
                Vector2Int xStepCell = new Vector2Int(current.x + stepX, current.y);
                Vector2Int zStepCell = new Vector2Int(current.x, current.y + stepZ);
                current.x += stepX;
                current.y += stepZ;
                tMaxX += tDeltaX;
                tMaxZ += tDeltaZ;
                if (IsBlocked(xStepCell) || IsBlocked(zStepCell) || IsBlocked(current))
                {
                    return false;
                }
            }
        }

        return !IsBlocked(end);
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

    private Vector2Int WorldToTraversalGrid(Vector3 worldPosition)
    {
        int gx = Mathf.Clamp(Mathf.FloorToInt(WorldToTraversalX(worldPosition.x)), 0, width - 1);
        int gz = Mathf.Clamp(Mathf.FloorToInt(WorldToTraversalZ(worldPosition.z)), 0, height - 1);
        return new Vector2Int(gx, gz);
    }

    private float WorldToTraversalX(float worldX)
    {
        float x = Mathf.Clamp(worldX, worldMin.x, worldMax.x);
        return ((x - worldMin.x) / cellSize) + 0.5f;
    }

    private float WorldToTraversalZ(float worldZ)
    {
        float z = Mathf.Clamp(worldZ, worldMin.z, worldMax.z);
        return ((z - worldMin.z) / cellSize) + 0.5f;
    }

    private static float CalculateInitialBoundaryT(float start, int currentCell, int step, float delta)
    {
        if (step == 0 || Mathf.Abs(delta) <= 0.000001f)
        {
            return float.PositiveInfinity;
        }

        float nextBoundary = step > 0 ? currentCell + 1f : currentCell;
        return Mathf.Max(0f, (nextBoundary - start) / delta);
    }

    private int ToIndex(Vector2Int gridPosition)
    {
        return gridPosition.y * width + gridPosition.x;
    }
}
