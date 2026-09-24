namespace Vortex.Modules.Navigation.Movements;

/// <summary>
/// Stepping off an edge, where the floor next door is missing but something
/// below it is not.
/// </summary>
/// <remarks>
/// <para>
/// Any floor will do, a single block with nothing beyond it included: the
/// movement lets go before the edge and brakes in the air, so it comes down on
/// the block it was aimed at rather than one further on.
/// </para>
/// <para>
/// Water breaks a fall of any height, so a drop into still water may go as far
/// down as there is to go, and ends floating at the surface.
/// </para>
/// </remarks>
internal sealed class Dropping : IMovement
{
    /// <summary>
    /// How far the player is willing to drop in one step onto something hard.
    /// </summary>
    /// <remarks>
    /// Three blocks is the most that costs no health. Going further needs
    /// something to break the fall, such as water.
    /// </remarks>
    public const int MaxFallHeight = 3;

    /// <summary>
    /// How far down to look for water to land in. Water breaks any fall, but a
    /// search that follows every edge all the way to the bottom of the world
    /// spends a long time on edges with nothing at the bottom.
    /// </summary>
    public const int MaxWaterFall = 64;

    public void Expand(SearchContext context, Cell from, List<Step> into)
    {
        // Nothing to step off from while swimming.
        if (context.IsFloating(from))
            return;

        for (var heading = 0; heading < Headings.Usable(context.Allowed); heading++)
        {
            var direction = Headings.Directions[heading];

            if (Headings.IsDiagonal(heading) && !context.CornerIsClear(from, direction))
                continue;

            var side = from + direction;

            // Water level with the feet is swum into, not dropped into.
            if (!context.IsGap(side) || context.IsWater(side))
                continue;

            if (Landing(context, side) is { } landing)
            {
                var cost = (Costs.Drop + landing.Height * Costs.FallPerBlock) * Headings.Length(direction);

                into.Add(new Step(StepKind.Drop, landing.At, cost, heading, Amount: landing.Height));
            }
        }
    }

    /// <summary>
    /// Where a player falling from a block ends up, and how far down that is,
    /// or null if it is too far down or not safe.
    /// </summary>
    /// <remarks>
    /// Shared with the digging that opens a way down, which ends in the same
    /// fall.
    /// </remarks>
    /// <param name="start">The block the fall starts in.</param>
    /// <param name="alreadyFallen">How far above <paramref name="start"/> the fall really began.</param>
    /// <param name="startCleared">
    /// Whether <paramref name="start"/> is still solid and only about to be dug
    /// out, so that it does not count against the room for the head just below
    /// it.
    /// </param>
    public static (Cell At, int Height)? Landing(SearchContext context, Cell start, int alreadyFallen = 0, bool startCleared = false)
    {
        for (var drop = 1; drop + alreadyFallen <= MaxWaterFall; drop++)
        {
            var landing = start with { Y = start.Y - drop };
            var height = drop + alreadyFallen;

            if (context.IsWater(landing))
            {
                // Water breaks the fall, but only still water is somewhere to
                // stay: in shallow water there is floor to stand on, in deep
                // water the player floats.
                return context.CanStandAt(landing) || context.CanFloatAt(landing)
                    ? (landing, height)
                    : null;
            }

            var standable = drop == 1 && startCleared
                ? context.IsPassable(landing) && context.IsFloor(landing.Below) && context.IsSafeAt(landing)
                : context.CanStandAt(landing);

            if (standable)
                return height <= MaxFallHeight ? (landing, height) : null;

            if (!context.IsPassable(landing))
                return null;
        }

        return null;
    }
}
