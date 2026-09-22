using Vortex.Data;


namespace Vortex.Shared;

public record Vector3i(int X, int Y, int Z)
{
    public static Vector3i Zero { get; } = new(0, 0, 0);

    public static Vector3i operator +(Vector3i left, Vector3i right)
        => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static Vector3i operator -(Vector3i left, Vector3i right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public static Vector3i operator *(Vector3i vector, int factor)
        => new(vector.X * factor, vector.Y * factor, vector.Z * factor);
}
public record Vector2i(int X, int Z);
public record Vector3f(float X, float Y, float Z);

/// <summary>
/// A position or offset in world space. Entity positions are sent as doubles.
/// </summary>
public record Vector3d(double X, double Y, double Z)
{
    public static Vector3d Zero { get; } = new(0, 0, 0);

    public static Vector3d operator +(Vector3d left, Vector3d right)
        => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static Vector3d operator -(Vector3d left, Vector3d right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public static Vector3d operator *(Vector3d vector, double factor)
        => new(vector.X * factor, vector.Y * factor, vector.Z * factor);

    /// <summary>
    /// The block position this point sits in.
    /// </summary>
    public Vector3i ToBlockPosition()
        => new((int)Math.Floor(X), (int)Math.Floor(Y), (int)Math.Floor(Z));

    public double HorizontalDistanceTo(Vector3d other)
    {
        var dx = X - other.X;
        var dz = Z - other.Z;

        return Math.Sqrt(dx * dx + dz * dz);
    }
}

public record Chunk(ChunkSection[] Sections);
public record ChunkSection(BlockState?[,,] States);
