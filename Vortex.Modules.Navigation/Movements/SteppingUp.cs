namespace Vortex.Modules.Navigation.Movements;

/// <summary>
/// Stepping onto something in the way at body height, where its top is clear
/// and there is room to jump.
/// </summary>
/// <remarks>
/// Straight on only: a step up taken across a corner catches the edge as often
/// as it clears it.
/// </remarks>
internal sealed class SteppingUp : IMovement
{
    public void Expand(SearchContext context, Cell from, List<Step> into)
    {
        if (!context.HasHeadroom(from))
            return;

        for (var heading = 0; heading < Headings.Straight; heading++)
        {
            var side = from + Headings.Directions[heading];

            if (context.IsBlocked(side) && context.CanStandAt(side.Above))
                into.Add(new Step(StepKind.StepUp, side.Above, Costs.StepUp, heading));
        }
    }
}
