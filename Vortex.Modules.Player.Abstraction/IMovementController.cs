using Vortex.Shared;

namespace Vortex.Modules.Player.Abstraction;

/// <summary>
/// Carries out movement. It executes, it does not plan.
/// </summary>
/// <remarks>
/// This layer knows nothing about routes, obstacles or alternatives. It walks in
/// the direction it is given until it arrives or stops making progress, and then
/// reports which of the two happened. Deciding where to go next, and what to do
/// about an obstacle, belongs above it.
/// </remarks>
public interface IMovementController
{
    /// <summary>
    /// Gets a value indicating whether a movement is currently in progress.
    /// </summary>
    bool IsMoving { get; }

    /// <summary>
    /// Walks a given distance in a straight line.
    /// </summary>
    /// <param name="direction">
    /// The direction to walk in, in the XZ plane. Does not need to be normalized;
    /// its length is ignored.
    /// </param>
    /// <param name="distance">How far to walk, in blocks.</param>
    /// <param name="mode">How to move.</param>
    /// <param name="autoJump">
    /// Whether to jump at an obstacle that blocks the way. This only helps against
    /// something to walk into; clearing a gap needs an explicit <see cref="Jump"/>.
    /// </param>
    /// <returns>Whether the distance was covered or the way was blocked.</returns>
    Task<MovementResult> Move(Vector3d direction, double distance, MovementMode mode = MovementMode.Walk, bool autoJump = false);

    /// <summary>
    /// Jumps once, if the player is on the ground.
    /// </summary>
    void Jump();

    /// <summary>
    /// Stops the current movement.
    /// </summary>
    void Stop();
}
