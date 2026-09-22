namespace Vortex.Modules.Behaviour.Abstraction;

/// <summary>
/// Runs behaviour. It owns the task stack, so that tasks themselves do not have
/// to.
/// </summary>
public interface IBotBrain
{
    /// <summary>
    /// Gets a value indicating whether a task is currently running.
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// Gets what the bot is doing, outermost goal first, as descriptions of the
    /// tasks currently on the stack. Empty while idle.
    /// </summary>
    /// <remarks>
    /// Because no task carries progress of its own, this is the whole of the
    /// bot's current intent -- there is no hidden position inside a task that
    /// this would fail to show.
    /// </remarks>
    IReadOnlyList<string> CurrentStack { get; }

    /// <summary>
    /// Runs a task to completion, along with everything it turns out to depend
    /// on. Only one may run at a time; starting another cancels the running one.
    /// </summary>
    /// <param name="task">The task to reach the goal of.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>How the task ended.</returns>
    Task<TaskResult> RunAsync(BotTask task, CancellationToken cancellationToken = default)
        => RunAsync(task, BehaviourPolicy.Default, cancellationToken);

    /// <summary>
    /// Runs a task to completion under a policy that holds for everything it
    /// turns out to depend on.
    /// </summary>
    /// <param name="task">The task to reach the goal of.</param>
    /// <param name="policy">What the task tree may do: where items may come from, whether it may dig.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>How the task ended.</returns>
    Task<TaskResult> RunAsync(BotTask task, BehaviourPolicy policy, CancellationToken cancellationToken = default);

    /// <summary>The policy the running task tree works under; the default while idle.</summary>
    BehaviourPolicy Policy { get; }

    /// <summary>
    /// Cancels whatever is running.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Builds a task, filling in the managers and dependency factories it asks
    /// for from the container.
    /// </summary>
    /// <remarks>
    /// This is for creating a root task from outside the behaviour system, where
    /// there is nothing to inject a factory into. Tasks themselves should take a
    /// delegate factory such as <c>Func&lt;Vector3i, BreakBlockTask&gt;</c> for
    /// their dependencies instead, which keeps the argument types checked at
    /// compile time.
    /// </remarks>
    /// <typeparam name="TTask">The type of task to build.</typeparam>
    /// <param name="arguments">
    /// The task's own parameters. Matched to constructor parameters by type, so
    /// two parameters of the same type cannot be told apart this way.
    /// </param>
    TTask CreateTask<TTask>(params object[] arguments) where TTask : BotTask;
}
