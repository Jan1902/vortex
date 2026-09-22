using Vortex.Framework.Abstraction;

namespace Vortex.Bot.Commands.Infrastructure;

/// <summary>
/// A chat command. Its arguments are properties marked with
/// <see cref="ArgumentAttribute"/>, filled in before it runs; a new instance is
/// made for every call.
/// </summary>
public abstract class BotCommand
{
    public abstract Task ExecuteAsync(CommandContext context);
}

/// <summary>
/// Who called a command and how to answer.
/// </summary>
/// <param name="Client">The bot.</param>
/// <param name="SenderUuid">The player who called it, or <c>null</c> for a message from the server.</param>
/// <param name="SenderName">The player's name, if known.</param>
/// <param name="BotName">The name commands start with, for the ones that show how to call others.</param>
/// <param name="Commands">Every command there is, for the ones that list them.</param>
/// <param name="Reply">How to answer.</param>
public sealed record CommandContext(
    IVortexClient Client,
    Guid? SenderUuid,
    string? SenderName,
    string BotName,
    IReadOnlyList<CommandInfo> Commands,
    Func<string, Task> Reply)
{
    /// <summary>Answers in chat.</summary>
    public Task ReplyAsync(string message) => Reply(message);
}
