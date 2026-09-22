using Vortex.Shared;

namespace Vortex.Modules.Player;

/// <summary>
/// What the player looks like at the start of a tick.
/// </summary>
/// <remarks>
/// Read before the tick is simulated, so the position and velocity are the ones
/// the coming step starts from. <see cref="Blocked"/> necessarily looks back: it
/// is only known once a step has been taken, so it describes the one before
/// this. That is exactly what it is for -- running into something is answered on
/// the tick after the bump.
/// </remarks>
/// <param name="Position">Where the player's feet are.</param>
/// <param name="Velocity">How fast it is going, in blocks per tick.</param>
/// <param name="OnGround">Whether it is standing on something.</param>
/// <param name="Blocked">Whether the previous step was stopped by geometry.</param>
internal record MovementState(Vector3d Position, Vector3d Velocity, bool OnGround, bool Blocked)
{
    /// <summary>Standing still at the origin, before the loop has reported anything.</summary>
    public static MovementState Unknown { get; } = new(Vector3d.Zero, Vector3d.Zero, OnGround: true, Blocked: false);
}
