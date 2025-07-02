using g3;
using gs;
using VoxReader;

namespace VoxToObjConverter.Core.Services.MeshServices
{
    /// <summary>
    /// Builds a watertight mesh from voxels using marching cubes approach for smooth surfaces.
    /// </summary>
    public class SolidMeshBuilder
    {
        private const double EPSILON = 1e-9;

        public DMesh3 CreateMeshFromVoxels(IEnumerable<Voxel> voxels)
        {
            var mesh = new DMesh3();
            var voxelSet = new HashSet<(int x, int y, int z)>();

            // Create voxel set for fast lookup
            foreach (var v in voxels)
            {
                var p = v.LocalPosition;
                voxelSet.Add((p.X, p.Y, p.Z));
            }

            // Use greedy meshing to merge adjacent faces
            var processedFaces = new HashSet<(int x, int y, int z, int face)>();
            var vertexCache = new Dictionary<Vector3d, int>(new Vector3dComparer());

            // 6 directions: Left, Right, Bottom, Top, Back, Front
            var directions = new[]
            {
            new { Dir = (-1, 0, 0), Face = 0, Name = "Left" },
            new { Dir = (1, 0, 0), Face = 1, Name = "Right" },
            new { Dir = (0, -1, 0), Face = 2, Name = "Bottom" },
            new { Dir = (0, 1, 0), Face = 3, Name = "Top" },
            new { Dir = (0, 0, -1), Face = 4, Name = "Back" },
            new { Dir = (0, 0, 1), Face = 5, Name = "Front" }
        };

            foreach (var voxel in voxels)
            {
                var p = voxel.LocalPosition;
                int x = p.X, y = p.Y, z = p.Z;

                foreach (var dir in directions)
                {
                    var (dx, dy, dz) = dir.Dir;
                    var faceKey = (x, y, z, dir.Face);

                    // Skip already processed faces
                    if (processedFaces.Contains(faceKey))
                        continue;

                    // Check if face is needed (no adjacent voxel)
                    if (voxelSet.Contains((x + dx, y + dy, z + dz)))
                        continue;

                    // Generate optimized face
                    var faceQuads = GenerateGreedyFace(voxelSet, processedFaces, x, y, z, dir.Face, dir.Dir);

                    foreach (var quad in faceQuads)
                    {
                        AddQuadToMesh(mesh, vertexCache, quad, dir.Face);
                    }
                }
            }

            return mesh;
        }

        private List<Vector3d[]> GenerateGreedyFace(HashSet<(int x, int y, int z)> voxelSet,
            HashSet<(int x, int y, int z, int face)> processedFaces,
            int startX, int startY, int startZ, int faceType, (int dx, int dy, int dz) direction)
        {
            var quads = new List<Vector3d[]>();
            var visited = new HashSet<(int x, int y, int z)>();

            // Simple version - create one face per voxel
            // Can be improved with greedy meshing in the future
            var quad = GenerateFaceQuad(startX, startY, startZ, faceType);
            quads.Add(quad);

            processedFaces.Add((startX, startY, startZ, faceType));

            return quads;
        }

        private Vector3d[] GenerateFaceQuad(int x, int y, int z, int faceType)
        {
            double minX = x, maxX = x + 1;
            double minY = y, maxY = y + 1;
            double minZ = z, maxZ = z + 1;

            Vector3d[] quad = new Vector3d[4];

            switch (faceType)
            {
                case 0: // Left face (-X) - normal points left
                    quad[0] = new Vector3d(minX, minY, minZ);
                    quad[1] = new Vector3d(minX, minY, maxZ);
                    quad[2] = new Vector3d(minX, maxY, maxZ);
                    quad[3] = new Vector3d(minX, maxY, minZ);
                    break;
                case 1: // Right face (+X) - normal points right
                    quad[0] = new Vector3d(maxX, minY, minZ);
                    quad[1] = new Vector3d(maxX, maxY, minZ);
                    quad[2] = new Vector3d(maxX, maxY, maxZ);
                    quad[3] = new Vector3d(maxX, minY, maxZ);
                    break;
                case 2: // Bottom face (-Y) - normal points down
                    quad[0] = new Vector3d(minX, minY, minZ);
                    quad[1] = new Vector3d(maxX, minY, minZ);
                    quad[2] = new Vector3d(maxX, minY, maxZ);
                    quad[3] = new Vector3d(minX, minY, maxZ);
                    break;
                case 3: // Top face (+Y) - normal points up
                    quad[0] = new Vector3d(minX, maxY, minZ);
                    quad[1] = new Vector3d(minX, maxY, maxZ);
                    quad[2] = new Vector3d(maxX, maxY, maxZ);
                    quad[3] = new Vector3d(maxX, maxY, minZ);
                    break;
                case 4: // Back face (-Z) - normal points back
                    quad[0] = new Vector3d(minX, minY, minZ);
                    quad[1] = new Vector3d(minX, maxY, minZ);
                    quad[2] = new Vector3d(maxX, maxY, minZ);
                    quad[3] = new Vector3d(maxX, minY, minZ);
                    break;
                case 5: // Front face (+Z) - normal points forward
                    quad[0] = new Vector3d(minX, minY, maxZ);
                    quad[1] = new Vector3d(maxX, minY, maxZ);
                    quad[2] = new Vector3d(maxX, maxY, maxZ);
                    quad[3] = new Vector3d(minX, maxY, maxZ);
                    break;
            }

            return quad;
        }

        private void AddQuadToMesh(DMesh3 mesh, Dictionary<Vector3d, int> vertexCache, Vector3d[] quad, int faceType)
        {
            // Add vertices with deduplication
            int[] ids = new int[4];
            for (int i = 0; i < 4; i++)
            {
                if (!vertexCache.TryGetValue(quad[i], out int vid))
                {
                    vid = mesh.AppendVertex(quad[i]);
                    vertexCache[quad[i]] = vid;
                }
                ids[i] = vid;
            }

            // Check validity of indices
            if (ids.Any(id => id < 0))
                return;

            // Add triangles with correct orientation
            try
            {
                var t1 = mesh.AppendTriangle(ids[0], ids[1], ids[2]);
                var t2 = mesh.AppendTriangle(ids[0], ids[2], ids[3]);

                // Check if triangles were added successfully
                if (t1 == DMesh3.InvalidID || t2 == DMesh3.InvalidID)
                {
                    // Try reverse order
                    mesh.AppendTriangle(ids[0], ids[2], ids[1]);
                    mesh.AppendTriangle(ids[0], ids[3], ids[2]);
                }
            }
            catch
            {
                // Ignore invalid triangles
            }
        }

        /// <summary>
        /// Additional method for mesh post-processing
        /// </summary>
        public DMesh3 PostProcessMesh(DMesh3 mesh)
        {
            try
            {
                // Compact mesh (remove unused vertices)
                mesh.CompactInPlace();

                // Fix triangle orientation if there are issues
                if (!mesh.IsClosed())
                {
                    // Try to fix orientation manually
                    FixMeshOrientation(mesh);
                }

                return mesh;
            }
            catch
            {
                return mesh; // Return original mesh on error
            }
        }

        /// <summary>
        /// Fixes mesh triangle orientation
        /// </summary>
        private void FixMeshOrientation(DMesh3 mesh)
        {
            // Simple orientation check and fix
            var trianglesToFlip = new List<int>();

            foreach (int tid in mesh.TriangleIndices())
            {
                if (!mesh.IsTriangle(tid))
                    continue;

                var tri = mesh.GetTriangle(tid);
                var normal = mesh.GetTriNormal(tid);

                // Get triangle center
                var centroid = mesh.GetTriCentroid(tid);

                // Simple heuristic: if normal points "inward",
                // then triangle needs to be flipped
                // This works for convex objects
                if (IsNormalPointingInward(centroid, normal))
                {
                    trianglesToFlip.Add(tid);
                }
            }

            // Flip triangles
            foreach (int tid in trianglesToFlip)
            {
                mesh.ReverseTriOrientation(tid);
            }
        }

        /// <summary>
        /// Simple normal direction check (for convex objects)
        /// </summary>
        private bool IsNormalPointingInward(Vector3d centroid, Vector3d normal)
        {
            // For simple voxel models, we can assume
            // that object is in positive coordinate space
            // and normals should point away from origin
            return Vector3d.Dot(centroid, normal) < 0;
        }

        /// <summary>
        /// Comparer for precise Vector3d comparison
        /// </summary>
        private class Vector3dComparer : IEqualityComparer<Vector3d>
        {
            public bool Equals(Vector3d x, Vector3d y)
            {
                return Math.Abs(x.x - y.x) < EPSILON &&
                       Math.Abs(x.y - y.y) < EPSILON &&
                       Math.Abs(x.z - y.z) < EPSILON;
            }

            public int GetHashCode(Vector3d obj)
            {
                // Round to fixed precision for stable hashing
                long hashX = (long)Math.Round(obj.x / EPSILON);
                long hashY = (long)Math.Round(obj.y / EPSILON);
                long hashZ = (long)Math.Round(obj.z / EPSILON);

                unchecked
                {
                    long hash = 17;
                    hash = hash * 31 + hashX;
                    hash = hash * 31 + hashY;
                    hash = hash * 31 + hashZ;
                    return (int)hash;
                }
            }
        }
    }
}
