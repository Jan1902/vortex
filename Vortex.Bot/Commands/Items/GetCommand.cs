using Vortex.Bot.Commands.Infrastructure;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Items;

namespace Vortex.Bot.Commands.Items;

[Command("get", "Gets items from wherever they can be had")]
public sealed class GetCommand : TaskCommand
{
    [Argument(0)]
    public Item Item { get; set; }

    [Argument(1, Optional = true)]
    public int Count { get; set; } = 1;

    protected override bool MayDig => true;

    protected override Task<BotTask?> CreateTaskAsync(CommandContext context)
        => Task.FromResult<BotTask?>(new GetItemsTask(Item, context.Client.Inventory.Count(Item) + Count));
}
