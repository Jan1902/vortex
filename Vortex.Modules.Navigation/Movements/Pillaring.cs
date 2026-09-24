namespace Vortex.Modules.Navigation.Movements;

/// <summary>
/// Going straight up by jumping and placing a block where the feet were.
/// </summary>
/// <remarks>
/// <para>
/// The way up out of a hole or a ravine, or onto a ledge too high to step onto,
/// for a player that has blocks and may use them.
/// </para>
/// <para>
/// All it needs is room for the head one block higher. A ceiling lower than
/// that is left alone rather than dug through: the jump that places the block
/// needs the room before the block is down, and digging upwards is its own
/// move.
/// </para>
/// </remarks>
internal sealed class Pillaring : IMovement
{
    public void Expand(SearchContext context, Cell from, List<Step> into)
    {
        // Nothing to push off from while swimming. Anywhere else the search
        // gets to has a floor, even where the world does not have it yet
        // because it is the block the last move placed.
        if (!context.Allowed.Build || context.Allowed.Loadout.Blocks <= 0 || context.IsFloating(from))
            return;

        var up = from.Above;

        // The new head goes where there was nothing but air above the old one,
        // and the block goes on top of the floor, where the feet are now.
        if (!context.HasHeadroom(from) || !context.IsSafeAt(up) || !context.IsReplaceable(from))
            return;

        into.Add(new Step(StepKind.Pillar, up, Costs.Pillar, Headings.None));
    }
}
