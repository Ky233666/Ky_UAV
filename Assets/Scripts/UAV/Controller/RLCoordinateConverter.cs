using UnityEngine;

public sealed class RLCoordinateConverter
{
    private readonly Vector3 worldMin;
    private readonly Vector3 worldMax;
    private readonly float cellSize;
    private readonly float cruiseHeight;

    public RLCoordinateConverter(Vector3 worldMin, Vector3 worldMax, float cellSize, float cruiseHeight)
    {
        this.worldMin = worldMin;
        this.worldMax = worldMax;
        this.cellSize = Mathf.Max(0.01f, cellSize);
        this.cruiseHeight = cruiseHeight;
        Width = Mathf.Max(1, Mathf.RoundToInt((worldMax.x - worldMin.x) / this.cellSize) + 1);
        Height = Mathf.Max(1, Mathf.RoundToInt((worldMax.z - worldMin.z) / this.cellSize) + 1);
    }

    public int Width { get; }

    public int Height { get; }

    public RLWorldTransform BuildWorldTransform()
    {
        return new RLWorldTransform
        {
            origin_x = worldMin.x,
            origin_z = worldMin.z,
            cell_size = cellSize,
            cruise_y = cruiseHeight
        };
    }

    public RLGridCoord WorldToGridCoord(Vector3 worldPosition)
    {
        Vector2Int grid = WorldToGrid(worldPosition);
        return new RLGridCoord(grid.x, grid.y);
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        float x = Mathf.Clamp(worldPosition.x, worldMin.x, worldMax.x);
        float z = Mathf.Clamp(worldPosition.z, worldMin.z, worldMax.z);
        int gx = Mathf.RoundToInt((x - worldMin.x) / cellSize);
        int gy = Mathf.RoundToInt((z - worldMin.z) / cellSize);
        return new Vector2Int(gx, gy);
    }

    public Vector3 GridToWorld(RLGridCoord gridCoord)
    {
        if (gridCoord == null)
        {
            return new Vector3(worldMin.x, cruiseHeight, worldMin.z);
        }

        return GridToWorld(gridCoord.x, gridCoord.y);
    }

    public Vector3 GridToWorld(int x, int y)
    {
        float worldX = worldMin.x + x * cellSize;
        float worldZ = worldMin.z + y * cellSize;
        return new Vector3(worldX, cruiseHeight, worldZ);
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }
}
