using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Entities;

internal class EntityPacketHandler(
    ILogger<EntityPacketHandler> logger,
    IEventBus eventBus,
    EntityManager entities)
    : IPacketHandler<LoginPlay>,
    IPacketHandler<Respawn>,
    IPacketHandler<AddEntity>,
    IPacketHandler<AddExperienceOrb>,
    IPacketHandler<MoveEntityPosition>,
    IPacketHandler<MoveEntityPositionAndRotation>,
    IPacketHandler<MoveEntityRotation>,
    IPacketHandler<TeleportEntity>,
    IPacketHandler<SetEntityMotion>,
    IPacketHandler<RotateHead>,
    IPacketHandler<RemoveEntities>,
    IPacketHandler<PlayerInfoUpdate>,
    IPacketHandler<PlayerInfoRemove>,
    IPacketHandler<SetEntityData>
{
    public Task HandleAsync(LoginPlay packet)
    {
        entities.Reset(packet.EntityId);
        entities.ClearPlayers();

        logger.LogDebug("Playing as entity {EntityId}", packet.EntityId);

        return Task.CompletedTask;
    }

    public Task HandleAsync(Respawn packet)
    {
        // The server sends everything that is still around again after this.
        entities.Reset();

        return Task.CompletedTask;
    }

    public Task HandleAsync(AddEntity packet)
        => Spawned(new Entity(
            packet.EntityId,
            packet.Uuid,
            packet.Type,
            new Vector3d(packet.X, packet.Y, packet.Z),
            packet.Yaw,
            packet.Pitch,
            packet.HeadYaw,
            EntityManager.ToVelocity(packet.VelocityX, packet.VelocityY, packet.VelocityZ),
            OnGround: false));

    public Task HandleAsync(AddExperienceOrb packet)
        => Spawned(new Entity(
            packet.EntityId,
            Guid.Empty,
            EntityType.ExperienceOrb,
            new Vector3d(packet.X, packet.Y, packet.Z),
            0, 0, 0,
            Vector3d.Zero,
            OnGround: false));

    public Task HandleAsync(MoveEntityPosition packet)
    {
        entities.MoveBy(packet.EntityId, packet.DeltaX, packet.DeltaY, packet.DeltaZ, packet.OnGround);

        return Task.CompletedTask;
    }

    public Task HandleAsync(MoveEntityPositionAndRotation packet)
    {
        entities.MoveBy(packet.EntityId, packet.DeltaX, packet.DeltaY, packet.DeltaZ, packet.OnGround);
        entities.Turn(packet.EntityId, packet.Yaw, packet.Pitch, packet.OnGround);

        return Task.CompletedTask;
    }

    public Task HandleAsync(MoveEntityRotation packet)
    {
        entities.Turn(packet.EntityId, packet.Yaw, packet.Pitch, packet.OnGround);

        return Task.CompletedTask;
    }

    public Task HandleAsync(TeleportEntity packet)
    {
        entities.MoveTo(packet.EntityId, new Vector3d(packet.X, packet.Y, packet.Z), packet.Yaw, packet.Pitch, packet.OnGround);

        return Task.CompletedTask;
    }

    public Task HandleAsync(SetEntityMotion packet)
    {
        entities.SetVelocity(packet.EntityId, packet.VelocityX, packet.VelocityY, packet.VelocityZ);

        return Task.CompletedTask;
    }

    public Task HandleAsync(RotateHead packet)
    {
        entities.TurnHead(packet.EntityId, packet.HeadYaw);

        return Task.CompletedTask;
    }

    public async Task HandleAsync(RemoveEntities packet)
    {
        foreach (var id in packet.EntityIds)
        {
            if (entities.Remove(id) is not { } entity)
                continue;

            logger.LogDebug("Entity {EntityId} ({Type}) is gone", entity.Id, entity.Type);

            await eventBus.PublishAsync(new EntityRemovedEvent(entity));
        }
    }

    public Task HandleAsync(PlayerInfoUpdate packet)
    {
        foreach (var entry in packet.Entries)
        {
            entities.UpdatePlayer(entry.Uuid, entry.Name, entry.GameMode, entry.Listed, entry.Latency);

            if (entry.Name is not null)
                logger.LogDebug("Player {Name} is online", entry.Name);
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(PlayerInfoRemove packet)
    {
        foreach (var uuid in packet.Uuids)
            entities.RemovePlayer(uuid);

        return Task.CompletedTask;
    }

    public Task HandleAsync(SetEntityData packet)
    {
        entities.SetData(packet.EntityId, packet.Values);

        if (!packet.Complete)
            logger.LogTrace("Read {Count} metadata values of entity {EntityId} before one that cannot be read", packet.Values.Length, packet.EntityId);

        return Task.CompletedTask;
    }

    private async Task Spawned(Entity entity)
    {
        if (entities.Add(entity) is null)
            return;

        logger.LogDebug("Entity {EntityId} ({Type}) appeared at {Position}", entity.Id, entity.Type, entity.Position);

        await eventBus.PublishAsync(new EntitySpawnedEvent(entity));
    }
}
