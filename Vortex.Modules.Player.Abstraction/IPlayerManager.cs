using Vortex.Shared;

namespace Vortex.Modules.Player.Abstraction;

/// <summary>
/// Tracks and controls the bot's own player entity.
/// </summary>
public interface IPlayerManager
{
    /// <summary>
    /// Gets the current position of the player's feet.
    /// </summary>
    Vector3d Position { get; }

    /// <summary>
    /// Gets the current velocity in blocks per tick.
    /// </summary>
    Vector3d Velocity { get; }

    /// <summary>
    /// Gets a value indicating whether the player is standing on solid ground.
    /// </summary>
    bool IsOnGround { get; }

    /// <summary>
    /// Gets the direction the player is facing, in degrees.
    /// </summary>
    float Yaw { get; }

    /// <summary>
    /// Gets the vertical angle the player is looking at, in degrees.
    /// </summary>
    float Pitch { get; }

    /// <summary>
    /// Gets the player's current health, out of twenty.
    /// </summary>
    float Health { get; }

    /// <summary>
    /// Gets a value indicating whether the player is alive. A dead player sits on
    /// the death screen and receives no chunks until it respawns.
    /// </summary>
    bool IsAlive { get; }

    /// <summary>
    /// Gets a value indicating whether the server has placed the player in the
    /// world yet. Nothing can be moved before that happened.
    /// </summary>
    bool IsSpawned { get; }

    /// <summary>
    /// Points the player in a direction.
    /// </summary>
    /// <param name="yaw">The horizontal angle in degrees.</param>
    /// <param name="pitch">The vertical angle in degrees.</param>
    void Look(float yaw, float pitch);

    /// <summary>
    /// Points the player at a position in the world.
    /// </summary>
    void LookAt(Vector3d target);
}
