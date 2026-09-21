using Vortex.Shared;

namespace Vortex.Modules.Player.Abstraction;

/// <summary>
/// What the player looks like at the start of a tick, which is what a movement's
/// conditions are judged on.
/// </summary>
/// <remarks>
/// Everything here is read before the tick is simulated, so the position and
/// velocity are the ones the coming step starts from. <see cref="Blocked"/> is
/// the exception and necessarily looks back: whether geometry stopped the player
/// is only known once the step has been taken, so it describes the step before
/// this one. That is exactly what it is wanted for -- running into something is
/// reacted to on the tick after the bump.
/// </remarks>
/// <param name="Position">Where the player's feet are.</param>
/// <param name="Velocity">How fast it is going, in blocks per tick.</param>
/// <param name="OnGround">Whether it is standing on something.</param>
/// <param name="Blocked">Whether the previous step was stopped by geometry.</param>
/// <param name="TicksInPhase">
/// How many ticks the current phase of the movement has been running for. Zero
/// on the phase's first tick.
/// </param>
/// <param name="LeftTheGround">
/// Whether the player has been in the air at any point during the movement so
/// far. This is what tells standing still apart from having come back down,
/// which <see cref="OnGround"/> on its own cannot.
/// </param>
public record MovementState(
    Vector3d Position,
    Vector3d Velocity,
    bool OnGround,
    bool Blocked,
    int TicksInPhase = 0,
    bool LeftTheGround = false)
{
    /// <summary>Standing still at the origin, before the loop has reported anything.</summary>
    public static MovementState Unknown { get; } = new(Vector3d.Zero, Vector3d.Zero, OnGround: true, Blocked: false);
}
