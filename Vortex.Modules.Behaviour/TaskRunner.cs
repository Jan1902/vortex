using Microsoft.Extensions.Logging;
using Vortex.Modules.Behaviour.Abstraction;

namespace Vortex.Modules.Behaviour;

/// <summary>
/// Drives tasks. It owns the stack, decides which dependency is next and holds
/// the guard rails, so that a task can be nothing but a description of a goal.
/// </summary>
/// <remarks>
/// <para>
/// The loop is the whole of the behaviour system: ask whether the goal is
/// reached, otherwise take the first dependency that is not satisfied and run
/// that, and once nothing is pending let the task act. Then start over. Every
/// round reads the world again, which is what lets a task pick its work back up
/// after the world moved underneath it without carrying any progress itself.
/// </para>
/// <para>
/// At debug level the whole of that reasoning is written out, indented by how
/// deep the stack is, so a run reads as an account of what the bot wanted and
/// what it found. Nothing is worked out for the log that the loop does not work
/// out anyway, and the extra enumeration that names the dependencies it skipped
/// only happens when debug logging is actually switched on.
/// </para>
/// </remarks>
internal class TaskRunner(ILogger<TaskRunner> logger)
{
    /// <summary>
    /// How deep the stack may get. This is only here to turn a task that ends up
    /// depending on itself into an error message instead of a stack overflow.
    /// </summary>
    private const int MaxDepth = 64;

    /// <summary>
    /// How many rounds a single task gets before the runner gives up on it.
    /// Generous on purpose: a task that walks a block at a time legitimately
    /// spends many rounds on the same step, and a leaf that truly cannot get
    /// anywhere is expected to say so itself.
    /// </summary>
    private const int MaxRounds = 2048;

    /// <summary>
    /// How often the same dependency may be picked twice in a row before this
    /// counts as oscillation. Once a dependency reports success it is satisfied,
    /// so seeing it come up again immediately means something is flipping it
    /// back -- usually a goal and its dependency disagreeing about what "done"
    /// means, or an ordering that is not stable between rounds.
    /// </summary>
    private const int MaxRepeats = 3;

    private readonly List<BotTask> _stack = [];
    private readonly object _stackLock = new();

    /// <summary>
    /// Gets the tasks currently being worked on, outermost first.
    /// </summary>
    public IReadOnlyList<string> Stack
    {
        get
        {
            lock (_stackLock)
                return _stack.Select(t => t.Description).ToArray();
        }
    }

    public Task<TaskResult> RunAsync(BotTask task, CancellationToken cancellationToken)
        => RunAsync(task, depth: 0, cancellationToken);

    private async Task<TaskResult> RunAsync(BotTask task, int depth, CancellationToken cancellationToken)
    {
        if (depth >= MaxDepth)
            return TaskResult.Failed($"'{task.Description}' is nested {MaxDepth} deep; it most likely depends on itself");

        Push(task);

        logger.LogDebug("{Indent}want: {Task}", Indent(depth), task.Description);

        try
        {
            var (result, rounds) = await RunRoundsAsync(task, depth, cancellationToken);

            logger.LogDebug("{Indent}{Marker} {Task} -- {Result} after {Rounds} round(s)",
                Indent(depth),
                result.IsSuccess ? "done:" : "gave up:",
                task.Description,
                result,
                rounds);

            return result;
        }
        finally
        {
            Pop();
        }
    }

    private async Task<(TaskResult Result, int Rounds)> RunRoundsAsync(BotTask task, int depth, CancellationToken cancellationToken)
    {
        var indent = Indent(depth + 1);

        string? lastDependency = null;
        var repeats = 0;

        for (var round = 0; round < MaxRounds; round++)
        {
            if (cancellationToken.IsCancellationRequested)
                return (TaskResult.Cancelled(), round);

            if (task.IsSatisfied())
            {
                if (round == 0)
                    logger.LogDebug("{Indent}already so, nothing to do", indent);

                return (TaskResult.Success(), round);
            }

            // Name, do not filter: the task lists what should hold without
            // checking whether it already does. Deciding that is this line's
            // job, which is why a task never needs to know the preconditions
            // of the tasks it depends on.
            var pending = SelectPendingDependency(task, indent);

            if (pending is not null)
            {
                if (pending.Description == lastDependency && ++repeats >= MaxRepeats)
                    return (TaskResult.Failed(
                        $"'{task.Description}' keeps asking for '{pending.Description}' right after it reported success"), round);

                if (pending.Description != lastDependency)
                {
                    lastDependency = pending.Description;
                    repeats = 0;
                }

                var dependencyResult = await RunAsync(pending, depth + 1, cancellationToken);

                if (dependencyResult.Outcome == TaskOutcome.Failed && task.OnDependencyFailed(pending, dependencyResult))
                {
                    // The task has another way and wants it tried. Whatever it
                    // names next is a fresh start, not a repeat of this one.
                    logger.LogDebug("{Indent}trying another way", indent);

                    lastDependency = null;
                    continue;
                }

                if (dependencyResult.IsFailure)
                    return (dependencyResult, round);

                // Round again rather than falling through to the action: the
                // dependency just changed the world, so what comes next has
                // to be derived from it anew.
                continue;
            }

            logger.LogDebug("{Indent}acting (round {Round})", indent, round + 1);

            var result = await task.ExecuteAsync(cancellationToken);

            logger.LogDebug("{Indent}acted: {Result}", indent, result);

            if (result.IsFailure)
                return (result, round);

            // A task whose dependencies are all satisfied and whose action
            // reported success, but that still does not consider itself
            // done, is expected to have moved the world closer. If it did
            // not, the round limit ends it below.
        }

        return (TaskResult.Failed($"'{task.Description}' made no progress in {MaxRounds} rounds"), MaxRounds);
    }

    /// <summary>
    /// The first dependency that does not hold yet, or null when they all do.
    /// </summary>
    /// <remarks>
    /// With debug logging on this also names the ones it walked past, because
    /// which preconditions the task considered is as much of what it is thinking
    /// as which one it settled on. That costs an extra log line per satisfied
    /// dependency and nothing else -- each one is asked exactly once either way.
    /// </remarks>
    private BotTask? SelectPendingDependency(BotTask task, string indent)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
            return task.Dependencies().FirstOrDefault(d => !d.IsSatisfied());

        foreach (var dependency in task.Dependencies())
        {
            if (dependency.IsSatisfied())
            {
                logger.LogDebug("{Indent}have: {Dependency}", indent, dependency.Description);

                continue;
            }

            logger.LogDebug("{Indent}need: {Dependency}", indent, dependency.Description);

            return dependency;
        }

        return null;
    }

    /// <summary>
    /// Two spaces per level of nesting, so that the log reads as the shape of
    /// the task tree.
    /// </summary>
    private static string Indent(int depth)
        => new(' ', depth * 2);

    private void Push(BotTask task)
    {
        lock (_stackLock)
            _stack.Add(task);
    }

    private void Pop()
    {
        lock (_stackLock)
            _stack.RemoveAt(_stack.Count - 1);
    }
}
