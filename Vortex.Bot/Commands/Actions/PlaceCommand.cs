using Vortex.Bot.Commands.Infrastructure;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks;
using Vortex.Shared;

namespace Vortex.Bot.Commands.Actions;

[Command("place", "Places a block")]
public sealed class PlaceCommand : TaskCommand
{
    [Argument(0)]
    public Item Item { get; set; }

    [Argument(1)]
    public Vector3i Block { get; set; } = null!;

    protected override Task<BotTask?> CreateTaskAsync(CommandContext context)
        => Task.FromResult<BotTask?>(context.Client.Brain.CreateTask<PlaceBlockTask>(Item, Block));
}
