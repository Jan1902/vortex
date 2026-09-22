using Vortex.Bot.Commands.Infrastructure;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Entities;

namespace Vortex.Bot.Commands.Actions;

[Command("attack", "Fights the closest entity of a type")]
public sealed class AttackCommand : TaskCommand
{
    [Argument(0)]
    public EntityType EntityType { get; set; }

    protected override async Task<BotTask?> CreateTaskAsync(CommandContext context)
    {
        var client = context.Client;

        if (client.Entities.Nearest(client.Position, entity => entity.Type == EntityType) is not { } target)
        {
            await context.ReplyAsync($"I see no {EntityType}");
            return null;
        }

        return new AttackTask(target.Id);
    }
}
