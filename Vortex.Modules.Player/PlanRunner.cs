using Vortex.Modules.Player.Abstraction;

namespace Vortex.Modules.Player;

/// <summary>
/// Walks one movement plan, a tick at a time.
/// </summary>
/// <remarks>
/// <para>
/// All of what running a movement means and none of what it takes to run one for
/// real: no locking, no waiting, no logging. That is on purpose. The controller
/// wraps this to drive the actual player, and the simulator runs the very same
/// thing against the physics to answer "where would this put me" -- so the
/// answer to that question is not a second opinion about how a movement goes,
/// it is the same opinion.
/// </para>
/// <para>
/// It knows nothing about what a particular movement means. Walking, stepping
/// up, dropping off an edge and jumping a gap are all this same code; what makes
/// them different is the plan.
/// </para>
/// </remarks>
internal sealed class PlanRunner(MovementPlan plan)
{
    /// <summary>
    /// Progress smaller than this over <see cref="StuckTicks"/> ticks counts as
    /// being stuck. This catches the cases where the player grinds along a corner
    /// without ever cleanly colliding.
    /// </summary>
    private const double MinimumProgress = 0.05;

    private const int StuckTicks = 10;

    private int _phase;
    private int _ticksInPhase;
    private int _ticksTotal;
    private bool _jumped;

    /// <summary>
    /// Whether the player has been in the air since the plan started. Kept for
    /// the whole movement rather than reset with each phase: a coast that waits
    /// to touch down is usually entered at the moment of landing, and a phase
    /// that forgot the fall it was waiting on would wait for ever.
    /// </summary>
    private bool _leftGround;
    private double _bestRemaining = double.MaxValue;
    private int _ticksWithoutProgress;

    public MovementPlan Plan => plan;

    /// <summary>Gets why the plan was given up on, once it has been.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// Advances the plan by one tick.
    /// </summary>
    /// <param name="tick">The state the coming step starts from.</param>
    /// <returns>
    /// What to hold down this tick, and how the plan ended if this tick ended it.
    /// </returns>
    public (MovementInput Input, MovementResult? Result) Step(MovementState tick)
    {
        _leftGround |= !tick.OnGround;

        var state = tick with { TicksInPhase = _ticksInPhase, LeftTheGround = _leftGround };

        // A phase is never judged before it has run: its condition is about what
        // the phase achieved, and on the tick it starts it has achieved nothing.
        // Without that, a coast that waits to land would be over before the
        // player ever left the ground.
        if (_ticksInPhase > 0 && plan.Phases[_phase].Until(state))
        {
            _phase++;
            _ticksInPhase = 0;
            _jumped = false;

            if (_phase >= plan.Phases.Count)
            {
                var arrived = plan.Succeeded?.Invoke(state) ?? true;

                if (!arrived)
                    Reason = "ended up somewhere else";

                return (MovementInput.Idle, arrived ? MovementResult.Arrived : MovementResult.Blocked);
            }

            state = state with { TicksInPhase = 0 };
        }

        if (HasGivenUp(state))
            return (MovementInput.Idle, MovementResult.Blocked);

        var phase = plan.Phases[_phase];

        _ticksInPhase++;
        _ticksTotal++;

        return (new MovementInput(phase.Direction, phase.Mode, WantsJump(state, phase), phase.AllowStepUp), null);
    }

    /// <summary>
    /// Whether to jump this tick.
    /// </summary>
    /// <remarks>
    /// Only from the ground, and only once per phase. The jump is spent on the
    /// attempt that could actually take off rather than on the wish for one, so
    /// a phase does not use it up while in mid-air.
    /// </remarks>
    private bool WantsJump(MovementState state, MovementPhase phase)
    {
        if (_jumped || !state.OnGround || phase.JumpWhen?.Invoke(state) != true)
            return false;

        _jumped = true;

        return true;
    }

    /// <summary>
    /// Whether the movement has stopped being worth continuing.
    /// </summary>
    /// <remarks>
    /// Progress is only asked about on the ground. In the air the player has
    /// almost no say in where it is going, and a jump that rises before it
    /// travels would read as ten ticks of getting nowhere.
    /// </remarks>
    private bool HasGivenUp(MovementState state)
    {
        if (_ticksTotal >= plan.Timeout)
        {
            Reason = $"still going after {_ticksTotal} ticks";

            return true;
        }

        if (!state.OnGround)
        {
            _ticksWithoutProgress = 0;

            return false;
        }

        var remaining = state.Position.HorizontalDistanceTo(plan.Destination);

        if (remaining < _bestRemaining - MinimumProgress)
        {
            _bestRemaining = remaining;
            _ticksWithoutProgress = 0;

            return false;
        }

        if (++_ticksWithoutProgress < StuckTicks)
            return false;

        Reason = $"no ground made in {StuckTicks} ticks, {remaining:F2} blocks short";

        return true;
    }
}
