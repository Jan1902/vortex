using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Helper;

namespace Vortex.Modules.Behaviour.Tasks.Container;

/// <summary>
/// Takes items out of the container that is open, a stack at a time, until the
/// bot carries enough of them or the container has no more.
/// </summary>
public class TakeItemsTask(IReadOnlySet<Item> items, int count) : BotTask
{
    public TakeItemsTask(Item item, int count)
        : this(new HashSet<Item> { item }, count)
    {
    }

    public override string Description
        => $"take {count} {Stacks.Describe(items)} out of the open container";

    public override bool IsDone(Bot bot)
        => Stacks.CountIn(items, bot.Inventory) >= count;

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        if (bot.Inventory.OpenContainer is null)
            return TaskResult.Failed("no container is open");

        var took = 0;

        while (!IsDone(bot))
        {
            if (bot.Inventory.OpenContainer is not { } window)
                return TaskResult.Failed("the container closed");

            var slot = Enumerable.Range(0, window.ContainerSize)
                .FirstOrDefault(slot => window.Slots[slot] is { } stack && items.Contains(stack.Item), -1);

            if (slot < 0)
                break;

            var before = Stacks.CountIn(items, bot.Inventory);

            await bot.Inventory.QuickMoveAsync(slot);
            await Task.Delay(100, bot.Cancellation);

            if (Stacks.CountIn(items, bot.Inventory) <= before)
                return TaskResult.Failed($"no room for {Stacks.Describe(items)}");

            took++;
        }

        OpenContainerTask.Remember(bot);

        return took > 0 || IsDone(bot)
            ? TaskResult.Success()
            : TaskResult.Failed($"the container has no {Stacks.Describe(items)}");
    }
}
