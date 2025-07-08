namespace VoxToObjConverter.Core.Services.VoxelServices.Utils;

/// <summary>
/// Represents a 3D position of a voxel using integer coordinates.
/// </summary>
public readonly struct VoxelPosition
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public VoxelPosition(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Returns a new VoxelPosition offset by the specified deltas.
    /// </summary>
    public VoxelPosition Offset(int dx, int dy, int dz) =>
        new VoxelPosition(X + dx, Y + dy, Z + dz);
}
