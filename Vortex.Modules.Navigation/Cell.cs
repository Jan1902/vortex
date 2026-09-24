using Vortex.Shared;

namespace Vortex.Modules.Navigation;

/// <summary>
/// A block position, the way the search handles it.
/// </summary>
/// <remarks>
/// <see cref="Vector3i"/> is a class, so every neighbour the search looks at
/// would be an allocation, and every lookup by position a comparison through a
/// reference. The search looks at millions of them. This is the same three
/// numbers as a value, turned back into a <see cref="Vector3i"/> only for what
/// leaves the search.
/// </remarks>
internal readonly record struct Cell(int X, int Y, int Z)
{
    public Cell Above
        => new(X, Y + 1, Z);

    public Cell Below
        => new(X, Y - 1, Z);

    public static Cell operator +(Cell left, Cell right)
        => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static Cell operator -(Cell left, Cell right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public static Cell operator *(Cell cell, int factor)
        => new(cell.X * factor, cell.Y * factor, cell.Z * factor);

    public static implicit operator Cell(Vector3i position)
        => new(position.X, position.Y, position.Z);

    public Vector3i ToVector3i()
        => new(X, Y, Z);

    public override string ToString()
        => $"{X} {Y} {Z}";
}
