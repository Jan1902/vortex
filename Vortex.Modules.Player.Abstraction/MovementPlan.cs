using Vortex.Shared;

namespace Vortex.Modules.Player.Abstraction;

/// <summary>
/// A movement, as the phases to go through in turn and the test for whether it
/// worked.
/// </summary>
/// <remarks>
/// <para>
/// The plan is fully worked out before it starts, from where the player is at
/// the time. Nothing in it is recomputed while it runs, which is what makes a
/// movement something that can be reasoned about -- and tested -- on its own,
/// rather than an interaction between a controller's state and the world.
/// </para>
/// <para>
/// A plan ends when the last phase's condition holds, and is then judged by
/// <see cref="Succeeded"/>. It also ends, as a failure, if the player stops
/// making ground or runs out of time, which is the one piece of judgement the
/// controller keeps for itself because every movement needs it and none of them
/// needs it differently.
/// </para>
/// </remarks>
/// <param name="Phases">The phases to go through, in order. At least one.</param>
/// <param name="Destination">
/// Where the movement is ultimately aimed. Used to tell progress from grinding
/// against a corner, and to say in a log what the bot was trying to do.
/// </param>
/// <param name="Succeeded">
/// Whether the player ended up where the movement wanted it, judged on the state
/// it finished in. <c>null</c> means running the phases to the end is success in
/// itself, which is the case for anything that simply walks somewhere.
/// </param>
/// <param name="Timeout">
/// How many ticks the whole plan may take before it is given up as failed. This
/// catches a condition that never comes true -- a landing that never happens
/// because the player went over the edge of the world.
/// </param>
public sealed record MovementPlan(
    IReadOnlyList<MovementPhase> Phases,
    Vector3d Destination,
    Func<MovementState, bool>? Succeeded = null,
    int Timeout = MovementPlan.DefaultTimeout)
{
    /// <summary>Ten seconds, which is longer than any single move should take.</summary>
    public const int DefaultTimeout = 200;

    /// <summary>A plan with a single phase.</summary>
    public static MovementPlan Single(MovementPhase phase, Vector3d destination)
        => new([phase], destination);
}
