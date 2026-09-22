using Microsoft.Extensions.Logging;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player;

/// <summary>
/// Carries out one movement at a time and reports how it ended.
/// </summary>
/// <remarks>
/// <para>
/// Every movement is steered the same way: each tick the player faces the
/// destination afresh and pushes towards it, and stops pushing once it is close
/// enough. Nothing is scripted in advance. Overshooting simply turns the push
/// round into a brake, and drifting sideways corrects itself, which is what lets
/// the whole thing get by without timing anything.
/// </para>
/// <para>
/// What tells the movements apart is a handful of plain rules: when to jump, how
/// close counts as close enough to let go, and what counts as having made it.
/// This is the approach Baritone and mineflayer-pathfinder take, and for the same
/// reason -- the route is searched again after every move, so the move only has
/// to get the player there, not get it there exactly.
/// </para>
/// </remarks>
internal class MovementController(ILogger<MovementController> logger) : IMovementController
{
    /// <summary>How close to the destination a walk has to get to be done.</summary>
    private const double ArrivalTolerance = 0.35;

    /// <summary>
    /// Where a walking jump leaves the ground, relative to the edge: just
    /// before it.
    /// </summary>
    /// <remarks>
    /// A walking jump is the controlled one. It goes a touch early and then
    /// lets go in the air once the momentum covers the rest, which is what lands
    /// it on a block one or two out rather than sailing past.
    /// </remarks>
    private const double WalkTakeOff = -0.1;

    /// <summary>
    /// How many ticks' worth of its own speed a walking jump reckons its
    /// momentum will still carry it once it lets go.
    /// </summary>
    private const double WalkCarry = 6;

    /// <summary>
    /// Where a sprinting jump leaves the ground: a little past the edge, as late
    /// as the player's feet still find it.
    /// </summary>
    /// <remarks>
    /// A sprint is only ever planned for a gap walking cannot reach, so it is the
    /// jump for distance. It leaves as late as possible and pushes all the way.
    /// </remarks>
    private const double SprintTakeOff = 0.2;

    /// <summary>Closer than this, there is no direction left to push in.</summary>
    private const double DeadZone = 0.05;

    /// <summary>
    /// How close to the landing a drop stops pushing, while still on the ground.
    /// </summary>
    /// <remarks>
    /// Much earlier than a jump, because in the air there is next to no say in
    /// where the player goes. Walking off an edge at full speed carries it a
    /// block past the one below; letting go this far out means friction has
    /// taken most of the speed by the time it topples over.
    /// </remarks>
    private const double DropLetGo = 0.45;

    /// <summary>Slower than this on the ground counts as standing still.</summary>
    private const double Stalled = 0.01;

    /// <summary>
    /// Progress smaller than this over <see cref="StuckTicks"/> ticks counts as
    /// being stuck, which catches grinding along a corner without ever cleanly
    /// colliding.
    /// </summary>
    private const double MinimumProgress = 0.05;

    private const int StuckTicks = 10;

    /// <summary>Ten seconds, which is longer than any single move should take.</summary>
    internal const int Timeout = 200;

    private readonly object _lock = new();

    private Execution? _current;
    private bool _jumpRequested;

    /// <summary>
    /// Where the physics loop last saw the player. A movement is aimed from
    /// where the player is, and a caller asking between two ticks has no way to
    /// hand that over itself.
    /// </summary>
    private Vector3d _lastPosition = Vector3d.Zero;

    public bool IsMoving
    {
        get { lock (_lock) return _current is not null; }
    }

    public Task<MovementResult> WalkTo(Vector3d target, MovementMode mode = MovementMode.Walk, bool autoJump = false)
    {
        lock (_lock)
        {
            // Already there: a walk that starts inside its own tolerance would
            // otherwise only finish on the next tick, for nothing.
            if (_lastPosition.HorizontalDistanceTo(target) <= ArrivalTolerance)
                return Task.FromResult(MovementResult.Arrived);

            return Start(new Movement(MovementKind.Walk, target, mode, autoJump));
        }
    }

    public Task<MovementResult> StepUpTo(Vector3d target, MovementMode mode = MovementMode.Walk)
        => Start(new Movement(MovementKind.StepUp, target, mode));

    public Task<MovementResult> DropTo(Vector3d target, MovementMode mode = MovementMode.Walk)
        => Start(new Movement(MovementKind.Drop, target, mode));

    public Task<MovementResult> JumpTo(Vector3d takeOff, Vector3d landing, MovementMode mode = MovementMode.Walk)
        => Start(new Movement(MovementKind.Jump, landing, mode, TakeOff: takeOff));

    public void Jump()
    {
        lock (_lock)
            _jumpRequested = true;
    }

    public void Stop()
        => Abandon(MovementResult.Cancelled);

    /// <summary>
    /// Reports that the server moved the player itself.
    /// </summary>
    /// <remarks>
    /// The movement in progress was aimed from where the player believed it
    /// was. After a correction that means nothing, so it ends -- as something
    /// other than a cancellation, because the caller is expected to try again
    /// rather than give up.
    /// </remarks>
    internal void Desynchronize()
        => Abandon(MovementResult.Desynced);

    private Task<MovementResult> Start(Movement movement)
    {
        Execution? replaced;
        Execution started;

        lock (_lock)
        {
            replaced = _current;
            started = new Execution(movement, _lastPosition);
            _current = started;
        }

        replaced?.Complete(MovementResult.Cancelled);

        return started.Completion.Task;
    }

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
    /// <param name="tick">The state the coming step starts from.</param>
    internal MovementInput Tick(MovementState tick)
    {
        Execution? finished = null;
        MovementResult result = default;
        string? reason = null;
        MovementInput input;

        lock (_lock)
        {
            _lastPosition = tick.Position;

            var requestedJump = _jumpRequested;
            _jumpRequested = false;

            if (_current is null)
                return MovementInput.Idle with { Jump = requestedJump };

            if (_current.Step(tick, out input) is { } ended)
            {
                (result, reason) = ended;
                finished = _current;
                _current = null;
                input = MovementInput.Idle;
            }

            input = input with { Jump = input.Jump || requestedJump };
        }

        if (finished is null)
            return input;

        logger.LogDebug("{Kind} to {X:F1} {Y:F1} {Z:F1} ended as {Result}{Reason}",
            finished.Movement.Kind,
            finished.Movement.Destination.X, finished.Movement.Destination.Y, finished.Movement.Destination.Z,
            result,
            reason is null ? string.Empty : $": {reason}");

        finished.Complete(result);

        return input;
    }

    /// <summary>One movement in progress.</summary>
    private sealed class Execution(Movement movement, Vector3d origin)
    {
        /// <summary>
        /// The way the movement goes, fixed from where it starts. Only used to
        /// measure along -- the take-off point, the block past a drop -- never to
        /// steer, which is aimed afresh every tick.
        /// </summary>
        private readonly Vector3d _heading = Normalize(movement.Destination - (movement.TakeOff ?? origin));

        private bool _jumped;
        private bool _leftGround;
        private int _ticks;
        private double _closest = double.MaxValue;
        private int _ticksWithoutProgress;

        public Movement Movement => movement;

        public TaskCompletionSource<MovementResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Complete(MovementResult result)
            => Completion.TrySetResult(result);

        /// <summary>
        /// Works out this tick's input, or how the movement ended.
        /// </summary>
        /// <returns>The result and why, once the movement is over; otherwise null.</returns>
        public (MovementResult Result, string? Reason)? Step(MovementState state, out MovementInput input)
        {
            input = MovementInput.Idle;

            _leftGround |= !state.OnGround;
            _ticks++;

            if (Outcome(state) is { } outcome)
                return outcome;

            if (_ticks > Timeout)
                return (MovementResult.Blocked, $"still going after {Timeout} ticks");

            if (IsStuck(state, out var remaining))
                return (MovementResult.Blocked, $"no ground made in {StuckTicks} ticks, {remaining:F2} blocks short");

            var toTarget = Normalize(new Vector3d(
                movement.Destination.X - state.Position.X, 0, movement.Destination.Z - state.Position.Z));

            var speed = Math.Sqrt(state.Velocity.X * state.Velocity.X + state.Velocity.Z * state.Velocity.Z);

            var push = state.Position.HorizontalDistanceTo(movement.Destination) > DeadZone
                && ShouldPush(state, speed);

            var jump = !_jumped && state.OnGround && WantsJump(state);

            _jumped |= jump;

            input = new MovementInput(
                push ? toTarget : null,
                movement.Mode,
                jump,
                AllowStepUp: movement.Kind is MovementKind.Walk or MovementKind.StepUp);

            return null;
        }

        /// <summary>
        /// Whether to keep pushing towards the destination.
        /// </summary>
        /// <remarks>
        /// Letting go is the only say there is in where a fall or a jump ends,
        /// because in the air the player keeps nearly all the speed it has and
        /// can add or take away almost none.
        /// </remarks>
        private bool ShouldPush(MovementState state, double speed)
        {
            var remaining = state.Position.HorizontalDistanceTo(movement.Destination);

            switch (movement.Kind)
            {
                case MovementKind.Drop:
                    // Let go on the ground, short of the edge, so friction takes
                    // the speed off before the fall. A player that came to rest on
                    // the ledge gets a nudge, so letting go early never strands it.
                    return remaining > DropLetGo || (state.OnGround && !_leftGround && speed < Stalled);

                case MovementKind.Jump when !state.OnGround && movement.Mode != MovementMode.Sprint:
                    // A walking jump lets go once the momentum covers the rest.
                    return remaining > WalkCarry * speed;

                default:
                    return true;
            }
        }

        /// <summary>Whether this is the moment to jump, once per movement.</summary>
        private bool WantsJump(MovementState state)
            => movement.Kind switch
            {
                // Walking into the block first is what makes a step up land on
                // it: the bump takes the speed away, so the jump goes up rather
                // than along.
                MovementKind.StepUp => state.Blocked,
                MovementKind.Walk => movement.AutoJump && state.Blocked,

                MovementKind.Jump => Along(state.Position - movement.TakeOff!)
                    >= (movement.Mode == MovementMode.Sprint ? SprintTakeOff : WalkTakeOff),

                _ => false
            };

        /// <summary>How the movement ended, if it has.</summary>
        private (MovementResult, string?)? Outcome(MovementState state)
        {
            if (!state.OnGround)
                return null;

            var destination = movement.Destination;

            switch (movement.Kind)
            {
                case MovementKind.Walk:
                case MovementKind.StepUp:
                    // Horizontally close is enough: the ground decides the
                    // height, and nobody stands within reach of the middle of a
                    // block they failed to climb.
                    return state.Position.HorizontalDistanceTo(destination) <= ArrivalTolerance
                        ? (MovementResult.Arrived, null)
                        : null;

                default:
                    // A fall or a jump is only over once it has been in the air
                    // and come back down, and is judged by where that was.
                    if (!_leftGround)
                        return null;

                    return LandedWhereItShould(state.Position)
                        ? (MovementResult.Arrived, null)
                        : (MovementResult.Blocked, $"came down at {state.Position.X:F1} {state.Position.Y:F1} {state.Position.Z:F1}");
            }
        }

        /// <summary>
        /// Whether the player came down on the block it was aimed at.
        /// </summary>
        /// <remarks>
        /// A drop may also end one block further on, at the same height. There is
        /// no stopping a fall once it has started, and a player that walks off an
        /// edge keeps some of its speed all the way down. Baritone accepts the
        /// same; the route only plans a drop where that block is safe to land on.
        /// </remarks>
        private bool LandedWhereItShould(Vector3d position)
        {
            var landed = position.ToBlockPosition();
            var aimed = movement.Destination.ToBlockPosition();

            if (landed == aimed)
                return true;

            return movement.Kind == MovementKind.Drop && landed == aimed + Step(_heading);
        }

        /// <summary>
        /// Whether the player has stopped getting anywhere.
        /// </summary>
        /// <remarks>
        /// Only asked on the ground. In the air there is next to no say in where
        /// the player goes, and a jump that rises before it travels would read as
        /// ten ticks of getting nowhere.
        /// </remarks>
        private bool IsStuck(MovementState state, out double remaining)
        {
            remaining = state.Position.HorizontalDistanceTo(movement.Destination);

            if (!state.OnGround || remaining < _closest - MinimumProgress)
            {
                _closest = Math.Min(_closest, remaining);
                _ticksWithoutProgress = 0;

                return false;
            }

            return ++_ticksWithoutProgress >= StuckTicks;
        }

        private double Along(Vector3d offset)
            => offset.X * _heading.X + offset.Z * _heading.Z;

        /// <summary>The single block step in a direction, straight or diagonal.</summary>
        private static Vector3i Step(Vector3d heading)
            => new(Axis(heading.X), 0, Axis(heading.Z));

        private static int Axis(double component)
            => Math.Abs(component) < 0.3 ? 0 : Math.Sign(component);

        private static Vector3d Normalize(Vector3d direction)
        {
            var length = Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);

            return length < 1e-6
                ? Vector3d.Zero
                : new Vector3d(direction.X / length, 0, direction.Z / length);
        }
    }
}
