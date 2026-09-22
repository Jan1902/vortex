using Vortex.Data;

namespace Vortex.Modules.Behaviour.Abstraction;

/// <summary>
/// A number of items to carry, where any of several kinds will do: four
/// planks, of whichever wood.
/// </summary>
/// <param name="Items">The kinds that count towards the request.</param>
/// <param name="Count">How many of them, all kinds together, the bot should carry.</param>
public sealed record ItemRequest(IReadOnlySet<Item> Items, int Count)
{
    /// <summary>A number of one kind of item.</summary>
    public static ItemRequest Of(Item item, int count)
        => new(new HashSet<Item> { item }, count);

    /// <summary>How many of the requested kinds a count function reports, all together.</summary>
    public int CountIn(Func<Item, int> count)
        => Items.Sum(count);

    public override string ToString()
        => Items.Count switch
        {
            1 => $"{Count} {Items.First()}",
            <= 3 => $"{Count} {string.Join(" or ", Items.Order())}",
            _ => $"{Count} {Items.Order().First()} or one of {Items.Count - 1} alike",
        };
}

/// <summary>
/// The items being obtained further up the task tree, so that none of them is
/// set out to be obtained through itself -- iron ingots from iron blocks from
/// iron ingots.
/// </summary>
public sealed record ObtainChain(IReadOnlySet<Item> Items)
{
    public static ObtainChain Empty { get; } = new(new HashSet<Item>());

    public bool Contains(Item item)
        => Items.Contains(item);

    /// <summary>This chain with more items being obtained further up.</summary>
    public ObtainChain With(IEnumerable<Item> items)
        => new(new HashSet<Item>(Items.Concat(items)));
}
