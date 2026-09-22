using Vortex.Bot.Commands.Infrastructure;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Blocks;
using Vortex.Shared;

namespace Vortex.Bot.Commands.Actions;

[Command("mine", "Breaks a block and picks up what it drops")]
public sealed class MineCommand : TaskCommand
{
    [Argument(0)]
    public Vector3i Block { get; set; } = null!;

    protected override Task<BotTask?> CreateTaskAsync(CommandContext context)
        => Task.FromResult<BotTask?>(context.Client.Brain.CreateTask<HarvestBlockTask>(Block));
}
