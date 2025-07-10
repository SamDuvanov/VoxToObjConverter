using g3;
using VoxReader;
using VoxToObjConverter.Core.Models;

namespace VoxToObjConverter.Core.Services.MeshServices;

/// <summary>
/// Builds 3D meshes from voxel data using a solid boxy approach.
/// Only external faces (faces adjacent to external space) are generated to create a solid mesh.
/// </summary>
public class MeshBuilder
{
    private const int BoundaryExpansion = 1;

    /// <summary>
    /// Generates a solid 3D mesh of separate triangles from a collection of voxels.
    /// Uses flood fill algorithm to determine external space and only renders external faces.
    /// </summary>
    /// <param name="voxels">Collection of voxels to convert to mesh</param>
    /// <returns>Generated 3D mesh with only external faces</returns>
    public DMesh3 GenerateSolidBoxyMesh(IEnumerable<Voxel> voxels)
    {
        var mesh = new DMesh3();
        var voxelList = voxels.ToList();

        if (!voxelList.Any())
        {
            return mesh;
        }

        var voxelPositions = CreateVoxelPositionSet(voxelList);
        var modelBounds = CalculateModelBounds(voxelList);
        var expandedBounds = ExpandBounds(modelBounds, BoundaryExpansion);
        var externalSpace = DetermineExternalSpace(voxelPositions, expandedBounds);

        foreach (var voxel in voxelList)
        {
            AddExternalVoxelFaces(mesh, voxel, externalSpace);
        }

        return mesh;
    }

    /// <summary>
    /// Creates a hash set of voxel positions for fast lookup operations.
    /// </summary>
    /// <param name="voxels">Collection of voxels</param>
    /// <returns>Hash set containing integer coordinates of all voxels</returns>
    private HashSet<Vector> CreateVoxelPositionSet(IEnumerable<Voxel> voxels)
    {
        return new HashSet<Vector>(
            voxels.Select(v => new Vector(
                v.LocalPosition.X,
                v.LocalPosition.Y,
                v.LocalPosition.Z))
        );
    }

    /// <summary>
    /// Calculates the bounding box of the voxel model.
    /// </summary>
    /// <param name="voxels">Collection of voxels</param>
    /// <returns>Bounding box coordinates</returns>
    private ModelBoundingBox CalculateModelBounds(IEnumerable<Voxel> voxels)
    {
        var positions = voxels.Select(v => v.LocalPosition).ToList();

        return new ModelBoundingBox(
            minX: positions.Min(p => p.X),
            maxX: positions.Max(p => p.X),
            minY: positions.Min(p => p.Y),
            maxY: positions.Max(p => p.Y),
            minZ: positions.Min(p => p.Z),
            maxZ: positions.Max(p => p.Z)
        );
    }

    /// <summary>
    /// Expands the model bounds by the specified amount in all directions.
    /// </summary>
    /// <param name="bounds">Original model bounds</param>
    /// <param name="expansion">Amount to expand in each direction</param>
    /// <returns>Expanded bounds</returns>
    private ModelBoundingBox ExpandBounds(ModelBoundingBox bounds, int expansion)
    {
        return new ModelBoundingBox(
            minX: bounds.MinX - expansion,
            maxX: bounds.MaxX + expansion,
            minY: bounds.MinY - expansion,
            maxY: bounds.MaxY + expansion,
            minZ: bounds.MinZ - expansion,
            maxZ: bounds.MaxZ + expansion
        );
    }

    /// <summary>
    /// Determines external space using flood fill algorithm starting from the boundary.
    /// External space consists of all positions that can be reached from the boundary
    /// without passing through voxels.
    /// </summary>
    /// <param name="voxelPositions">Set of voxel positions</param>
    /// <param name="bounds">Expanded model bounds</param>
    /// <returns>Set of positions that are external to the model</returns>
    private HashSet<Vector> DetermineExternalSpace(
        HashSet<Vector> voxelPositions,
        ModelBoundingBox bounds)
    {
        var externalSpace = new HashSet<Vector>();
        var queue = new Queue<Vector>();
        var visited = new HashSet<Vector>();

        // Start flood fill from the corner of expanded bounds
        var startPoint = new Vector(bounds.MinX, bounds.MinY, bounds.MinZ);
        queue.Enqueue(startPoint);
        visited.Add(startPoint);

        var directions = GetSixDirections();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            externalSpace.Add(current);

            foreach (var direction in directions)
            {
                var next = current.Add(direction);

                if (!IsWithinBounds(next, bounds) ||
                    visited.Contains(next) ||
                    voxelPositions.Contains(next))
                {
                    continue;
                }

                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        return externalSpace;
    }

    /// <summary>
    /// Returns the six cardinal directions for 3D space navigation.
    /// </summary>
    /// <returns>Array of direction vectors</returns>
    private Vector[] GetSixDirections()
    {
        return new[]
        {
            new Vector(-1, 0, 0), new Vector(1, 0, 0),
            new Vector(0, -1, 0), new Vector(0, 1, 0),
            new Vector(0, 0, -1), new Vector(0, 0, 1)
        };
    }

    /// <summary>
    /// Checks if a position is within the specified bounds.
    /// </summary>
    /// <param name="position">Position to check</param>
    /// <param name="bounds">Bounds to check against</param>
    /// <returns>True if position is within bounds</returns>
    private bool IsWithinBounds(Vector position, ModelBoundingBox bounds)
    {
        return position.X >= bounds.MinX && position.X <= bounds.MaxX &&
               position.Y >= bounds.MinY && position.Y <= bounds.MaxY &&
               position.Z >= bounds.MinZ && position.Z <= bounds.MaxZ;
    }

    /// <summary>
    /// Adds only external faces of a voxel to the mesh.
    /// A face is external if its adjacent position is in external space.
    /// </summary>
    /// <param name="mesh">Mesh to add faces to</param>
    /// <param name="voxel">Voxel to process</param>
    /// <param name="externalSpace">Set of external space positions</param>
    private void AddExternalVoxelFaces(DMesh3 mesh, Voxel voxel, HashSet<Vector> externalSpace)
    {
        var position = new Vector(
            voxel.LocalPosition.X,
            voxel.LocalPosition.Y,
            voxel.LocalPosition.Z);

        var cubeVertices = GenerateCubeVertices(position);
        var faceDefinitions = GetCubeFaceDefinitions();

        foreach (var face in faceDefinitions)
        {
            var neighborPosition = position.Add(face.Direction);

            if (externalSpace.Contains(neighborPosition))
            {
                AddQuadFace(mesh, cubeVertices, face.VertexIndices);
            }
        }
    }

    /// <summary>
    /// Generates the 8 vertices of a cube at the specified position.
    /// </summary>
    /// <param name="position">Position of the cube</param>
    /// <returns>Array of 8 cube vertices</returns>
    private Vector3d[] GenerateCubeVertices(Vector position)
    {
        var x = position.X;
        var y = position.Y;
        var z = position.Z;

        return new[]
        {
            new Vector3d(x, y, z),         // 0: 000
            new Vector3d(x, y, z + 1),     // 1: 001
            new Vector3d(x, y + 1, z),     // 2: 010
            new Vector3d(x, y + 1, z + 1), // 3: 011
            new Vector3d(x + 1, y, z),     // 4: 100
            new Vector3d(x + 1, y, z + 1), // 5: 101
            new Vector3d(x + 1, y + 1, z), // 6: 110
            new Vector3d(x + 1, y + 1, z + 1) // 7: 111
        };
    }

    /// <summary>
    /// Defines the 6 faces of a cube with their directions and vertex indices.
    /// </summary>
    /// <returns>Array of cube face definitions</returns>
    private QuadFace[] GetCubeFaceDefinitions()
    {
        return new[]
        {
            // Front face (Z-) - normal points toward -Z
            new QuadFace(new Vector(0, 0, -1), new[] { 0, 2, 6, 4 }),
            // Back face (Z+) - normal points toward +Z
            new QuadFace(new Vector(0, 0, 1), new[] { 1, 5, 7, 3 }),
            // Left face (X-) - normal points toward -X
            new QuadFace(new Vector(-1, 0, 0), new[] { 0, 1, 3, 2 }),
            // Right face (X+) - normal points toward +X
            new QuadFace(new Vector(1, 0, 0), new[] { 4, 6, 7, 5 }),
            // Bottom face (Y-) - normal points toward -Y
            new QuadFace(new Vector(0, -1, 0), new[] { 0, 4, 5, 1 }),
            // Top face (Y+) - normal points toward +Y
            new QuadFace(new Vector(0, 1, 0), new[] { 2, 3, 7, 6 })
        };
    }

    /// <summary>
    /// Adds a quad face to the mesh using the specified vertices.
    /// </summary>
    /// <param name="mesh">Mesh to add the quad to</param>
    /// <param name="vertices">Array of cube vertices</param>
    /// <param name="indices">Indices of vertices that form the quad</param>
    private void AddQuadFace(DMesh3 mesh, Vector3d[] vertices, int[] indices)
    {
        // Add vertices to mesh
        var meshIndices = new int[4];

        for (int i = 0; i < 4; i++)
        {
            meshIndices[i] = mesh.AppendVertex(vertices[indices[i]]);
        }

        // Add two triangles to form the quad (counter-clockwise winding)
        mesh.AppendTriangle(meshIndices[0], meshIndices[1], meshIndices[2]);
        mesh.AppendTriangle(meshIndices[0], meshIndices[2], meshIndices[3]);
    }
}