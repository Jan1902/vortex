using Vortex.Bot.Commands.Infrastructure;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Container;

namespace Vortex.Bot.Commands.Items;

[Command("store", "Puts all of an item into the open container")]
public sealed class StoreCommand : TaskCommand
{
    [Argument(0)]
    public Item Item { get; set; }

    protected override Task<BotTask?> CreateTaskAsync(CommandContext context)
        => Task.FromResult<BotTask?>(context.Client.Brain.CreateTask<StoreItemsTask>(Item));
}
