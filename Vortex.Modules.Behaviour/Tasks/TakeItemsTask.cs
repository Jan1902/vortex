using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Inventory.Abstraction;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Takes an item out of the open container until the bot carries a number of it.
/// </summary>
/// <remarks>
/// Moves whole stacks, so it can end up with more than asked for. The container
/// has to be open already; opening it is what <see cref="OpenContainerTask"/> does.
/// </remarks>
public class TakeItemsTask(Item item, int count, IInventoryManager inventory) : BotTask
{
    public override string Description
        => $"carry {count} {item} out of the open container";

    public override bool IsSatisfied()
        => inventory.Count(item) >= count;

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (inventory.OpenContainer is not { } container)
            return TaskResult.Failed("no container is open");

        var slot = Enumerable.Range(0, container.ContainerSize)
            .FirstOrDefault(slot => container.Slots[slot]?.Item == item, -1);

        if (slot < 0)
            return TaskResult.Failed($"the container has no more {item}");

        var before = inventory.Count(item);

        await inventory.QuickMoveAsync(slot);

        return inventory.Count(item) > before
            ? TaskResult.Success()
            : TaskResult.Failed($"no room for {item}");
    }
}
