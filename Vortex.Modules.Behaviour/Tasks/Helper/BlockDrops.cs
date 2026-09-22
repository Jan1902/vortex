using Vortex.Data;

namespace Vortex.Modules.Behaviour.Tasks.Helper;

/// <summary>
/// Which blocks an item can be mined from: the loot tables read backwards.
/// </summary>
/// <remarks>
/// <para>
/// Worked out once, on first use, from each block's default state broken with
/// the bare hand and with every tool there is. The state a block is actually
/// in, and whether the tool at hand will do, is for whoever mines it to check
/// against the real block.
/// </para>
/// <para>
/// Blocks that only give themselves back, as something that can be crafted,
/// are left out: planks, wool, bricks. Those are what people build with, and
/// taking a house apart for its planks is not getting planks.
/// </para>
/// </remarks>
internal static class BlockDrops
{
    private static readonly Lazy<Dictionary<Item, HashSet<Block>>> _sources = new(Build);

    private static readonly Lazy<Item[]> _tools = new(() => Enum.GetValues<Item>().Where(item => item.Tool() is not null).ToArray());

    /// <summary>Every item that is a tool.</summary>
    public static IReadOnlyList<Item> Tools => _tools.Value;

    /// <summary>The blocks any of the items can be mined from.</summary>
    public static IReadOnlySet<Block> Producing(IEnumerable<Item> items)
    {
        var blocks = new HashSet<Block>();

        foreach (var item in items)
            if (_sources.Value.TryGetValue(item, out var producers))
                blocks.UnionWith(producers);

        return blocks;
    }

    /// <summary>The tools that get any of the items out of a block, the bare hand not being one of them.</summary>
    public static IReadOnlySet<Item> ToolsFor(BlockState state, IReadOnlySet<Item> items)
        => Tools.Where(tool => LootTables.PossibleDrops(state, tool).Overlaps(items)).ToHashSet();

    private static Dictionary<Item, HashSet<Block>> Build()
    {
        var sources = new Dictionary<Item, HashSet<Block>>();

        foreach (var block in Enum.GetValues<Block>())
        {
            var state = BlockState.Default(block);

            var drops = Tools.Select(tool => (Item?)tool).Prepend(null)
                .SelectMany(tool => LootTables.PossibleDrops(state, tool))
                .ToHashSet();

            if (drops.Count == 1 && IsBuiltWith(block, drops.First()))
                continue;

            foreach (var item in drops)
            {
                if (!sources.TryGetValue(item, out var blocks))
                    sources[item] = blocks = [];

                blocks.Add(block);
            }
        }

        return sources;
    }

    /// <summary>Whether a block that drops only this item is most likely something someone built.</summary>
    private static bool IsBuiltWith(Block block, Item item)
        => block.ToString() == item.ToString() && CraftingPlanner.CraftingRecipes(item).Any();
}
