using Vortex.Shared;

namespace Vortex.Modules.Interaction.Abstraction;

/// <summary>
/// A side of a block, numbered the way the protocol numbers them.
/// </summary>
public enum BlockFace
{
    Down = 0,
    Up = 1,
    North = 2,
    South = 3,
    West = 4,
    East = 5
}

/// <summary>A hand to act with.</summary>
public enum Hand
{
    Main = 0,
    Off = 1
}

public static class BlockFaces
{
    /// <summary>The offset from a block to its neighbour on this side.</summary>
    public static Vector3i Offset(this BlockFace face)
        => face switch
        {
            BlockFace.Down => new Vector3i(0, -1, 0),
            BlockFace.Up => new Vector3i(0, 1, 0),
            BlockFace.North => new Vector3i(0, 0, -1),
            BlockFace.South => new Vector3i(0, 0, 1),
            BlockFace.West => new Vector3i(-1, 0, 0),
            _ => new Vector3i(1, 0, 0),
        };

    /// <summary>The side facing the other way.</summary>
    public static BlockFace Opposite(this BlockFace face)
        => face switch
        {
            BlockFace.Down => BlockFace.Up,
            BlockFace.Up => BlockFace.Down,
            BlockFace.North => BlockFace.South,
            BlockFace.South => BlockFace.North,
            BlockFace.West => BlockFace.East,
            _ => BlockFace.West,
        };

    /// <summary>
    /// The side of a block that faces a point, such as the eyes of whoever
    /// looks at it: the one across which the point lies furthest out.
    /// </summary>
    public static BlockFace Facing(Vector3i block, Vector3d point)
    {
        var dx = point.X - (block.X + 0.5);
        var dy = point.Y - (block.Y + 0.5);
        var dz = point.Z - (block.Z + 0.5);

        if (Math.Abs(dy) >= Math.Abs(dx) && Math.Abs(dy) >= Math.Abs(dz))
            return dy >= 0 ? BlockFace.Up : BlockFace.Down;

        if (Math.Abs(dx) >= Math.Abs(dz))
            return dx >= 0 ? BlockFace.East : BlockFace.West;

        return dz >= 0 ? BlockFace.South : BlockFace.North;
    }

    /// <summary>The middle of a side of a block, in world coordinates.</summary>
    public static Vector3d Center(Vector3i block, BlockFace face)
    {
        var offset = face.Offset();

        return new Vector3d(block.X + 0.5 + offset.X * 0.5, block.Y + 0.5 + offset.Y * 0.5, block.Z + 0.5 + offset.Z * 0.5);
    }
}
