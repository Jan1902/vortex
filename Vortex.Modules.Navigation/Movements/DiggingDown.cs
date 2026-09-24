namespace Vortex.Modules.Navigation.Movements;

/// <summary>
/// Straight down, by taking the floor out from under the player.
/// </summary>
/// <remarks>
/// One block at a time, onto whatever is under it -- or, where that is the
/// roof of a cave, down into the cave, as far as a plain drop would go.
/// </remarks>
internal sealed class DiggingDown : IMovement
{
    public void Expand(SearchContext context, Cell from, List<Step> into)
    {
        if (!context.Allowed.Dig || context.IsFloating(from))
            return;

        var floor = from.Below;

        if (!context.NeedsBreaking(floor) || !context.IsSafeAt(floor) || context.BreakCost(default(BlockList).With(floor)) is not { } breaking)
            return;

        var toBreak = default(BlockList).With(floor);

        if (context.IsFloor(floor.Below))
        {
            into.Add(new Step(StepKind.MineThrough, floor, Costs.Drop + breaking, Headings.None, Amount: 1, Breaks: toBreak));

            return;
        }

        // Nothing under the floor: a hollow, and the fall into it starts one
        // block up, where the player's feet were.
        if (context.NeedsBreaking(floor.Below) || Dropping.Landing(context, floor, alreadyFallen: 1, startCleared: true) is not { } landing)
            return;

        var cost = Costs.Drop + breaking + landing.Height * Costs.FallPerBlock;

        into.Add(new Step(StepKind.MineThrough, landing.At, cost, Headings.None, Amount: landing.Height, Breaks: toBreak));
    }
}
