using Vortex.Modules.Player.Abstraction;

namespace Vortex.Modules.Player;

/// <summary>
/// Plays a movement out against the physics without anything actually moving,
/// and says whether it would work.
/// </summary>
/// <remarks>
/// <para>
/// This is what lets a movement be aimed instead of guessed at. The moment to
/// jump, the moment to stop pushing before an edge, how far a gap can be -- all
/// of those are the same question, "what would happen if I did this now", and
/// all of them used to be answered by a constant measured once by hand and left
/// to drift.
/// </para>
/// <para>
/// The answer is trustworthy because nothing here is a model of the movement: it
/// is the same <see cref="PlanRunner"/> the controller uses, stepped by the same
/// <see cref="PlayerPhysics"/> the player is stepped by, reading the same world.
/// A simulation and the real thing can only disagree about the world having
/// changed underneath them.
/// </para>
/// </remarks>
internal class MovementSimulator(PlayerPhysics physics)
{
    /// <summary>
    /// Whether playing a plan out from a given state would get the player where
    /// the plan wants it.
    /// </summary>
    /// <param name="start">Where to begin, as a tick of the physics loop would see it.</param>
    /// <param name="plan">The plan to play out.</param>
    public bool Works(MovementState start, MovementPlan plan)
    {
        var runner = new PlanRunner(plan);

        // Starting fresh rather than inheriting where the caller happens to be
        // in its own movement: this is a question about a plan of its own.
        var state = start with { TicksInPhase = 0, LeftTheGround = false };

        // The plan ends itself -- by arriving, by stopping making ground, or by
        // running out of ticks. The bound here is only so that a plan built to
        // outlast its own budget cannot spin.
        for (var tick = 0; tick <= plan.Timeout; tick++)
        {
            var (input, result) = runner.Step(state);

            if (result is not null)
                return result == MovementResult.Arrived;

            state = Ahead(state, input);
        }

        return false;
    }

    /// <summary>
    /// Where one tick of holding something down would put the player.
    /// </summary>
    public MovementState Ahead(MovementState state, MovementInput input)
    {
        var step = physics.Step(state.Position, state.Velocity, state.OnGround, input);

        return new MovementState(step.Position, step.Velocity, step.OnGround, step.Blocked);
    }
}
