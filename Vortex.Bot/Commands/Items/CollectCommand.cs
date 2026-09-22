using Vortex.Bot.Commands.Infrastructure;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Items;

namespace Vortex.Bot.Commands.Items;

[Command("collect", "Picks up the items lying around me")]
public sealed class CollectCommand : TaskCommand
{
    [Argument(0, Optional = true)]
    public double Radius { get; set; } = 16;

    protected override Task<BotTask?> CreateTaskAsync(CommandContext context)
        => Task.FromResult<BotTask?>(context.Client.Brain.CreateTask<CollectItemsTask>(context.Client.Position, Radius));
}
