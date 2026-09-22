using Vortex.Bot.Commands.Infrastructure;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Shared;

namespace Vortex.Bot.Commands.Movement;

[Command("reach", "Walks until a block is within reach")]
public sealed class ReachCommand : TaskCommand
{
    [Argument(0)]
    public Vector3i Block { get; set; } = null!;

    protected override Task<BotTask?> CreateTaskAsync(CommandContext context)
        => Task.FromResult<BotTask?>(new WithinReachTask(Block));
}
