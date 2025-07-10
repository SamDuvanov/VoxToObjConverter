using g3;
using gs;

namespace VoxToObjConverter.Core.Services.MeshServices;

/// <summary>
/// Provides functionality to clean and optimize a 3D mesh by
/// removing duplicates, merging edges, compacting, and recomputing normals.
/// </summary>
public class MeshWelder
{
    private readonly DMesh3 _mesh;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeshWelder"/> class.
    /// </summary>
    /// <param name="mesh">The input mesh to be processed.</param>
    public MeshWelder(DMesh3 mesh)
    {
        _mesh = mesh;
    }

    /// <summary>
    /// Performs welding operations on the mesh:
    /// removes duplicate triangles, merges coincident edges,
    /// compacts the mesh data, recomputes normals, and checks validity.
    /// </summary>
    public void Weld()
    {
        RemoveDuplicateTriangles();
        MergeCoincidentEdges();
        CompactMesh();
        RecomputeNormals();
        ValidateMesh();
    }

    /// <summary>
    /// Removes duplicate triangles from the mesh.
    /// </summary>
    private void RemoveDuplicateTriangles()
    {
        var remover = new RemoveDuplicateTriangles(_mesh);
        remover.Apply();
    }

    /// <summary>
    /// Merges edges that are geometrically coincident.
    /// </summary>
    private void MergeCoincidentEdges()
    {
        var merger = new MergeCoincidentEdges(_mesh);
        merger.Apply();
    }

    /// <summary>
    /// Compacts the mesh in-place to optimize memory and structure.
    /// </summary>
    private void CompactMesh()
    {
        _mesh.CompactInPlace();
    }

    /// <summary>
    /// Quickly recomputes vertex normals for the mesh.
    /// </summary>
    private void RecomputeNormals()
    {
        MeshNormals.QuickCompute(_mesh);
    }

    /// <summary>
    /// Checks the validity of the mesh structure.
    /// </summary>
    private void ValidateMesh()
    {
        _mesh.CheckValidity();
    }
}
