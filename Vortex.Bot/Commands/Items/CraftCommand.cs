using Vortex.Bot.Commands.Infrastructure;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks;

namespace Vortex.Bot.Commands.Items;

[Command("craft", "Crafts an item, and whatever goes into it")]
public sealed class CraftCommand : TaskCommand
{
    [Argument(0)]
    public Item Item { get; set; }

    [Argument(1, Optional = true)]
    public int Count { get; set; } = 1;

    protected override Task<BotTask?> CreateTaskAsync(CommandContext context)
        => Task.FromResult<BotTask?>(context.Client.Brain.CreateTask<CraftTask>(Item, context.Client.Inventory.Count(Item) + Count));
}
