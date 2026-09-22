using Vortex.Modules.Behaviour.Abstraction;

namespace Vortex.Bot.Commands.Infrastructure;

/// <summary>
/// A command that gives the bot's brain a task: it says what it is on, runs the
/// task and says how it went.
/// </summary>
/// <remarks>
/// The brain runs one task at a time, so a new one replaces whatever the bot was
/// doing, which then reports itself cancelled.
/// </remarks>
public abstract class TaskCommand : BotCommand
{
    public override async Task ExecuteAsync(CommandContext context)
    {
        if (await CreateTaskAsync(context) is not { } task)
            return;

        await context.ReplyAsync($"On it: {task.Description}");

        var result = await context.Client.Brain.RunAsync(task);

        await context.ReplyAsync(result.ToString());
    }

    /// <summary>
    /// Builds the task to run, through <c>context.Client.Brain.CreateTask</c>.
    /// </summary>
    /// <returns>The task, or <c>null</c> after telling the caller why there is none.</returns>
    protected abstract Task<BotTask?> CreateTaskAsync(CommandContext context);
}
