namespace VoxToObjConverter.Core.Models;

/// <summary>
/// Represents a face of a cube with its direction and vertex indices.
/// </summary>
public readonly struct QuadFace
{
    public Vector Direction { get; }

    public int[] VertexIndices { get; }

    public QuadFace(Vector direction, int[] vertexIndices)
    {
        Direction = direction;
        VertexIndices = vertexIndices;
    }
}