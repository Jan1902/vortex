using Vortex.Bot.Commands.Infrastructure;

namespace Vortex.Bot.Commands.Control;

[Command("stop", "Stops what I am doing")]
public sealed class StopCommand : BotCommand
{
    public override Task ExecuteAsync(CommandContext context)
    {
        context.Client.Brain.Cancel();

        return context.ReplyAsync("Stopping");
    }
}
