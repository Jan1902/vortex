using Microsoft.Extensions.Logging;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player;

/// <summary>
/// Turns movement requests into per-tick input and reports how they ended.
/// </summary>
internal class MovementController(ILogger<MovementController> logger) : IMovementController
{
    /// <summary>How close to the target counts as having arrived.</summary>
    private const double ArrivalTolerance = 0.2;

    /// <summary>
    /// Progress smaller than this over <see cref="StuckTicks"/> ticks counts as
    /// being stuck. This catches the cases where the player grinds along a corner
    /// without ever cleanly colliding.
    /// </summary>
    private const double MinimumProgress = 0.05;

    private const int StuckTicks = 10;

    private readonly object _lock = new();

    private Movement? _current;
    private bool _jumpRequested;

    public bool IsMoving
    {
        get { lock (_lock) return _current is not null; }
    }

    public Task<MovementResult> Move(Vector3d direction, double distance, MovementMode mode = MovementMode.Walk, bool autoJump = false)
    {
        var length = Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);

        if (length < 1e-6 || distance <= 0)
            return Task.FromResult(MovementResult.Arrived);

        var normalized = new Vector3d(direction.X / length, 0, direction.Z / length);
        var movement = new Movement(normalized, distance, mode, autoJump);

        Movement? replaced;

        lock (_lock)
        {
            replaced = _current;
            _current = movement;
        }

        replaced?.Complete(MovementResult.Cancelled);

        return movement.Completion.Task;
    }

    public void Jump()
    {
        lock (_lock)
            _jumpRequested = true;
    }

    public void Stop()
    {
        Movement? cancelled;

        lock (_lock)
        {
            cancelled = _current;
            _current = null;
        }

        cancelled?.Complete(MovementResult.Cancelled);
    }

    /// <summary>
    /// Produces the input for this tick.
    /// </summary>
    /// <param name="position">The player's current position.</param>
    internal MovementInput GetInput(Vector3d position)
    {
        lock (_lock)
        {
            var jump = _jumpRequested;
            _jumpRequested = false;

            if (_current is null)
                return MovementInput.Idle with { Jump = jump };

            // The target is fixed when the movement starts rather than recomputed
            // from the current position, so drifting sideways does not accumulate
            // across a sequence of moves.
            _current.EnsureTarget(position);

            // Auto jump only fires while actually blocked, and only once per
            // obstacle, so a wall does not turn into continuous hopping.
            var shouldJump = jump || (_current.AutoJump && _current.WantsJump);
            _current.WantsJump = false;

            return new MovementInput(_current.Direction, _current.Mode, shouldJump);
        }
    }

    /// <summary>
    /// Feeds back what the physics made of the input, ending the movement when it
    /// arrived or stopped making progress.
    /// </summary>
    internal void OnStepped(PhysicsStep step)
    {
        Movement? finished = null;
        var result = MovementResult.Arrived;

        lock (_lock)
        {
            if (_current is null)
                return;

            var remaining = _current.RemainingDistance(step.Position);

            if (remaining <= ArrivalTolerance || _current.HasPassedTarget(step.Position))
            {
                finished = _current;
                _current = null;
                result = MovementResult.Arrived;
            }
            else if (step.Blocked && _current.AutoJump && step.OnGround && !_current.JumpAttempted)
            {
                // Give the obstacle one jump before declaring it impassable.
                _current.WantsJump = true;
                _current.JumpAttempted = true;
            }
            else if (_current.TrackProgress(remaining))
            {
                finished = _current;
                _current = null;
                result = MovementResult.Blocked;
            }
        }

        if (finished is null)
            return;

        logger.LogDebug("Movement ended as {Result}", result);

        finished.Complete(result);
    }

    /// <summary>
    /// One movement request in progress.
    /// </summary>
    private sealed class Movement(Vector3d direction, double distance, MovementMode mode, bool autoJump)
    {
        public Vector3d Direction { get; } = direction;

        public MovementMode Mode { get; } = mode;

        public bool AutoJump { get; } = autoJump;

        public TaskCompletionSource<MovementResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool WantsJump { get; set; }

        public bool JumpAttempted { get; set; }

        private Vector3d? _target;
        private double _bestRemaining = double.MaxValue;
        private int _ticksWithoutProgress;

        /// <summary>
        /// Fixes the target on the first tick, based on where the player was when
        /// the movement actually started.
        /// </summary>
        public void EnsureTarget(Vector3d position)
            => _target ??= position + Direction * distance;

        public double RemainingDistance(Vector3d position)
            => _target is null ? distance : position.HorizontalDistanceTo(_target);

        /// <summary>
        /// Determines whether the player overshot the target, which a single tick
        /// can easily do at sprinting speed.
        /// </summary>
        public bool HasPassedTarget(Vector3d position)
        {
            if (_target is null)
                return false;

            var toTarget = _target - position;

            // Still ahead while the remaining vector points the same way we walk.
            return toTarget.X * Direction.X + toTarget.Z * Direction.Z <= 0;
        }

        /// <summary>
        /// Records progress and reports whether the movement is stuck.
        /// </summary>
        public bool TrackProgress(double remaining)
        {
            if (remaining < _bestRemaining - MinimumProgress)
            {
                _bestRemaining = remaining;
                _ticksWithoutProgress = 0;

                return false;
            }

            return ++_ticksWithoutProgress >= StuckTicks;
        }

        public void Complete(MovementResult result)
            => Completion.TrySetResult(result);
    }
}
