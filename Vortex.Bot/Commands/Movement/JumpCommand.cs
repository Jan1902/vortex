using Vortex.Bot.Commands.Infrastructure;

namespace Vortex.Bot.Commands.Movement;

[Command("jump", "Jumps once")]
public sealed class JumpCommand : BotCommand
{
    public override Task ExecuteAsync(CommandContext context)
    {
        context.Client.Movement.Jump();

        return context.ReplyAsync("Hop!");
    }
}
