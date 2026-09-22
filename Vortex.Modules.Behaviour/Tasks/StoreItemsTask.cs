using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Inventory.Abstraction;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Puts everything of an item the bot carries into the open container.
/// </summary>
/// <remarks>
/// The container has to be open already; opening it is what
/// <see cref="OpenContainerTask"/> does.
/// </remarks>
public class StoreItemsTask(Item item, IInventoryManager inventory) : BotTask
{
    public override string Description
        => $"put all {item} into the open container";

    public override bool IsSatisfied()
        => Carried().Count == 0;

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (inventory.OpenContainer is null)
            return TaskResult.Failed("no container is open");

        var before = inventory.Count(item);

        foreach (var slot in Carried())
            if (inventory.ToActiveWindowSlot(slot) is { } windowSlot)
                await inventory.QuickMoveAsync(windowSlot);

        return inventory.Count(item) < before
            ? TaskResult.Success()
            : TaskResult.Failed($"the container has no room for {item}");
    }

    /// <summary>
    /// The slots of the main inventory and hotbar holding the item. The offhand
    /// cannot be reached from a container window.
    /// </summary>
    private List<int> Carried()
        => inventory.Find(stack => stack.Item == item)
            .Select(found => found.Slot)
            .Where(slot => slot != PlayerSlots.Offhand)
            .ToList();
}
