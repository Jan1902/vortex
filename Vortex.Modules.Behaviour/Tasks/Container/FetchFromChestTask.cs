using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Container;

/// <summary>Goes to a container, takes items out of it and closes it again.</summary>
public class FetchFromChestTask(Vector3i chest, IReadOnlySet<Item> items, int count) : BotTask
{
    public override string Description
        => $"fetch {count} {Stacks.Describe(items)} from {chest.X} {chest.Y} {chest.Z}";

    public override bool IsDone(Bot bot)
        => Stacks.CountIn(items, bot.Inventory) >= count;

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        var opened = await bot.Run(new OpenContainerTask(chest));

        if (opened.IsFailure)
        {
            // Gone, or never was one: nothing to come back to.
            bot.Chests.Forget(chest);
            return opened;
        }

        var taken = await bot.Run(new TakeItemsTask(items, count));

        await bot.Inventory.CloseContainerAsync();

        return taken;
    }
}
