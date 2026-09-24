namespace Vortex.Modules.Navigation.Movements;

/// <summary>
/// Breaking through what stands in the way at body height, to walk into where
/// it was.
/// </summary>
/// <remarks>
/// Straight on only. What is on the other side does not have to be floor: a
/// tunnel that breaks into a cave drops into it, as far as a plain drop would.
/// </remarks>
internal sealed class Tunnelling : IMovement
{
    public void Expand(SearchContext context, Cell from, List<Step> into)
    {
        if (!context.Allowed.Dig || context.IsFloating(from))
            return;

        for (var heading = 0; heading < Headings.Straight; heading++)
        {
            var side = from + Headings.Directions[heading];

            if (context.IsBlocked(side) && Through(context, side) is { } tunnel)
                into.Add(tunnel.Towards(heading));
        }
    }

    private static Step? Through(SearchContext context, Cell side)
    {
        var blocking = default(BlockList);
        ReadOnlySpan<Cell> body = [side.Above, side];

        foreach (var position in body)
            if (context.NeedsBreaking(position))
                blocking = blocking.With(position);

        if (blocking.Count == 0 || context.BreakCost(blocking) is not { } breaking)
            return null;

        // Something has to hold the player up over there, or catch it not far
        // below.
        if (context.IsFloor(side.Below))
            return new Step(StepKind.MineThrough, side, Costs.Walk + breaking, Headings.None, Breaks: blocking);

        if (context.NeedsBreaking(side.Below) || Dropping.Landing(context, side, startCleared: true) is not { } landing)
            return null;

        var cost = Costs.Walk + breaking + Costs.Drop + landing.Height * Costs.FallPerBlock;

        return new Step(StepKind.MineThrough, landing.At, cost, Headings.None, Amount: landing.Height, Breaks: blocking);
    }
}
