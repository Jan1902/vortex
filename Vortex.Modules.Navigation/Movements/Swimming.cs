namespace Vortex.Modules.Navigation.Movements;

/// <summary>
/// Swimming at the surface of still water: from one block of it to the next,
/// or into it from the bank.
/// </summary>
/// <remarks>
/// <para>
/// Only ever level. Getting out onto a bank the same height as the water is a
/// plain walk, onto one a block higher a step up, both offered by their own
/// movements. Getting in from higher up is a drop.
/// </para>
/// <para>
/// No diving and no currents: flowing water is never swum in, only fallen into
/// where it is shallow enough to stand in.
/// </para>
/// </remarks>
internal sealed class Swimming : IMovement
{
    public void Expand(SearchContext context, Cell from, List<Step> into)
    {
        for (var heading = 0; heading < Headings.Usable(context.Allowed); heading++)
        {
            var direction = Headings.Directions[heading];

            if (Headings.IsDiagonal(heading) && !context.CornerIsClear(from, direction))
                continue;

            var side = from + direction;

            if (context.IsFloating(side))
                into.Add(new Step(StepKind.Swim, side, Costs.Swim * Headings.Length(direction), heading));
        }
    }
}
