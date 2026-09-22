using Vortex.Bot.Commands.Infrastructure;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks;
using Vortex.Shared;

namespace Vortex.Bot.Commands.Actions;

[Command("use", "Uses a block, such as a lever or a door")]
public sealed class UseCommand : TaskCommand
{
    [Argument(0)]
    public Vector3i Block { get; set; } = null!;

    protected override Task<BotTask?> CreateTaskAsync(CommandContext context)
        => Task.FromResult<BotTask?>(context.Client.Brain.CreateTask<UseBlockTask>(Block));
}
