using Vortex.Modules.Behaviour.Abstraction;

namespace Vortex.Bot.Commands.Infrastructure;

/// <summary>
/// A command that gives the bot a task: it says what it is on, runs the task
/// and says how it went.
/// </summary>
/// <remarks>
/// The bot runs one task at a time, so a new one replaces whatever it was
/// doing, which then reports itself cancelled.
/// </remarks>
public abstract class TaskCommand : BotCommand
{
    public override async Task ExecuteAsync(CommandContext context)
    {
        if (await CreateTaskAsync(context) is not { } task)
            return;

        await context.ReplyAsync($"On it: {task.Description}");

        var result = await context.Client.Brain.RunAsync(task, MayDig);

        await context.ReplyAsync(result.ToString());
    }

    /// <summary>
    /// Whether the bot may break blocks in the way of where it is going. Only
    /// for commands that are about gathering; nobody wants a tunnel through
    /// their wall because they said "come".
    /// </summary>
    protected virtual bool MayDig => false;

    /// <summary>
    /// Builds the task to run.
    /// </summary>
    /// <returns>The task, or <c>null</c> after telling the caller why there is none.</returns>
    protected abstract Task<BotTask?> CreateTaskAsync(CommandContext context);
}
