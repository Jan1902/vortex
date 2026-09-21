using Vortex.Shared;

namespace Vortex.Modules.Player.Abstraction;

/// <summary>
/// One leg of a movement: what the player holds down, what it does along the
/// way, and when it is done holding it.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of what a movement can say, and it is deliberately as
/// little as a person at a keyboard has: a direction to push, whether to sprint
/// or sneak, when to hit the jump key, and when to let go. Everything the bot
/// can do is a short sequence of these.
/// </para>
/// <para>
/// Letting go matters as much as pushing. In mid-air the player keeps the
/// momentum it took off with, so a jump that holds its direction for the whole
/// arc carries about a block further than one that stops steering partway. That
/// is the only control there is over where a jump ends up, because the take-off
/// itself cannot be made gentler.
/// </para>
/// </remarks>
/// <param name="Direction">
/// The direction to push in, in the XZ plane, or <c>null</c> to let go and coast.
/// Does not need to be normalized. Fixed for the phase rather than re-aimed each
/// tick, because a jump that re-aims mid-air steers backwards as soon as it
/// overshoots.
/// </param>
/// <param name="Until">When this phase hands over to the next one.</param>
/// <param name="Mode">How to move.</param>
/// <param name="JumpWhen">
/// When to jump, or <c>null</c> never. Fires at most once per phase, and only
/// from the ground, so a phase that wants to jump on entry gets exactly one
/// take-off and a wall does not turn into continuous hopping.
/// </param>
/// <param name="AllowStepUp">
/// Whether to climb onto an obstacle low enough to step onto. Off for anything
/// airborne: catching the lip of a gap would turn a planned jump into a
/// scramble.
/// </param>
public sealed record MovementPhase(
    Vector3d? Direction,
    Func<MovementState, bool> Until,
    MovementMode Mode = MovementMode.Walk,
    Func<MovementState, bool>? JumpWhen = null,
    bool AllowStepUp = true)
{
    /// <summary>Pushes in a direction until a condition holds.</summary>
    public static MovementPhase Steer(
        Vector3d direction,
        Func<MovementState, bool> until,
        MovementMode mode = MovementMode.Walk,
        Func<MovementState, bool>? jumpWhen = null,
        bool allowStepUp = true)
        => new(direction, until, mode, jumpWhen, allowStepUp);

    /// <summary>
    /// Lets go and lets momentum and gravity finish the job.
    /// </summary>
    public static MovementPhase Coast(Func<MovementState, bool> until, bool allowStepUp = false)
        => new(Direction: null, until, AllowStepUp: allowStepUp);
}
