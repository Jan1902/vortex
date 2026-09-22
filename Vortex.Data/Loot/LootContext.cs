namespace Vortex.Data;

/// <summary>
/// What is known about how a block is broken, which loot conditions are checked
/// against.
/// </summary>
public sealed record LootContext
{
    /// <summary>
    /// The block being broken, or <c>null</c> if its state is unknown, in which
    /// case conditions on the state could go either way.
    /// </summary>
    public BlockState? State { get; init; }

    /// <summary>The tool it is broken with, or <c>null</c> for the bare hand.</summary>
    public Item? Tool { get; init; }

    /// <summary>The tool's enchantments and their levels.</summary>
    public IReadOnlyDictionary<Enchantment, int> Enchantments { get; init; } = new Dictionary<Enchantment, int>();

    /// <summary>Whether the block is destroyed by an explosion rather than mined.</summary>
    public bool Explosion { get; init; }

    /// <summary>
    /// Looks up a block relative to the broken one, by offset. <c>null</c> if the
    /// surroundings are unknown, in which case conditions on them could go
    /// either way.
    /// </summary>
    public Func<int, int, int, BlockState?>? Neighbour { get; init; }

    /// <summary>
    /// The context of mining a block by hand or with a tool.
    /// </summary>
    public static LootContext Mining(BlockState state, Item? tool = null, IReadOnlyDictionary<Enchantment, int>? enchantments = null)
        => new() { State = state, Tool = tool, Enchantments = enchantments ?? new Dictionary<Enchantment, int>() };

    /// <summary>The level of an enchantment on the tool, 0 without it.</summary>
    public int LevelOf(Enchantment enchantment)
        => Enchantments.TryGetValue(enchantment, out var level) ? level : 0;
}
