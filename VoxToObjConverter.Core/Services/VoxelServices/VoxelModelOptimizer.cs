using VoxReader.Interfaces;
using VoxReader;

namespace VoxToObjConverter.Core.Services.VoxelServices
{
    public class VoxelModelOptimizer
    { 
        public IEnumerable<Voxel> OptimizeModel(IModel voxModel)
        {
            var sizeX = voxModel.LocalSize.X;
            var sizeY = voxModel.LocalSize.Y;
            var sizeZ = voxModel.LocalSize.Z;

            // Creating voxels 3d array.
            bool[,,] grid = new bool[sizeX, sizeY, sizeZ];

            foreach (var voxel in voxModel.Voxels)
            {
                var p = voxel.LocalPosition;
                grid[p.X, p.Y, p.Z] = true;
            }

            // Filtering surface voxels.
            List<Voxel> surfaceVoxels = new();

            int[] dx = { -1, 1, 0, 0, 0, 0 };
            int[] dy = { 0, 0, -1, 1, 0, 0 };
            int[] dz = { 0, 0, 0, 0, -1, 1 };

            foreach (var voxel in voxModel.Voxels)
            {
                var x = voxel.LocalPosition.X;
                var y = voxel.LocalPosition.Y;
                var z = voxel.LocalPosition.Z;

                bool isSurface = false;

                for (int i = 0; i < 6; i++)
                {
                    int nx = x + dx[i];
                    int ny = y + dy[i];
                    int nz = z + dz[i];

                    // If the neighbor is out of bounds, then it is a boundary voxel
                    if (nx < 0 || ny < 0 || nz < 0 ||
                        nx >= sizeX || ny >= sizeY || nz >= sizeZ ||
                        !grid[nx, ny, nz])
                    {
                        isSurface = true;
                        break;
                    }
                }

                if (isSurface)
                {
                    surfaceVoxels.Add(voxel);
                }
            }

            return surfaceVoxels;
        }
    }
}
