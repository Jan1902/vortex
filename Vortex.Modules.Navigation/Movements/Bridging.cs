namespace Vortex.Modules.Navigation.Movements;

/// <summary>
/// Walking across a gap on a block placed in it: against the side of the one
/// the player stands on, from its edge.
/// </summary>
/// <remarks>
/// <para>
/// Straight on only. Across a corner there is no side of the block underfoot
/// facing the gap to place against.
/// </para>
/// <para>
/// The gap may be anything a block can be put into: air, water, or lava. What
/// is in it now does not matter once the block is there, so only the room above
/// has to be safe.
/// </para>
/// </remarks>
internal sealed class Bridging : IMovement
{
    public void Expand(SearchContext context, Cell from, List<Step> into)
    {
        // The block the player stands on is what the new one goes against.
        // Anywhere the search gets to, other than water, has one -- where the
        // world does not have it yet, it is the one the last move placed.
        if (!context.Allowed.Build || context.Allowed.Loadout.Blocks <= 0 || context.IsFloating(from))
            return;

        for (var heading = 0; heading < Headings.Straight; heading++)
        {
            var side = from + Headings.Directions[heading];
            var support = side.Below;

            if (context.IsBlocked(side) || !context.IsReplaceable(support))
                continue;

            if (context.IsHarmful(side) || context.IsHarmful(side.Above) || context.Drowns(side.Above))
                continue;

            into.Add(new Step(StepKind.Bridge, side, Costs.Bridge, heading));
        }
    }
}
