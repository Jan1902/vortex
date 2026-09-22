namespace Vortex.Modules.Entities.Abstraction;

/// <summary>An entity came into view.</summary>
public record EntitySpawnedEvent(Entity Entity);

/// <summary>An entity is gone: it died, was picked up, or left the tracking range.</summary>
/// <param name="Entity">The entity as it was last known.</param>
public record EntityRemovedEvent(Entity Entity);

/// <summary>
/// Someone picked up an item lying on the ground, or an arrow or experience orb.
/// </summary>
/// <remarks>
/// Only says who took what. What ends up in an inventory, the bot's included,
/// the server reports separately.
/// </remarks>
/// <param name="Item">The entity that was picked up, as it was last known.</param>
/// <param name="CollectorId">The entity that picked it up; compare with <see cref="IEntityManager.SelfId"/>.</param>
/// <param name="Count">How many items of the stack were taken.</param>
public record ItemPickedUpEvent(Entity Item, int CollectorId, int Count);
