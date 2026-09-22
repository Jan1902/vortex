namespace Vortex.Modules.Entities.Abstraction;

/// <summary>An entity came into view.</summary>
public record EntitySpawnedEvent(Entity Entity);

/// <summary>An entity is gone: it died, was picked up, or left the tracking range.</summary>
/// <param name="Entity">The entity as it was last known.</param>
public record EntityRemovedEvent(Entity Entity);
