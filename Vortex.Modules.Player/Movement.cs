using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player;

/// <summary>The kinds of movement the controller knows how to carry out.</summary>
internal enum MovementKind
{
    /// <summary>Walking to a point on the same level.</summary>
    Walk,

    /// <summary>Walking into a block one higher and jumping onto it.</summary>
    StepUp,

    /// <summary>Walking off an edge and falling to the block below.</summary>
    Drop,

    /// <summary>Jumping from an edge across a gap.</summary>
    Jump
}

/// <summary>
/// One movement: what kind it is, where it goes, and how.
/// </summary>
/// <param name="Kind">What kind of movement this is.</param>
/// <param name="Destination">Where the player's feet should end up.</param>
/// <param name="Mode">Walking, sprinting or sneaking.</param>
/// <param name="AutoJump">
/// For a walk: whether to try one jump at whatever gets in the way.
/// </param>
/// <param name="TakeOff">For a jump: the edge the ground ends at.</param>
internal sealed record Movement(
    MovementKind Kind,
    Vector3d Destination,
    MovementMode Mode,
    bool AutoJump = false,
    Vector3d? TakeOff = null);
