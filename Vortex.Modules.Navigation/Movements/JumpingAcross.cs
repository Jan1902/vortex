using Vortex.Modules.Navigation.Abstraction;

namespace Vortex.Modules.Navigation.Movements;

/// <summary>
/// Jumping over a gap in the floor along one of the eight directions.
/// </summary>
/// <remarks>
/// <para>
/// The landing does not have to be level. Jumping up onto a ledge across a gap
/// and coming down onto one below are the same movement with the same timing;
/// what changes is how far it carries, which is what <see cref="JumpReach"/>
/// knows.
/// </para>
/// <para>
/// The nearest landing wins, and walking is preferred to sprinting wherever
/// both reach: the slower the take-off, the more say there is in where it comes
/// down.
/// </para>
/// </remarks>
internal sealed class JumpingAcross : IMovement
{
    public void Expand(SearchContext context, Cell from, List<Step> into)
    {
        // A jump needs ground to take off from, which water is not.
        if (!context.Allowed.JumpGaps || !context.HasHeadroom(from) || context.IsFloating(from))
            return;

        for (var heading = 0; heading < Headings.Usable(context.Allowed); heading++)
        {
            var direction = Headings.Directions[heading];

            if (Headings.IsDiagonal(heading) && !context.CornerIsClear(from, direction))
                continue;

            // Something to jump over. A floor next door is a walk, and a wall is
            // a step up; neither is this.
            if (!context.IsGap(from + direction))
                continue;

            if (Across(context, from, direction) is { } leap)
                into.Add(leap.Towards(heading));
        }
    }

    /// <summary>
    /// The cheapest jump over a gap in this direction, or null if there is
    /// nothing worth jumping to.
    /// </summary>
    private static Step? Across(SearchContext context, Cell from, Cell direction)
    {
        var length = Headings.Length(direction);
        var reach = JumpReach.Furthest(context.Allowed.Sprint, JumpReach.LowestLanding);

        for (var steps = 2; steps * length <= reach; steps++)
        {
            // Everything flown over has to be clear, head height and all: the
            // player rises more than a block on the way across, so a ceiling
            // turns a jump into a bang on the head and a fall into the gap.
            if (!CanFlyOver(context, from + direction * (steps - 1), direction))
                return null;

            if (LandingAt(context, from, direction, steps) is { } landing)
                return landing;
        }

        return null;
    }

    /// <summary>
    /// Whether the player would pass through a block on the way across without
    /// catching anything.
    /// </summary>
    private static bool CanFlyOver(SearchContext context, Cell over, Cell direction)
        => !context.IsBlocked(over)
        && context.HasHeadroom(over)
        && (direction.X == 0 || direction.Z == 0 || context.CornerIsClear(over, direction));

    /// <summary>
    /// The best landing a given number of blocks out, at whatever height the
    /// jump can be aimed at.
    /// </summary>
    /// <remarks>
    /// Highest first, because coming down onto a ledge is worth more than
    /// dropping past it, and a lower landing is still available from there as a
    /// plain fall.
    /// </remarks>
    private static Step? LandingAt(SearchContext context, Cell from, Cell direction, int steps)
    {
        var distance = steps * Headings.Length(direction);
        var across = from + direction * steps;

        for (var rise = JumpReach.HighestLanding; rise >= JumpReach.LowestLanding; rise--)
        {
            var landing = across with { Y = from.Y + rise };

            if (!context.CanStandAt(landing))
                continue;

            if (distance <= JumpReach.Furthest(sprinting: false, rise))
                return new Step(StepKind.JumpGap, landing, Costs.JumpGap + (steps - 1) * Costs.JumpBlock, Headings.None, Amount: steps - 1);

            if (context.Allowed.Sprint && distance <= JumpReach.Furthest(sprinting: true, rise))
                return new Step(StepKind.JumpGap, landing, Costs.SprintJump + (steps - 1) * Costs.JumpBlock, Headings.None, Amount: steps - 1, Sprinting: true);
        }

        return null;
    }
}
