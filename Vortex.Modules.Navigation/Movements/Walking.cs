namespace Vortex.Modules.Navigation.Movements;

/// <summary>
/// Walking onto level ground next door, along the axes or corner to corner
/// where that is allowed.
/// </summary>
internal sealed class Walking : IMovement
{
    public void Expand(SearchContext context, Cell from, List<Step> into)
    {
        for (var heading = 0; heading < Headings.Usable(context.Allowed); heading++)
        {
            var direction = Headings.Directions[heading];

            // A corner cannot be squeezed through. The player is wider than a
            // point, so going round one means both blocks beside it have to be
            // out of the way, or it scrapes through geometry the server will not
            // let it through.
            if (Headings.IsDiagonal(heading) && !context.CornerIsClear(from, direction))
                continue;

            var side = from + direction;

            if (context.CanStandAt(side))
                into.Add(new Step(StepKind.Walk, side, Costs.Walk * Headings.Length(direction), heading));
        }
    }
}
