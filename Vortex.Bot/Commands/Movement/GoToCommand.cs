using Vortex.Bot.Commands.Infrastructure;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Bot.Commands.Movement;

[Command("goto", "Walks to a block, jumping gaps if need be")]
public sealed class GoToCommand : TaskCommand
{
    [Argument(0)]
    public Vector3i Block { get; set; } = null!;

    protected override Task<BotTask?> CreateTaskAsync(CommandContext context)
        => Task.FromResult<BotTask?>(context.Client.Brain.CreateTask<GoToTask>(Block, MovementCapabilities.Athletic));
}
