using VoxReader.Interfaces;

namespace VoxToObjConverter.Core.Services.VoxelServices.Utils;

/// <summary>
/// Represents a 3D grid indicating the presence or absence of voxels in each cell.
/// Used for quick lookup whether a voxel exists at a given position.
/// </summary>
public class VoxelPresenceGrid
{
    private readonly bool[,,] grid;

    public GridDimensions Dimensions { get; }

    public VoxelPresenceGrid(GridDimensions dimensions)
    {
        grid = new bool[dimensions.X, dimensions.Y, dimensions.Z];
        Dimensions = dimensions;
    }

    /// <summary>
    /// Creates a VoxelPresenceGrid from the given voxel model,
    /// marking cells as occupied where voxels exist.
    /// </summary>
    public static VoxelPresenceGrid BuildFromModel(IModel model, GridDimensions dimensions)
    {
        var grid = new VoxelPresenceGrid(dimensions);

        foreach (var voxel in model.Voxels)
        {
            var pos = new VoxelPosition(voxel.LocalPosition.X, voxel.LocalPosition.Y, voxel.LocalPosition.Z);
            grid[pos] = true;
        }

        return grid;
    }

    /// <summary>
    /// Checks if the given voxel position is inside the grid bounds.
    /// </summary>
    public bool IsInside(VoxelPosition pos) => Dimensions.IsInside(pos);

    /// <summary>
    /// Gets or sets whether a voxel exists at the specified position.
    /// </summary>
    public bool this[VoxelPosition pos]
    {
        get => grid[pos.X, pos.Y, pos.Z];
        set => grid[pos.X, pos.Y, pos.Z] = value;
    }
}

