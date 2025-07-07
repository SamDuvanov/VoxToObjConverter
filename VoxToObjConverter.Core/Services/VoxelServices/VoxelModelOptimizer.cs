using VoxReader;
using VoxReader.Interfaces;
using VoxToObjConverter.Core.Services.VoxelServices.Utils;

namespace VoxToObjConverter.Core.Services.VoxelServices;

/// <summary>
/// Optimizes a voxel model by retaining only surface voxels.
/// </summary>
public class VoxelModelOptimizer
{
    private static readonly int[] NeighborOffsetX = { -1, 1, 0, 0, 0, 0 };
    private static readonly int[] NeighborOffsetY = { 0, 0, -1, 1, 0, 0 };
    private static readonly int[] NeighborOffsetZ = { 0, 0, 0, 0, -1, 1 };

    /// <summary>
    /// Optimizes the voxel model by returning only its surface voxels.
    /// </summary>
    /// <param name="voxModel">The voxel model to optimize.</param>
    /// <returns>Enumerable of surface voxels.</returns>
    public IEnumerable<Voxel> OptimizeModel(IModel voxModel)
    {
        var dimensions = new GridDimensions(voxModel.LocalSize.X, voxModel.LocalSize.Y, voxModel.LocalSize.Z);
        var voxelGrid = VoxelPresenceGrid.BuildFromModel(voxModel, dimensions);

        return GetSurfaceVoxels(voxModel, voxelGrid);
    }

    /// <summary>
    /// Filters surface voxels from the model using the voxel grid.
    /// </summary>
    private static IEnumerable<Voxel> GetSurfaceVoxels(IModel voxModel, VoxelPresenceGrid voxelGrid)
    {
        var surfaceVoxels = new List<Voxel>();

        foreach (var voxel in voxModel.Voxels)
        {
            var voxelPosition = new VoxelPosition(voxel.LocalPosition.X, voxel.LocalPosition.Y, voxel.LocalPosition.Z);

            if (IsSurfaceVoxel(voxelPosition, voxelGrid))
            {
                surfaceVoxels.Add(voxel);
            }
        }

        return surfaceVoxels;
    }

    /// <summary>
    /// Determines if a voxel is a surface voxel based on its neighbors.
    /// </summary>
    private static bool IsSurfaceVoxel(VoxelPosition voxelPosition, VoxelPresenceGrid grid)
    {
        for (int i = 0; i < 6; i++)
        {
            var neighbor = voxelPosition.Offset(NeighborOffsetX[i], NeighborOffsetY[i], NeighborOffsetZ[i]);

            if (!grid.IsInside(neighbor) || !grid[neighbor])
            {
                return true;
            }
        }

        return false;
    }
}