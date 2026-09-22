using Vortex.Bot.Commands.Infrastructure;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Container;

namespace Vortex.Bot.Commands.Items;

[Command("take", "Takes items out of the open container")]
public sealed class TakeCommand : TaskCommand
{
    [Argument(0)]
    public Item Item { get; set; }

    [Argument(1, Optional = true)]
    public int Count { get; set; } = 64;

    protected override Task<BotTask?> CreateTaskAsync(CommandContext context)
        => Task.FromResult<BotTask?>(context.Client.Brain.CreateTask<TakeItemsTask>(Item, context.Client.Inventory.Count(Item) + Count));
}
