namespace Vortex.Modules.Behaviour.Abstraction;

/// <summary>
/// Runs one task at a time; starting another cancels the running one.
/// </summary>
public interface IBotBrain
{
    /// <summary>Whether a task is running.</summary>
    bool IsBusy { get; }

    /// <summary>What the bot is doing, outermost task first. Empty while idle.</summary>
    IReadOnlyList<string> CurrentStack { get; }

    /// <summary>What tasks work with; for building a task that needs to look at the world first.</summary>
    Bot Bot { get; }

    /// <summary>
    /// Runs a task to the end, replacing whatever was running.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <param name="mayDig">Whether routes may go through blocks, breaking them on the way.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task<TaskResult> RunAsync(BotTask task, bool mayDig = false, CancellationToken cancellationToken = default);

    /// <summary>Cancels whatever is running.</summary>
    void Cancel();
}
