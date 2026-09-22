namespace Vortex.Modules.Behaviour.Abstraction;

/// <summary>
/// What a task tree is allowed to do, set once for the whole of it by whoever
/// starts it rather than handed down from task to task.
/// </summary>
/// <param name="Sources">Where items may come from, in the order they are tried.</param>
/// <param name="MayDig">Whether routes may go through blocks, breaking them on the way.</param>
public sealed record BehaviourPolicy(IReadOnlyList<ItemSourceKind> Sources, bool MayDig = false)
{
    /// <summary>Every source, cheapest to try first, and no digging.</summary>
    public static BehaviourPolicy Default { get; } = new(
        [ItemSourceKind.Container, ItemSourceKind.Craft, ItemSourceKind.Mine, ItemSourceKind.Collect]);

    /// <summary>Every source, and digging through whatever is in the way.</summary>
    public static BehaviourPolicy Gathering { get; } = Default with { MayDig = true };
}
