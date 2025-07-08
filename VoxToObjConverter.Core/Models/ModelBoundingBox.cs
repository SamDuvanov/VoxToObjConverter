namespace VoxToObjConverter.Core.Models;

/// <summary>
/// Represents the bounding box of a 3D model.
/// </summary>
public readonly struct ModelBoundingBox
{
    public int MinX { get; }

    public int MaxX { get; }

    public int MinY { get; }

    public int MaxY { get; }

    public int MinZ { get; }

    public int MaxZ { get; }

    public ModelBoundingBox(int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
        MinZ = minZ;
        MaxZ = maxZ;
    }
}
