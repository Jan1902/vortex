using Microsoft.Extensions.Logging;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player;

/// <summary>
/// Runs one movement plan at a time against the real player, and reports how it
/// ended.
/// </summary>
/// <remarks>
/// What a movement <em>is</em> lives in <see cref="PlanRunner"/>, and what each
/// kind of movement looks like lives in <see cref="MovementPlans"/>. What is
/// left here is everything that only matters because this is the live player and
/// not a simulation: one movement at a time, waking up whoever is awaiting it,
/// and keeping track of where the player was last seen so that a plan can be
/// worked out from it.
/// </remarks>
internal class MovementController(MovementPlans plans, ILogger<MovementController> logger) : IMovementController
{
    /// <summary>A movement shorter than this has already happened.</summary>
    private const double NothingToDo = 1e-6;

    private readonly object _lock = new();

    private Execution? _current;
    private bool _jumpRequested;

    /// <summary>
    /// The last state the physics loop reported. A plan is worked out from where
    /// the player is, and a caller asking for a movement between two ticks has
    /// no way to hand that over itself.
    /// </summary>
    private MovementState _lastState = MovementState.Unknown;

    public bool IsMoving
    {
        get { lock (_lock) return _current is not null; }
    }

    public Task<MovementResult> WalkTo(Vector3d target, MovementMode mode = MovementMode.Walk, bool autoJump = false)
        => Plan(origin => MovementPlans.Walk(origin, target, mode, autoJump));

    public Task<MovementResult> StepUpTo(Vector3d target, MovementMode mode = MovementMode.Walk)
        => Plan(origin => MovementPlans.StepUp(origin, target, mode));

    public Task<MovementResult> DropTo(Vector3d target, MovementMode mode = MovementMode.Walk)
        => Plan(origin => plans.Drop(origin, target, mode));

    public Task<MovementResult> JumpTo(Vector3d takeOff, Vector3d landing, MovementMode mode = MovementMode.Walk)
        => Plan(origin => plans.Leap(origin, takeOff, landing, mode));

    public Task<MovementResult> Execute(MovementPlan plan)
    {
        lock (_lock)
            return Start(plan);
    }

    /// <summary>
    /// Builds a plan from where the player was last seen and runs it.
    /// </summary>
    /// <remarks>
    /// A movement aimed at somewhere the player already is has nothing to do,
    /// and a plan built for it would have a direction of nowhere and a condition
    /// that is already true.
    /// </remarks>
    private Task<MovementResult> Plan(Func<Vector3d, MovementPlan> build)
    {
        lock (_lock)
        {
            var origin = _lastState.Position;
            var plan = build(origin);

            return origin.HorizontalDistanceTo(plan.Destination) < NothingToDo
                ? Task.FromResult(MovementResult.Arrived)
                : Start(plan);
        }
    }

    public void Jump()
    {
        lock (_lock)
            _jumpRequested = true;
    }

    /// <summary>
    /// Takes over from whatever was running.
    /// </summary>
    /// <remarks>Called under the lock.</remarks>
    private Task<MovementResult> Start(MovementPlan plan)
    {
        var execution = new Execution(plan);

        var replaced = _current;
        _current = execution;

        replaced?.Complete(MovementResult.Cancelled);

        return execution.Completion.Task;
    }

    /// <summary>
    /// Reports that the server moved the player itself.
    /// </summary>
    /// <remarks>
    /// The plan in progress was worked out from where the player believed it
    /// was. After a correction every point in it means nothing, so the movement
    /// ends -- but as something other than a cancellation, because the caller is
    /// expected to try again rather than give up.
    /// </remarks>
    internal void Desynchronize()
        => Abandon(MovementResult.Desynced);

    public void Stop()
        => Abandon(MovementResult.Cancelled);

    private void Abandon(MovementResult result)
    {
        Execution? abandoned;

        lock (_lock)
        {
            abandoned = _current;
            _current = null;
        }

        abandoned?.Complete(result);
    }

    /// <summary>
    /// Advances the movement by one tick and says what the player should do.
    /// </summary>
    /// <remarks>
    /// One call per tick, taking the state the coming step starts from. The
    /// state after a step is the state before the next one, so there is nothing
    /// to feed back separately: reading and deciding happen on the same numbers,
    /// which is what keeps a phase from being judged on a tick it never ran.
    /// </remarks>
    /// <param name="tick">Where the player is and what it is doing.</param>
    internal MovementInput Tick(MovementState tick)
    {
        Execution? finished = null;
        MovementResult? result;
        MovementInput input;

        lock (_lock)
        {
            _lastState = tick;

            var requestedJump = _jumpRequested;
            _jumpRequested = false;

            if (_current is null)
                return MovementInput.Idle with { Jump = requestedJump };

            (input, result) = _current.Runner.Step(tick);

            input = input with { Jump = input.Jump || requestedJump };

            if (result is not null)
            {
                finished = _current;
                _current = null;
            }
        }

        if (finished is null)
            return input;

        var destination = finished.Runner.Plan.Destination;

        logger.LogDebug("Movement to {X:F1} {Y:F1} {Z:F1} ended as {Result}{Reason}",
            destination.X, destination.Y, destination.Z, result,
            finished.Runner.Reason is { Length: > 0 } reason ? $": {reason}" : string.Empty);

        finished.Complete(result!.Value);

        return input;
    }

    /// <summary>One plan in progress, and whoever is waiting for it.</summary>
    private sealed class Execution(MovementPlan plan)
    {
        public PlanRunner Runner { get; } = new(plan);

        public TaskCompletionSource<MovementResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Complete(MovementResult result)
            => Completion.TrySetResult(result);
    }
}
