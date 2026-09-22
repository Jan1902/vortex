using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Inventory.Abstraction;

namespace Vortex.Modules.Behaviour.Tasks.Container;

/// <summary>Puts every stack of an item the bot carries into the container that is open.</summary>
public class StoreItemsTask(Item item) : BotTask
{
    public override string Description
        => $"put all {item} into the open container";

    public override bool IsDone(Bot bot)
        => Carried(bot).Count == 0;

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        if (bot.Inventory.OpenContainer is null)
            return TaskResult.Failed("no container is open");

        var before = bot.Inventory.Count(item);

        foreach (var slot in Carried(bot))
            if (bot.Inventory.ToActiveWindowSlot(slot) is { } windowSlot)
                await bot.Inventory.QuickMoveAsync(windowSlot);

        await Task.Delay(100, bot.Cancellation);

        OpenContainerTask.Remember(bot);

        return bot.Inventory.Count(item) < before
            ? TaskResult.Success()
            : TaskResult.Failed($"the container has no room for {item}");
    }

    /// <summary>The slots holding the item, leaving the offhand alone.</summary>
    private List<int> Carried(Bot bot)
        => bot.Inventory.Find(stack => stack.Item == item)
            .Select(found => found.Slot)
            .Where(slot => slot != PlayerSlots.Offhand)
            .ToList();
}
