namespace Vortex.Modules.Navigation.Movements;

/// <summary>
/// Stepping up onto what is in the way, after clearing whatever sits on top of
/// it and over the player's own head.
/// </summary>
internal sealed class DiggingUp : IMovement
{
    public void Expand(SearchContext context, Cell from, List<Step> into)
    {
        // The cheap questions first: this runs for every way out of every
        // position the search looks at.
        if (!context.Allowed.Dig || context.IsFloating(from) || !context.IsSolid(from.Below))
            return;

        for (var heading = 0; heading < Headings.Straight; heading++)
        {
            var side = from + Headings.Directions[heading];

            // What is in the way is what gets stood on, so it has to be a floor.
            if (!context.IsFloor(side) || !context.IsSafeAt(side.Above))
                continue;

            if (Up(context, from, side) is { } digUp)
                into.Add(digUp.Towards(heading));
        }
    }

    private static Step? Up(SearchContext context, Cell from, Cell side)
    {
        var toBreak = default(BlockList);
        ReadOnlySpan<Cell> inTheWay = [side.Above, side.Above.Above, from.Above.Above];

        foreach (var block in inTheWay)
            if (context.NeedsBreaking(block))
                toBreak = toBreak.With(block);

        // Nothing in the way after all: that is a plain step up, which is
        // offered by its own movement.
        if (toBreak.Count == 0 || context.BreakCost(toBreak) is not { } breaking)
            return null;

        return new Step(StepKind.MineThrough, side.Above, breaking + Costs.StepUp, Headings.None, Breaks: toBreak);
    }
}
