using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player;

/// <summary>
/// What the player is trying to do this tick. This is the only way anything
/// outside influences the physics; the simulation itself never decides.
/// </summary>
/// <param name="Direction">
/// Normalized direction in the XZ plane, or <c>null</c> to stand still and coast
/// to a stop.
/// </param>
/// <param name="Mode">How to move.</param>
/// <param name="Jump">Whether to jump, which only takes effect on the ground.</param>
/// <param name="AllowStepUp">
/// Whether to climb onto an obstacle that is low enough to step onto. This is
/// mechanical and separate from jumping.
/// </param>
internal record MovementInput(
    Vector3d? Direction,
    MovementMode Mode = MovementMode.Walk,
    bool Jump = false,
    bool AllowStepUp = true)
{
    /// <summary>Standing still.</summary>
    /// <remarks>
    /// Naming the argument is required: <c>new(null)</c> binds to the record's
    /// copy constructor, which then dereferences null.
    /// </remarks>
    public static MovementInput Idle { get; } = new(Direction: null);
}
