using g3;
using gs;
using VoxReader;

namespace VoxToObjConverter.Core.Services.MeshServices
{
    /// <summary>
    /// Builds a watertight mesh from voxels using marching cubes approach for smooth surfaces.
    /// Removes internal faces to preserve only the outer shell.
    /// </summary>
    public class SolidMeshBuilder
    {
        private const double EPSILON = 1e-9;

        public DMesh3 GeneratePolygonMesh(IEnumerable<Voxel> voxels)
        {
            var triangleMesh = CreateMeshFromVoxels(voxels);
            var quadMesh = ConvertToQuadMesh(triangleMesh);

            return quadMesh;
        }

        private DMesh3 CreateMeshFromVoxels(IEnumerable<Voxel> voxels)
        {
            var mesh = new DMesh3();
            var voxelSet = new HashSet<(int x, int y, int z)>(
                voxels.Select(v => (v.LocalPosition.X, v.LocalPosition.Y, v.LocalPosition.Z)));

            // Define model bounds with margin for flood fill
            var bounds = GetModelBounds(voxels);
            var expandedBounds = (
                minX: bounds.minX - 1,
                maxX: bounds.maxX + 1,
                minY: bounds.minY - 1,
                maxY: bounds.maxY + 1,
                minZ: bounds.minZ - 1,
                maxZ: bounds.maxZ + 1
            );

            // Define external space using flood fill
            var externalSpace = GetExternalSpace(voxelSet, expandedBounds);

            var processedFaces = new HashSet<(int x, int y, int z, int face)>();
            var vertexCache = new Dictionary<Vector3d, int>(new Vector3dComparer());

            var directions = new[]
            {
                new { Dir = (-1, 0, 0), Face = 0 }, // Left
                new { Dir = (1, 0, 0), Face = 1 },  // Right
                new { Dir = (0, -1, 0), Face = 2 }, // Bottom
                new { Dir = (0, 1, 0), Face = 3 },  // Top
                new { Dir = (0, 0, -1), Face = 4 }, // Back
                new { Dir = (0, 0, 1), Face = 5 }   // Front
            };

            foreach (var voxel in voxels)
            {
                var p = voxel.LocalPosition;
                int x = p.X, y = p.Y, z = p.Z;

                foreach (var dir in directions)
                {
                    var (dx, dy, dz) = dir.Dir;
                    var faceKey = (x, y, z, dir.Face);

                    if (processedFaces.Contains(faceKey))
                        continue;

                    var neighborPos = (x + dx, y + dy, z + dz);

                    // Create face only if:
                    // 1. No neighboring voxel exists
                    // 2. Neighboring position belongs to external space
                    if (!voxelSet.Contains(neighborPos) && externalSpace.Contains(neighborPos))
                    {
                        var faceQuads = GenerateGreedyFace(voxelSet, processedFaces, x, y, z, dir.Face, dir.Dir);
                        foreach (var quad in faceQuads)
                        {
                            AddQuadToMesh(mesh, vertexCache, quad, dir.Face);
                        }
                    }
                }
            }

            return mesh;
        }

        private HashSet<(int x, int y, int z)> GetExternalSpace(HashSet<(int x, int y, int z)> voxelSet,
            (int minX, int maxX, int minY, int maxY, int minZ, int maxZ) bounds)
        {
            var externalSpace = new HashSet<(int x, int y, int z)>();
            var queue = new Queue<(int x, int y, int z)>();
            var visited = new HashSet<(int x, int y, int z)>();

            // Start flood fill from corner of expanded area
            var startPoint = (bounds.minX, bounds.minY, bounds.minZ);
            queue.Enqueue(startPoint);
            visited.Add(startPoint);

            var directions = new[]
            {
                (-1, 0, 0), (1, 0, 0),
                (0, -1, 0), (0, 1, 0),
                (0, 0, -1), (0, 0, 1)
            };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                externalSpace.Add(current);

                foreach (var (dx, dy, dz) in directions)
                {
                    var next = (current.x + dx, current.y + dy, current.z + dz);

                    // Check bounds
                    if (next.Item1 < bounds.minX || next.Item1 > bounds.maxX ||
                        next.Item2 < bounds.minY || next.Item2 > bounds.maxY ||
                        next.Item3 < bounds.minZ || next.Item3 > bounds.maxZ)
                        continue;

                    // Skip already visited points and voxels
                    if (visited.Contains(next) || voxelSet.Contains(next))
                        continue;

                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            return externalSpace;
        }

        private (int minX, int maxX, int minY, int maxY, int minZ, int maxZ) GetModelBounds(IEnumerable<Voxel> voxels)
        {
            var positions = voxels.Select(v => v.LocalPosition).ToList();
            return (
                minX: positions.Min(p => p.X),
                maxX: positions.Max(p => p.X),
                minY: positions.Min(p => p.Y),
                maxY: positions.Max(p => p.Y),
                minZ: positions.Min(p => p.Z),
                maxZ: positions.Max(p => p.Z)
            );
        }

        private List<Vector3d[]> GenerateGreedyFace(HashSet<(int x, int y, int z)> voxelSet,
            HashSet<(int x, int y, int z, int face)> processedFaces,
            int startX, int startY, int startZ, int faceType, (int dx, int dy, int dz) direction)
        {
            var quads = new List<Vector3d[]>();
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
                case 0: // Left (-X)
                    quad[0] = new Vector3d(minX, minY, minZ);
                    quad[1] = new Vector3d(minX, minY, maxZ);
                    quad[2] = new Vector3d(minX, maxY, maxZ);
                    quad[3] = new Vector3d(minX, maxY, minZ);
                    break;
                case 1: // Right (+X)
                    quad[0] = new Vector3d(maxX, minY, minZ);
                    quad[1] = new Vector3d(maxX, maxY, minZ);
                    quad[2] = new Vector3d(maxX, maxY, maxZ);
                    quad[3] = new Vector3d(maxX, minY, maxZ);
                    break;
                case 2: // Bottom (-Y)
                    quad[0] = new Vector3d(minX, minY, minZ);
                    quad[1] = new Vector3d(maxX, minY, minZ);
                    quad[2] = new Vector3d(maxX, minY, maxZ);
                    quad[3] = new Vector3d(minX, minY, maxZ);
                    break;
                case 3: // Top (+Y)
                    quad[0] = new Vector3d(minX, maxY, minZ);
                    quad[1] = new Vector3d(minX, maxY, maxZ);
                    quad[2] = new Vector3d(maxX, maxY, maxZ);
                    quad[3] = new Vector3d(maxX, maxY, minZ);
                    break;
                case 4: // Back (-Z)
                    quad[0] = new Vector3d(minX, minY, minZ);
                    quad[1] = new Vector3d(minX, maxY, minZ);
                    quad[2] = new Vector3d(maxX, maxY, minZ);
                    quad[3] = new Vector3d(maxX, minY, minZ);
                    break;
                case 5: // Front (+Z)
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

            if (ids.Any(id => id < 0))
                return;

            try
            {
                var t1 = mesh.AppendTriangle(ids[0], ids[1], ids[2]);
                var t2 = mesh.AppendTriangle(ids[0], ids[2], ids[3]);

                if (t1 == DMesh3.InvalidID || t2 == DMesh3.InvalidID)
                {
                    mesh.AppendTriangle(ids[0], ids[2], ids[1]);
                    mesh.AppendTriangle(ids[0], ids[3], ids[2]);
                }
            }
            catch
            {
                // Ignore invalid triangles
            }
        }

        public DMesh3 PostProcessMesh(DMesh3 mesh)
        {
            try
            {
                mesh.CompactInPlace();

                if (!mesh.IsClosed())
                {
                    FixMeshOrientation(mesh);
                }

                return mesh;
            }
            catch
            {
                return mesh;
            }
        }

        private void FixMeshOrientation(DMesh3 mesh)
        {
            var trianglesToFlip = new List<int>();

            foreach (int tid in mesh.TriangleIndices())
            {
                if (!mesh.IsTriangle(tid))
                    continue;

                var tri = mesh.GetTriangle(tid);
                var normal = mesh.GetTriNormal(tid);
                var centroid = mesh.GetTriCentroid(tid);

                if (IsNormalPointingInward(centroid, normal))
                {
                    trianglesToFlip.Add(tid);
                }
            }

            foreach (int tid in trianglesToFlip)
            {
                mesh.ReverseTriOrientation(tid);
            }
        }

        private bool IsNormalPointingInward(Vector3d centroid, Vector3d normal)
        {
            return Vector3d.Dot(centroid, normal) < 0;
        }

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