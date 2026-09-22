namespace Vortex.Modules.Behaviour.Abstraction;

/// <summary>
/// Something the bot does, written as one method that reads top to bottom.
/// </summary>
/// <remarks>
/// <para>
/// A task that needs something else done first simply runs it:
/// <c>var reached = await bot.Run(new WithinReachTask(pos));</c> and carries on
/// or gives up depending on the result. There is no separate list of
/// prerequisites and no planner; the order things happen in is the order they
/// are written in.
/// </para>
/// <para>
/// Failures come back as a <see cref="TaskResult"/> rather than an exception,
/// so that a task can look at what went wrong below it and try something else
/// if it has something else to try. Most just pass the failure on.
/// </para>
/// </remarks>
public abstract class BotTask
{
    /// <summary>What the task is after, for the log and for "jeff doing".</summary>
    public abstract string Description { get; }

    /// <summary>
    /// Whether there is nothing to do, checked by <see cref="Bot.Run"/> before
    /// the task runs at all. Cheap, and read from the world.
    /// </summary>
    public virtual bool IsDone(Bot bot)
        => false;

    /// <summary>Does the work.</summary>
    public abstract Task<TaskResult> RunAsync(Bot bot);
}
