namespace Vortex.Modules.Behaviour.Abstraction;

/// <summary>
/// Why a task stopped.
/// </summary>
public enum TaskOutcome
{
    /// <summary>The goal is reached.</summary>
    Succeeded,

    /// <summary>The goal was not reached and this task cannot get any closer.</summary>
    Failed,

    /// <summary>The run was cancelled from outside.</summary>
    Cancelled
}

/// <summary>
/// How a task ended, with the reason it failed if it did.
/// </summary>
/// <param name="Outcome">Why the task stopped.</param>
/// <param name="Reason">
/// What went wrong, for a failure. Null for anything else.
/// </param>
public record TaskResult(TaskOutcome Outcome, string? Reason = null)
{
    /// <summary>Gets a value indicating whether the goal was reached.</summary>
    public bool IsSuccess
        => Outcome is TaskOutcome.Succeeded;

    /// <summary>
    /// Gets a value indicating whether the task ended without reaching its goal,
    /// whether it failed or was cancelled.
    /// </summary>
    public bool IsFailure
        => Outcome is not TaskOutcome.Succeeded;

    public static TaskResult Success()
        => new(TaskOutcome.Succeeded);

    public static TaskResult Failed(string reason)
        => new(TaskOutcome.Failed, reason);

    public static TaskResult Cancelled()
        => new(TaskOutcome.Cancelled);

    public override string ToString()
        => Reason is null ? Outcome.ToString() : $"{Outcome}: {Reason}";
}
