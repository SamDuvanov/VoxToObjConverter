namespace VoxToObjConverter.Core.Models;

/// <summary>
/// Represents a 3D position with integer coordinates.
/// </summary>
public readonly struct Vector
{
    public int X { get; }

    public int Y { get; }

    public int Z { get; }

    public Vector(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector Add(Vector other)
    {
        return new Vector(X + other.X, Y + other.Y, Z + other.Z);
    }

    public bool Equals(Vector other)
    {
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    public override bool Equals(object obj)
    {
        return obj is Vector other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }
}
