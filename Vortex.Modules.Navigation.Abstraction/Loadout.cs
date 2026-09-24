using Vortex.Data;

namespace Vortex.Modules.Navigation.Abstraction;

/// <summary>
/// What the player carries, as far as getting somewhere is concerned: what it
/// can break blocks with, and how many blocks it has to build with.
/// </summary>
/// <remarks>
/// <para>
/// A search does not look into the inventory itself. It is told what there is,
/// once, when it starts, and plans with that: a stone wall is a few moments
/// with a pickaxe and a long wait with bare hands, and a gap can only be
/// bridged by a player who has blocks to bridge it with.
/// </para>
/// <para>
/// Which of the blocks carried may be used up that way is the caller's
/// decision, not the search's.
/// </para>
/// </remarks>
/// <param name="Tools">What there is to break blocks with, besides the bare hand.</param>
/// <param name="Blocks">How many blocks may be placed on the way.</param>
public sealed record Loadout(IReadOnlyList<Item> Tools, int Blocks)
{
    /// <summary>Nothing but bare hands.</summary>
    public static Loadout Empty { get; } = new([], 0);

    /// <summary>
    /// How many ticks breaking a block takes with the best of what there is,
    /// or null for a block that cannot be broken at all.
    /// </summary>
    public int? BreakTicks(Block block)
    {
        var best = Mining.BreakTicks(block, tool: null);

        foreach (var tool in Tools)
            if (Mining.BreakTicks(block, tool) is { } ticks && (best is null || ticks < best))
                best = ticks;

        return best;
    }
}
