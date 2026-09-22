using Microsoft.Extensions.Logging;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Knowledge;
using Vortex.Modules.Inventory.Abstraction;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Carries a number of items, from wherever they can be had.
/// </summary>
/// <remarks>
/// <para>
/// The one task to name when some items are needed. It does not know where
/// items come from; the <see cref="IItemSource"/>s do, and this asks them in the
/// order the running <see cref="BehaviourPolicy"/> gives, taking the first offer
/// that has not failed lately.
/// </para>
/// <para>
/// When an offer fails, it is written into <see cref="FailureMemory"/> and the
/// next round takes the next one -- the next chest, the next tree, crafting
/// instead of taking. Nothing about that is remembered here, so the task stays
/// as stateless as any other.
/// </para>
/// </remarks>
public class ObtainItemsTask(
    ItemRequest request,
    ObtainChain chain,
    IInventoryManager inventory,
    IEnumerable<IItemSource> sources,
    IBotBrain brain,
    FailureMemory failures,
    ILogger<ObtainItemsTask> logger) : BotTask
{
    /// <summary>The offer named last, to know which one a failure belongs to.</summary>
    private ItemSourceOption? _offered;

    public override string Description
        => $"carry {request}";

    public override bool IsSatisfied()
        => request.CountIn(inventory.Count) >= request.Count;

    public override IEnumerable<BotTask> Dependencies()
    {
        _offered = NextOption();

        if (_offered is not null)
            yield return _offered.Task;
    }

    public override Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
        // Only reached with nothing offered, or everything offered failing.
        => Task.FromResult(TaskResult.Failed(
            $"no way to get {request} ({string.Join(", ", brain.Policy.Sources).ToLowerInvariant()} all came up empty)"));

    public override bool OnDependencyFailed(BotTask dependency, TaskResult result)
    {
        if (_offered is null || !ReferenceEquals(dependency, _offered.Task))
            return false;

        logger.LogDebug("{Option} did not work out ({Result}); trying the next way to {Goal}", _offered.Key, result, Description);

        failures.Remember(_offered.Key);

        return true;
    }

    private ItemSourceOption? NextOption()
    {
        foreach (var kind in brain.Policy.Sources)
            foreach (var source in sources.Where(source => source.Kind == kind))
                foreach (var option in source.Options(request, chain))
                    if (!failures.HasFailed(option.Key))
                        return option;

        return null;
    }
}
