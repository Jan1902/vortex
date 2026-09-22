using Vortex.Bot.Commands.Infrastructure;

namespace Vortex.Bot.Commands.Control;

[Command("doing", "Tells what I am doing, and why")]
public sealed class DoingCommand : BotCommand
{
    public override Task ExecuteAsync(CommandContext context)
    {
        var stack = context.Client.Brain.CurrentStack;

        return context.ReplyAsync(stack.Count == 0 ? "Nothing" : string.Join(" -> ", stack));
    }
}
