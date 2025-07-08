namespace VoxToObjConverter.Core.Services.VoxelServices.Utils;

/// <summary>
/// Represents the dimensions of the voxel grid.
/// </summary>
public readonly struct GridDimensions
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public GridDimensions(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Checks if the given coordinates are inside the grid bounds.
    /// </summary>
    public bool IsInside(int x, int y, int z) =>
        x >= 0 && y >= 0 && z >= 0 && x < X && y < Y && z < Z;

    /// <summary>
    /// Checks if the given voxel position is inside the grid bounds.
    /// </summary>
    public bool IsInside(VoxelPosition pos) =>
        IsInside(pos.X, pos.Y, pos.Z);
}