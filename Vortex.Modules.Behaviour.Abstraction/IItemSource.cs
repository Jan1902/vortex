namespace Vortex.Modules.Behaviour.Abstraction;

/// <summary>
/// Where items can come from. Which kinds a task tree may use, and in what
/// order, is up to the <see cref="BehaviourPolicy"/> it runs under.
/// </summary>
public enum ItemSourceKind
{
    /// <summary>Taking them out of a container the bot has looked into before.</summary>
    Container,

    /// <summary>Crafting them from other items.</summary>
    Craft,

    /// <summary>Breaking blocks that drop them.</summary>
    Mine,

    /// <summary>Picking them up where they lie.</summary>
    Collect,
}

/// <summary>
/// One way of getting items: from a chest, by crafting, by mining.
/// </summary>
/// <remarks>
/// <para>
/// A source only knows how to do its own part. When it needs other items to do
/// it -- ingredients, a tool -- its task asks for them as an
/// <see cref="ItemRequest"/> of its own, and some source, possibly another
/// kind, answers that in turn. No source knows where the items it relies on
/// come from.
/// </para>
/// <para>
/// A source offers what it has reason to think will work, from what the bot
/// knows about the world: a chest it remembers the item in, a block it can see.
/// It is not asked to be certain. When an offer fails anyway, the task asking
/// sets it aside for a while and takes the next.
/// </para>
/// </remarks>
public interface IItemSource
{
    ItemSourceKind Kind { get; }

    /// <summary>
    /// The ways this source sees to bring the carried count up towards a
    /// request, best first. Each need not cover the whole request; the task
    /// asking comes back for more.
    /// </summary>
    /// <param name="request">The items wanted, and how many to carry in all.</param>
    /// <param name="chain">What is being obtained further up, and so is no way to get this.</param>
    IEnumerable<ItemSourceOption> Options(ItemRequest request, ObtainChain chain);
}

/// <summary>
/// One offer from an <see cref="IItemSource"/>.
/// </summary>
/// <param name="Key">
/// Names the offer independently of the counts in it -- "craft OakPlanks",
/// "container at 10 64 3" -- so that it can be set aside once it failed.
/// </param>
/// <param name="Task">What to run to take it up.</param>
public sealed record ItemSourceOption(string Key, BotTask Task);
