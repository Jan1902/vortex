using Vortex.Bot.Commands.Infrastructure;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Navigation;

namespace Vortex.Bot.Commands.Movement;

/// <summary>
/// Heads for where the caller stands right now, once; it does not follow.
/// </summary>
[Command("come", "Walks over to you")]
public sealed class ComeCommand : TaskCommand
{
    protected override bool MayDig => true;

    protected override async Task<BotTask?> CreateTaskAsync(CommandContext context)
    {
        if (context.SenderUuid is not { } sender)
        {
            await context.ReplyAsync("Only players can call me over");
            return null;
        }

        if (context.Client.Entities.Get(sender) is not { } player)
        {
            await context.ReplyAsync("I can't see you from here");
            return null;
        }

        return new GoToTask(player.Position.ToBlockPosition());
    }
}
