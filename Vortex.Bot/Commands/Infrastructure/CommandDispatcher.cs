using System.Reflection;
using Vortex.Framework.Abstraction;

namespace Vortex.Bot.Commands.Infrastructure;

/// <summary>
/// Reads chat for messages addressed to the bot, such as <c>jeff mine 1 64 2</c>,
/// and runs the command they name.
/// </summary>
/// <remarks>
/// Chat messages are handed out one after another, and a command may run for
/// minutes. <see cref="HandleAsync"/> therefore starts a command and returns
/// straight away, so that <c>jeff stop</c> or <c>jeff doing</c> still get
/// through while the bot is busy.
/// </remarks>
public sealed class CommandDispatcher
{
    private readonly IVortexClient _client;
    private readonly Func<string, Task> _reply;
    private readonly Dictionary<string, CommandInfo> _commands;

    /// <param name="client">The bot the commands act on.</param>
    /// <param name="name">The bot's name, which a message has to start with to be a command.</param>
    /// <param name="commands">The commands there are.</param>
    /// <param name="reply">How to answer; chat by default.</param>
    public CommandDispatcher(IVortexClient client, string name, IEnumerable<CommandInfo> commands, Func<string, Task>? reply = null)
    {
        _client = client;
        _reply = reply ?? client.SendChatMessage;
        Name = name.ToLowerInvariant();
        _commands = commands.ToDictionary(command => command.Name, StringComparer.OrdinalIgnoreCase);
        Commands = [.. _commands.Values.OrderBy(command => command.Name, StringComparer.Ordinal)];
    }

    /// <summary>The name a message has to start with, in lower case.</summary>
    public string Name { get; }

    /// <summary>The commands there are, by name.</summary>
    public IReadOnlyList<CommandInfo> Commands { get; }

    /// <summary>
    /// Finds every command class in an assembly.
    /// </summary>
    public static IEnumerable<CommandInfo> Discover(Assembly assembly)
        => assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<CommandAttribute>() is not null)
            .Select(CommandInfo.For);

    /// <summary>
    /// Starts the command a chat message names, if it names one, without
    /// waiting for it to finish.
    /// </summary>
    public Task HandleAsync(ChatMessageReceivedEventArgs chat)
    {
        _ = RunAsync(chat);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs the command a chat message names, if it names one, and answers
    /// when it cannot be run.
    /// </summary>
    public async Task RunAsync(ChatMessageReceivedEventArgs chat)
    {
        var words = chat.Message.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // The bot's own answers come back through chat as well. Help lines start
        // with its name, so it would otherwise take them for commands.
        if (words.Length == 0 || words[0] != Name || string.Equals(chat.SenderName, Name, StringComparison.OrdinalIgnoreCase))
            return;

        if (words.Length == 1)
        {
            await _reply($"Yes? Try {Name} help");
            return;
        }

        if (!_commands.TryGetValue(words[1], out var info))
        {
            await _reply($"I don't know how to {words[1]}. Try {Name} help");
            return;
        }

        if (info.Bind(words[2..]) is not { } command)
        {
            await _reply($"Usage: {Name} {info.Usage}");
            return;
        }

        try
        {
            await command.ExecuteAsync(new CommandContext(_client, chat.SenderUuid, chat.SenderName, Name, Commands, _reply));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"{Name} {info.Name} failed: {exception}");

            await _reply($"That went wrong: {exception.Message}");
        }
    }
}
