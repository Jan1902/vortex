using Vortex.Bot.Commands.Infrastructure;

namespace Vortex.Bot.Commands.Control;

/// <summary>
/// Lists the commands, or explains one.
/// </summary>
/// <remarks>
/// The list is a single message on purpose: a line per command would be enough
/// messages at once for the server to kick the bot for spamming.
/// </remarks>
[Command("help", "Lists what I can do, or explains a command")]
public sealed class HelpCommand : BotCommand
{
    [Argument(0, Optional = true)]
    public string? Command { get; set; }

    public override Task ExecuteAsync(CommandContext context)
    {
        if (Command is null)
            return context.ReplyAsync(
                $"I can: {string.Join(", ", context.Commands.Select(command => command.Name))}. Try {context.BotName} help <command>");

        return context.Commands.FirstOrDefault(command => command.Name == Command) is { } found
            ? context.ReplyAsync($"{context.BotName} {found.Usage}: {found.Description}")
            : context.ReplyAsync($"There is no {Command} command");
    }
}
