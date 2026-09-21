using Vortex.Shared;

namespace Vortex.Modules.Player.Abstraction;

/// <summary>
/// Carries out movement. It executes, it does not plan.
/// </summary>
/// <remarks>
/// <para>
/// This layer knows nothing about routes, obstacles or alternatives. It is given
/// somewhere to be and a way of getting there, it goes, and it reports whether
/// it arrived. Deciding where to go next, and what to do about an obstacle,
/// belongs above it.
/// </para>
/// <para>
/// The named movements are the ones that need the physics to be timed properly,
/// which is why they live here rather than being assembled by every caller.
/// Anything else is an <see cref="Execute"/> of a plan the caller builds itself.
/// </para>
/// </remarks>
public interface IMovementController
{
    /// <summary>
    /// Gets a value indicating whether a movement is currently in progress.
    /// </summary>
    bool IsMoving { get; }

    /// <summary>
    /// Walks to a point on the level the player is already on, in a straight
    /// line.
    /// </summary>
    /// <remarks>
    /// Straight in any direction, not just along an axis: a diagonal is just
    /// another direction here.
    /// </remarks>
    /// <param name="target">Where to end up.</param>
    /// <param name="mode">How to move.</param>
    /// <param name="autoJump">
    /// Whether to jump at an obstacle that blocks the way. This only helps
    /// against something to walk into; clearing a gap needs <see cref="JumpTo"/>.
    /// </param>
    /// <returns>Whether it got there or the way was blocked.</returns>
    Task<MovementResult> WalkTo(Vector3d target, MovementMode mode = MovementMode.Walk, bool autoJump = false);

    /// <summary>
    /// Climbs onto a block one higher by walking into it and jumping.
    /// </summary>
    /// <remarks>
    /// Walking into the block first is what makes this land on top rather than
    /// sail over: the collision takes the speed away, so the jump goes up rather
    /// than along.
    /// </remarks>
    /// <param name="target">The centre of the block to end up on.</param>
    /// <param name="mode">How to move.</param>
    Task<MovementResult> StepUpTo(Vector3d target, MovementMode mode = MovementMode.Walk);

    /// <summary>
    /// Walks off an edge and falls to the block below.
    /// </summary>
    /// <remarks>
    /// The fall is part of the movement rather than something to wait out
    /// afterwards, so whatever comes next is decided from solid ground.
    /// </remarks>
    /// <param name="target">The centre of the block to land on.</param>
    /// <param name="mode">How to walk up to the edge.</param>
    Task<MovementResult> DropTo(Vector3d target, MovementMode mode = MovementMode.Walk);

    /// <summary>
    /// Runs up to an edge, jumps from it, and comes down across a gap.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="WalkTo"/> with auto jump, which reacts to bumping into
    /// something, this has to be committed to before the edge: by the time the
    /// ground runs out it is already too late, and a jump from a standstill goes
    /// almost straight up. Deciding that a gap is worth jumping belongs above
    /// this; getting the run-up and the timing right is what happens here. The
    /// landing may be level with the take-off, above it or below it.
    /// </remarks>
    /// <param name="takeOff">The edge to leave the ground at.</param>
    /// <param name="landing">The centre of the block to come down on.</param>
    /// <param name="mode">How to run up. Sprinting is what carries a wide gap.</param>
    /// <returns>
    /// Whether the player got across, or came down somewhere else.
    /// </returns>
    Task<MovementResult> JumpTo(Vector3d takeOff, Vector3d landing, MovementMode mode = MovementMode.Walk);

    /// <summary>
    /// Carries out a movement the caller has built itself.
    /// </summary>
    /// <remarks>
    /// The way in for anything the named movements do not cover. A plan is a
    /// short sequence of directions to hold and moments to let go of them, so
    /// this is how a new kind of movement gets tried without anything here
    /// having to learn about it.
    /// </remarks>
    Task<MovementResult> Execute(MovementPlan plan);

    /// <summary>
    /// Jumps once, if the player is on the ground.
    /// </summary>
    void Jump();

    /// <summary>
    /// Stops the current movement.
    /// </summary>
    void Stop();
}
