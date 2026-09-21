using Vortex.Shared;

namespace Vortex.Modules.Player.Abstraction;

/// <summary>
/// The conditions a movement is built out of: the points and moments at which
/// one phase hands over to the next, or an action fires.
/// </summary>
/// <remarks>
/// Deliberately a small vocabulary. Every movement the bot can make -- walking,
/// stepping up, dropping off an edge, jumping a gap straight or diagonally, with
/// or without a run-up -- is some arrangement of these over two or three phases,
/// which is what keeps the controller free of a special case per kind of move.
/// </remarks>
public static class When
{
    /// <summary>Straight away, on the phase's first tick.</summary>
    /// <remarks>Used for an action that belongs to the phase itself, such as the take-off of a jump.</remarks>
    public static Func<MovementState, bool> Now { get; } = _ => true;

    /// <summary>The player has left the ground.</summary>
    public static Func<MovementState, bool> Airborne { get; } = state => !state.OnGround;

    /// <summary>The player is standing on something.</summary>
    public static Func<MovementState, bool> Landed { get; } = state => state.OnGround;

    /// <summary>
    /// The player has been in the air and is standing on something again.
    /// </summary>
    /// <remarks>
    /// What "wait for the fall to finish" actually means. <see cref="Landed"/>
    /// on its own is already true of a player that has not moved yet, so a phase
    /// that waits on it from the ground is over before anything happens. Having
    /// left the ground is remembered for the whole movement, so a phase entered
    /// on the very tick of landing still counts it.
    /// </remarks>
    public static Func<MovementState, bool> TouchedDown { get; } =
        state => state.OnGround && state.LeftTheGround;

    /// <summary>Geometry stopped the player's last step.</summary>
    public static Func<MovementState, bool> Blocked { get; } = state => state.Blocked;

    /// <summary>
    /// The player has reached or passed a point, measured along the direction it
    /// is travelling in.
    /// </summary>
    /// <remarks>
    /// A plane rather than a sphere, so that drifting sideways neither holds the
    /// condition back nor lets it fire early, and so that overshooting in one
    /// fast tick still counts as having got there.
    /// </remarks>
    /// <param name="point">The point to reach.</param>
    /// <param name="direction">The direction of travel, in the XZ plane.</param>
    public static Func<MovementState, bool> PastPoint(Vector3d point, Vector3d direction)
        => state => (point.X - state.Position.X) * direction.X
                  + (point.Z - state.Position.Z) * direction.Z <= 0;

    /// <summary>The player is within a given horizontal distance of a point.</summary>
    public static Func<MovementState, bool> Within(Vector3d point, double distance)
        => state => state.Position.HorizontalDistanceTo(point) <= distance;

    /// <summary>The player is at or above a height.</summary>
    public static Func<MovementState, bool> AtOrAbove(double height)
        => state => state.Position.Y >= height;

    /// <summary>A condition does not hold.</summary>
    public static Func<MovementState, bool> Not(Func<MovementState, bool> condition)
        => state => !condition(state);

    /// <summary>Any one of several conditions holds.</summary>
    public static Func<MovementState, bool> Any(params Func<MovementState, bool>[] conditions)
        => state => conditions.Any(condition => condition(state));

    /// <summary>All of several conditions hold.</summary>
    public static Func<MovementState, bool> All(params Func<MovementState, bool>[] conditions)
        => state => conditions.All(condition => condition(state));
}
