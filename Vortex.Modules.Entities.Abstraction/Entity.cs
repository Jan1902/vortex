using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// The entity's metadata as the server sent it, by index. What an index means
    /// depends on <see cref="Type"/>; <see cref="TryGetData"/> looks values up by name.
    /// </summary>
    public ImmutableDictionary<int, object?> Metadata { get; init; } = ImmutableDictionary<int, object?>.Empty;

    /// <summary>
    /// Reads a metadata value by name.
    /// </summary>
    /// <returns>
    /// Whether the entity has the field, the server has sent it, and it holds a
    /// <typeparamref name="T"/>.
    /// </returns>
    public bool TryGetData<T>(EntityDataKey key, [MaybeNullWhen(false)] out T value)
    {
        if (Metadata.TryGetValue(EntityDataKeys.IndexOf(Type, key), out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// The item an item entity lying on the ground is, or the item an entity such
    /// as an item frame holds; <c>null</c> for anything else.
    /// </summary>
    public ItemStack? Item => TryGetData<ItemStack>(EntityDataKey.Item, out var item) ? item : null;

    /// <summary>The health of a living entity, or <c>null</c> if unknown or not living.</summary>
    public float? Health => TryGetData<float>(EntityDataKey.Health, out var health) ? health : null;

    /// <summary>The name given with a name tag, as a text component, if any.</summary>
    public NbtTag? CustomName => TryGetData<NbtTag>(EntityDataKey.CustomName, out var name) ? name : null;
}
