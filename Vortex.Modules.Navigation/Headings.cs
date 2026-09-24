using Vortex.Modules.Navigation.Abstraction;

namespace Vortex.Modules.Navigation;

/// <summary>
/// The ways a move can go, numbered, so that the search can tell a turn from
/// carrying on.
/// </summary>
/// <remarks>
/// The eight neighbours come first, the four along the axes before the four
/// across the corners, so that a search without diagonals can simply take the
/// front of the list. Jumps to landings off all eight carry on after them.
/// </remarks>
internal static class Headings
{
    /// <summary>The heading of a move that goes nowhere sideways, and of the start.</summary>
    public const int None = -1;

    /// <summary>How many of <see cref="Directions"/> run along an axis.</summary>
    public const int Straight = 4;

    /// <summary>The ways out of a block to its neighbours.</summary>
    public static readonly Cell[] Directions =
    [
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 0, 1),
        new(0, 0, -1),

        new(1, 0, 1),
        new(1, 0, -1),
        new(-1, 0, 1),
        new(-1, 0, -1),
    ];

    /// <summary>
    /// Where a jump can land that is neither along an axis nor straight across
    /// a corner, such as two blocks on and one to the side: every such offset
    /// within the furthest reach there is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Their headings carry on after <see cref="Directions"/>, so that turning
    /// into or out of one costs a turn like any other change of way.
    /// </para>
    /// <para>
    /// Each comes with the neighbour its line leaves the block through -- always
    /// one along an axis, since the line never runs through a corner. A jump
    /// only makes sense where that neighbour is a gap; where it is floor, the
    /// player can walk on and jump from there. Ruling a jump out by that one
    /// block, which the search looks at anyway, is what keeps these from costing
    /// anything on ground where there is nothing to jump over.
    /// </para>
    /// </remarks>
    public static readonly (Cell Offset, int Exit)[] OffAxisJumps = OffAxisJumpOffsets().ToArray();

    /// <summary>How many of <see cref="Directions"/> a search may use.</summary>
    public static int Usable(MovementCapabilities allowed)
        => allowed.Diagonals ? Directions.Length : Straight;

    /// <summary>Whether a heading runs across a corner.</summary>
    public static bool IsDiagonal(int heading)
        => heading >= Straight;

    /// <summary>How far a step in a direction actually covers, in blocks.</summary>
    public static double Length(Cell direction)
        => direction.X != 0 && direction.Z != 0 ? Math.Sqrt(2) : 1;

    private static IEnumerable<(Cell Offset, int Exit)> OffAxisJumpOffsets()
    {
        var reach = (int)Math.Floor(JumpReach.Furthest(sprinting: true, JumpReach.LowestLanding));

        for (var dx = -reach; dx <= reach; dx++)
            for (var dz = -reach; dz <= reach; dz++)
            {
                if (dx == 0 || dz == 0 || Math.Abs(dx) == Math.Abs(dz) || dx * dx + dz * dz > reach * reach)
                    continue;

                // Out through the side the line meets first: the one across the
                // axis it covers more of.
                var exit = Math.Abs(dx) > Math.Abs(dz)
                    ? new Cell(Math.Sign(dx), 0, 0)
                    : new Cell(0, 0, Math.Sign(dz));

                yield return (new Cell(dx, 0, dz), Array.IndexOf(Directions, exit));
            }
    }
}
