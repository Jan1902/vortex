namespace Vortex.Data;

/// <summary>
/// What a block drops when it is broken.
/// </summary>
/// <param name="Pools">Each pool hands out its own items, independently of the others.</param>
/// <param name="Functions">Changes applied to everything the table drops.</param>
public sealed record LootTable(Block Block, LootPool[] Pools, LootFunction[] Functions)
{
    /// <summary>
    /// The items breaking the block can drop in a context, whether certainly or
    /// only by chance.
    /// </summary>
    /// <remarks>
    /// What a block entity holds, such as a shulker box's contents, is not
    /// included; only the block entity knows that.
    /// </remarks>
    public IReadOnlySet<Item> PossibleDrops(LootContext context)
    {
        var drops = new HashSet<Item>();

        foreach (var pool in Pools)
        {
            if (!pool.Conditions.CanAllPass(context) || pool.Rolls.Maximum + pool.BonusRolls.Maximum <= 0)
                continue;

            foreach (var entry in pool.Entries)
                entry.CollectPossibleDrops(context, drops);
        }

        return drops;
    }
}

/// <summary>
/// A part of a loot table that picks from its entries a number of times.
/// </summary>
/// <param name="Rolls">How many times it picks.</param>
/// <param name="BonusRolls">Extra picks per point of luck.</param>
public sealed record LootPool(LootNumber Rolls, LootNumber BonusRolls, LootEntry[] Entries, LootCondition[] Conditions, LootFunction[] Functions);

/// <summary>
/// The loot tables of all blocks, generated from Mojang's loot table files.
/// </summary>
public static partial class LootTables
{
    // Tables are built on first use: most blocks never get broken.
    private static readonly Lazy<LootTable?>[] _tables = Enum.GetValues<Block>()
        .Select(block => new Lazy<LootTable?>(() => Create(block)))
        .ToArray();

    /// <summary>
    /// Gets the loot table of a block.
    /// </summary>
    /// <returns>The table, or <c>null</c> for a block without one, such as air.</returns>
    public static LootTable? For(Block block)
        => _tables[(int)block].Value;

    /// <summary>
    /// The items mining a block with a tool can drop.
    /// </summary>
    /// <remarks>
    /// A block that needs the right tool, such as stone, drops nothing at all
    /// without it. The game decides that before it looks at the loot table,
    /// which is why the table itself does not say so.
    /// </remarks>
    public static IReadOnlySet<Item> PossibleDrops(BlockState state, Item? tool = null, IReadOnlyDictionary<Enchantment, int>? enchantments = null)
    {
        if (!Mining.CanHarvest(state.Block, tool))
            return new HashSet<Item>();

        return For(state.Block)?.PossibleDrops(LootContext.Mining(state, tool, enchantments)) ?? new HashSet<Item>();
    }

    private static partial LootTable? Create(Block block);
}
