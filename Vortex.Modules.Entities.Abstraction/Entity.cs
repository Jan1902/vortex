using Vortex.Data;
using Vortex.Shared;

namespace Vortex.Modules.Entities.Abstraction;

/// <summary>
/// What is known about an entity at one moment: a mob, another player, an item
/// lying on the ground, an arrow in flight.
/// </summary>
/// <remarks>
/// A snapshot that never changes. Each update the server sends replaces it with
/// a new one, so code reading an entity on another thread always sees a
/// consistent state rather than a position that is half updated.
/// </remarks>
/// <param name="Id">The ID the server refers to the entity by in packets. It is only valid while the entity is tracked.</param>
/// <param name="Uuid">The entity's lasting identity. For a player, the UUID of the account.</param>
/// <param name="Position">Where its feet are.</param>
/// <param name="Yaw">Which way it faces, in degrees.</param>
/// <param name="Pitch">How far it looks up or down, in degrees.</param>
/// <param name="HeadYaw">Which way its head faces, in degrees, which may differ from its body.</param>
/// <param name="Velocity">How far it moves per tick, in blocks.</param>
public sealed record Entity(
    int Id,
    Guid Uuid,
    EntityType Type,
    Vector3d Position,
    float Yaw,
    float Pitch,
    float HeadYaw,
    Vector3d Velocity,
    bool OnGround)
{
    /// <summary>
    /// The size every entity is taken to have, in blocks, whatever its real size.
    /// </summary>
    /// <remarks>
    /// Real hitboxes are not part of Mojang's data. Rather than tracking them,
    /// entities are treated as small, so reaching one means getting close to its
    /// middle -- never further than necessary, sometimes closer.
    /// </remarks>
    public const double AssumedSize = 0.5;

    /// <summary>
    /// The middle of the entity, as far as <see cref="AssumedSize"/> goes: the
    /// point to aim at.
    /// </summary>
    public Vector3d Center => Position + new Vector3d(0, AssumedSize / 2, 0);
}
