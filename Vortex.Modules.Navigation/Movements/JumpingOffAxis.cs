using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Movements;

/// <summary>
/// A jump to a block that lies off every one of the eight directions, such as
/// two on and one to the side.
/// </summary>
/// <remarks>
/// Judged the way the player flies it: in a straight line from the middle of
/// the block to the middle of the landing, so everything the player's box
/// sweeps over on that line has to be clear, and the reach is the distance
/// between the two middles. As with the other jumps, the highest landing wins
/// and walking is preferred to sprinting wherever both reach.
/// </remarks>
internal sealed class JumpingOffAxis : IMovement
{
    /// <summary>How many points along the line of flight are checked.</summary>
    private const int Samples = 20;

    public void Expand(SearchContext context, Cell from, List<Step> into)
    {
        if (!context.Allowed.JumpGaps || !context.Allowed.Diagonals || !context.HasHeadroom(from) || context.IsFloating(from))
            return;

        // Which of the neighbours along the axes are gaps, one bit per heading.
        var gaps = 0;

        for (var heading = 0; heading < Headings.Straight; heading++)
            if (context.IsGap(from + Headings.Directions[heading]))
                gaps |= 1 << heading;

        if (gaps == 0)
            return;

        for (var i = 0; i < Headings.OffAxisJumps.Length; i++)
        {
            var (offset, exit) = Headings.OffAxisJumps[i];

            if ((gaps & (1 << exit)) == 0)
                continue;

            if (Jump(context, from, offset) is { } leap)
                into.Add(leap.Towards(Headings.Directions.Length + i));
        }
    }

    private static Step? Jump(SearchContext context, Cell from, Cell offset)
    {
        var distance = Math.Sqrt(offset.X * offset.X + offset.Z * offset.Z);
        var gap = Math.Max(Math.Abs(offset.X), Math.Abs(offset.Z)) - 1;

        for (var rise = JumpReach.HighestLanding; rise >= JumpReach.LowestLanding; rise--)
        {
            var sprinting = distance > JumpReach.Furthest(sprinting: false, rise);

            if (sprinting && (!context.Allowed.Sprint || distance > JumpReach.Furthest(sprinting: true, rise)))
                continue;

            var landing = new Cell(from.X + offset.X, from.Y + rise, from.Z + offset.Z);

            if (!context.CanStandAt(landing) || !CanFlyTo(context, from, landing))
                continue;

            return sprinting
                ? new Step(StepKind.JumpGap, landing, Costs.SprintJump + gap * Costs.JumpBlock, Headings.None, Amount: gap, Sprinting: true)
                : new Step(StepKind.JumpGap, landing, Costs.JumpGap + gap * Costs.JumpBlock, Headings.None, Amount: gap);
        }

        return null;
    }

    /// <summary>
    /// Whether the straight flight from one block to a landing off to the side
    /// actually crosses a gap, and is clear all the way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The gap comes first, because it is cheap to ask and on open ground the
    /// answer is always no: where every block under the line could be walked
    /// on, there is nothing to jump over and walking gets there anyway.
    /// </para>
    /// <para>
    /// Then every column the player's box passes over has to be clear at body
    /// height and the block above, as for any jump. A column passed over at
    /// more than one point is asked about each time; the answers are kept, so
    /// that is cheaper than remembering which ones have been seen.
    /// </para>
    /// </remarks>
    private static bool CanFlyTo(SearchContext context, Cell from, Cell landing)
    {
        var startX = from.X + 0.5;
        var startZ = from.Z + 0.5;

        var dx = landing.X - from.X;
        var dz = landing.Z - from.Z;

        var crossesGap = false;

        for (var sample = 1; sample < Samples && !crossesGap; sample++)
        {
            var along = (double)sample / Samples;
            var under = new Cell((int)Math.Floor(startX + dx * along), from.Y, (int)Math.Floor(startZ + dz * along));

            if (under != from && (under.X != landing.X || under.Z != landing.Z) && !context.CanStandAt(under))
                crossesGap = true;
        }

        if (!crossesGap)
            return false;

        for (var sample = 0; sample <= Samples; sample++)
        {
            var along = (double)sample / Samples;
            var (minX, maxX, minZ, maxZ) = PlayerHitbox.ColumnBounds(startX + dx * along, startZ + dz * along);

            for (var x = minX; x <= maxX; x++)
                for (var z = minZ; z <= maxZ; z++)
                {
                    var over = new Cell(x, from.Y, z);

                    if (over == from)
                        continue;

                    // Jumping up onto a landing, its column at take-off height
                    // is the very block landed on.
                    if (x == landing.X && z == landing.Z && landing.Y > from.Y)
                        continue;

                    if (context.IsBlocked(over) || !context.HasHeadroom(over))
                        return false;
                }
        }

        return true;
    }
}
