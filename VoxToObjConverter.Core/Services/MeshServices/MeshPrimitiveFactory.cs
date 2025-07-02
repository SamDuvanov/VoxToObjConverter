using g3;
using VoxReader;

namespace VoxToObjConverter.Core.Services.MeshServices
{
    /// <summary>
    /// Creates mesh faces from voxel cubes.
    /// </summary>
    public class MeshPrimitiveFactory
    {
        /// <summary>
        /// Creates a mesh from a collection of voxels, generating visible faces only.
        /// </summary>
        public DMesh3 CreateMeshFromVoxels(IEnumerable<Voxel> voxels)
        {
            var mesh = new DMesh3();

            // Put voxels in a hash set for fast neighbor lookup
            var voxelSet = new HashSet<(int x, int y, int z)>();

            foreach (var v in voxels)
            {
                var p = v.LocalPosition;
                voxelSet.Add((p.X, p.Y, p.Z));
            }

            // Neighbor offsets for 6 faces: Left, Right, Bottom, Top, Back, Front
            (int dx, int dy, int dz)[] neighbors = new (int, int, int)[]
            {
                (-1, 0, 0), (1, 0, 0),
                (0, -1, 0), (0, 1, 0),
                (0, 0, -1), (0, 0, 1)
            };

            // For each voxel, generate faces only if neighbor voxel is missing
            foreach (var voxel in voxels)
            {
                var p = voxel.LocalPosition;
                int x = p.X;
                int y = p.Y;
                int z = p.Z;

                for (int faceIndex = 0; faceIndex < neighbors.Length; faceIndex++)
                {
                    var (dx, dy, dz) = neighbors[faceIndex];
                    if (!voxelSet.Contains((x + dx, y + dy, z + dz)))
                    {
                        // Generate a face mesh
                        var faceMesh = GenerateFaceMesh(voxel, faceIndex);
                        // Add face vertices and triangles to the main mesh
                        AppendMesh(mesh, faceMesh);
                    }
                }
            }

            return mesh;
        }

        /// <summary>
        /// Generates a single face mesh for the voxel at the given face index.
        /// </summary>
        private DMesh3 GenerateFaceMesh(Voxel voxel, int faceIndex)
        {
            double half = 0.5;
            var p = voxel.LocalPosition;
            var center = new Vector3d(p.X + half, p.Y + half, p.Z + half);
            var halfExtents = new Vector3d(half, half, half);
            var box = new Box3d(center, halfExtents);

            Vector3d min = box.Center - box.Extent;
            Vector3d max = box.Center + box.Extent;

            Vector3d[] v = new Vector3d[4];

            switch (faceIndex)
            {
                case 0: // Left
                    v[0] = min;
                    v[1] = new Vector3d(min.x, max.y, min.z);
                    v[2] = new Vector3d(min.x, max.y, max.z);
                    v[3] = new Vector3d(min.x, min.y, max.z);
                    break;
                case 1: // Right
                    v[0] = new Vector3d(max.x, min.y, min.z);
                    v[1] = new Vector3d(max.x, max.y, min.z);
                    v[2] = max;
                    v[3] = new Vector3d(max.x, min.y, max.z);
                    break;
                case 2: // Bottom
                    v[0] = min;
                    v[1] = new Vector3d(max.x, min.y, min.z);
                    v[2] = new Vector3d(max.x, min.y, max.z);
                    v[3] = new Vector3d(min.x, min.y, max.z);
                    break;
                case 3: // Top
                    v[0] = new Vector3d(min.x, max.y, min.z);
                    v[1] = new Vector3d(max.x, max.y, min.z);
                    v[2] = max;
                    v[3] = new Vector3d(min.x, max.y, max.z);
                    break;
                case 4: // Back
                    v[0] = min;
                    v[1] = new Vector3d(min.x, max.y, min.z);
                    v[2] = new Vector3d(max.x, max.y, min.z);
                    v[3] = new Vector3d(max.x, min.y, min.z);
                    break;
                case 5: // Front
                    v[0] = new Vector3d(min.x, min.y, max.z);
                    v[1] = new Vector3d(min.x, max.y, max.z);
                    v[2] = max;
                    v[3] = new Vector3d(max.x, min.y, max.z);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(faceIndex));
            }

            var faceMesh = new DMesh3();
            int a = faceMesh.AppendVertex(v[0]);
            int b = faceMesh.AppendVertex(v[1]);
            int c = faceMesh.AppendVertex(v[2]);
            int d = faceMesh.AppendVertex(v[3]);

            faceMesh.AppendTriangle(a, b, c);
            faceMesh.AppendTriangle(a, c, d);

            return faceMesh;
        }

        /// <summary>
        /// Appends all vertices and triangles from source mesh into target mesh.
        /// </summary>
        private void AppendMesh(DMesh3 target, DMesh3 source)
        {
            // Map old vertex IDs to new ones
            var vertexMap = new Dictionary<int, int>();

            foreach (int vID in source.VertexIndices())
            {
                Vector3d pos = source.GetVertex(vID);
                int newVID = target.AppendVertex(pos);
                vertexMap[vID] = newVID;
            }

            foreach (int tID in source.TriangleIndices())
            {
                Index3i tri = source.GetTriangle(tID);
                int a = vertexMap[tri.a];
                int b = vertexMap[tri.b];
                int c = vertexMap[tri.c];
                target.AppendTriangle(a, b, c);
            }
        }
    }
}
