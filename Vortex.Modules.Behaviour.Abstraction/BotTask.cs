namespace Vortex.Modules.Behaviour.Abstraction;

/// <summary>
/// A single, idempotent unit of behaviour. It is both a goal and, where it needs
/// to be, the action that reaches it.
/// </summary>
/// <remarks>
/// <para>
/// A task holds no progress of its own. Whether it is done, and what has to
/// happen next, is derived from the world every time it is asked. That is what
/// makes running one twice harmless: the second run sees the world the first one
/// left behind and finds nothing to do.
/// </para>
/// <para>
/// A task never runs its dependencies itself. It names them, unconditionally,
/// and the runner works out which of them are already satisfied. A task
/// therefore does not need to know the preconditions of the tasks it depends on
/// -- it does not ask whether it is in range before naming a movement, it names
/// the movement and lets that task answer for itself.
/// </para>
/// <para>
/// Everything a task needs to look at the world or act on it is injected into
/// its constructor. To build its dependencies it takes Autofac delegate
/// factories, for example <c>Func&lt;Vector3i, BreakBlockTask&gt;</c>, rather
/// than constructing them directly.
/// </para>
/// </remarks>
public abstract class BotTask
{
    /// <summary>
    /// What this task is after, in a form fit for a log line. It should name the
    /// concrete goal, not the type, so that a stack of them reads as an
    /// explanation of what the bot is currently doing.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Whether the goal is already reached. Read from the world, never from
    /// anything this task remembers.
    /// </summary>
    /// <remarks>
    /// This is checked before the task does anything at all and again after
    /// every step, so it has to be cheap.
    /// </remarks>
    public abstract bool IsSatisfied();

    /// <summary>
    /// What has to hold before this task can act, in order and re-derived on
    /// every round.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Name dependencies unconditionally; do not filter out the ones that look
    /// like they are already done. The runner takes the first one that is not
    /// satisfied and runs only that, then comes back and asks again.
    /// </para>
    /// <para>
    /// Because of that, a sequence needs no bookkeeping: naming "be in reach of
    /// the log" and "break the log" in that order produces walking and mining in
    /// turn, and picks the walking back up by itself once the next log is out of
    /// reach. Keep the order deterministic -- an ordering that flips between
    /// rounds makes the bot oscillate.
    /// </para>
    /// <para>
    /// Returned lazily, so a task that is still on its first dependency never
    /// pays for constructing the later ones.
    /// </para>
    /// </remarks>
    public virtual IEnumerable<BotTask> Dependencies()
        => [];

    /// <summary>
    /// What this task does itself once all of its dependencies are satisfied.
    /// </summary>
    /// <remarks>
    /// Leave this alone for a task that is purely a goal and reaches it through
    /// its dependencies. Where it is implemented it should act over a short
    /// horizon -- one path segment, one block -- because nothing is re-evaluated
    /// while it runs.
    /// </remarks>
    public virtual Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
        => Task.FromResult(TaskResult.Success());

    /// <summary>
    /// Whether to carry on after a dependency failed, rather than fail with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a task with more than one way to its goal. Carrying on means the
    /// next round asks for dependencies again, so a task that returns true
    /// here has to make sure that round names something else -- by recording
    /// the failure as something known about the world, not as progress of its
    /// own. Otherwise it runs straight back into the same failure, which the
    /// runner's round limit then ends.
    /// </para>
    /// <para>
    /// Only asked about failures. A cancelled dependency always ends the task.
    /// </para>
    /// </remarks>
    /// <param name="dependency">The dependency that failed, as named by this task.</param>
    /// <param name="result">How it failed.</param>
    public virtual bool OnDependencyFailed(BotTask dependency, TaskResult result)
        => false;
}
