using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Entities.Test;

public class EntityTrackingTests
{
    private const int SelfId = 7;
    private const int ZombieId = 42;

    private static readonly Guid ZombieUuid = Guid.NewGuid();

    private readonly EntityManager _entities = new();
    private readonly RecordingEventBus _events = new();
    private readonly EntityPacketHandler _handler;

    public EntityTrackingTests()
    {
        _handler = new EntityPacketHandler(NullLogger<EntityPacketHandler>.Instance, _events, _entities);
        _handler.HandleAsync(new LoginPlay(SelfId, IsHardcore: false)).Wait();
    }

    [Fact]
    public async Task TracksSpawnedEntities()
    {
        await SpawnZombie();

        var zombie = _entities.Get(ZombieId);

        Assert.NotNull(zombie);
        Assert.Equal(EntityType.Zombie, zombie.Type);
        Assert.Equal(new Vector3d(10.5, 64, -3.5), zombie.Position);
        Assert.Equal(90f, zombie.Yaw);
        Assert.Same(zombie, _entities.Get(ZombieUuid));
        Assert.Equal(zombie, Assert.IsType<EntitySpawnedEvent>(Assert.Single(_events.Events)).Entity);
    }

    [Fact]
    public async Task AddsUpMovementDeltas()
    {
        await SpawnZombie();

        // One block east, half a block up, in 1/4096 steps.
        await _handler.HandleAsync(new MoveEntityPosition(ZombieId, 4096, 2048, 0, OnGround: false));
        await _handler.HandleAsync(new MoveEntityPositionAndRotation(ZombieId, -1024, 0, 4096, Yaw: 180f, Pitch: 0f, OnGround: true));

        var zombie = _entities.Get(ZombieId)!;

        Assert.Equal(new Vector3d(11.25, 64.5, -2.5), zombie.Position);
        Assert.Equal(180f, zombie.Yaw);
        Assert.True(zombie.OnGround);
    }

    [Fact]
    public async Task TeleportsReplaceThePosition()
    {
        await SpawnZombie();

        await _handler.HandleAsync(new TeleportEntity(ZombieId, 100, 70, 100, Yaw: 45f, Pitch: 10f, OnGround: true));

        Assert.Equal(new Vector3d(100, 70, 100), _entities.Get(ZombieId)!.Position);
    }

    [Fact]
    public async Task ConvertsVelocityToBlocksPerTick()
    {
        await SpawnZombie();

        await _handler.HandleAsync(new SetEntityMotion(ZombieId, 8000, -4000, 0));

        Assert.Equal(new Vector3d(1, -0.5, 0), _entities.Get(ZombieId)!.Velocity);
    }

    [Fact]
    public async Task ForgetsRemovedEntities()
    {
        await SpawnZombie();

        await _handler.HandleAsync(new RemoveEntities([ZombieId, 999]));

        Assert.Null(_entities.Get(ZombieId));
        Assert.Null(_entities.Get(ZombieUuid));
        Assert.Equal(ZombieId, Assert.IsType<EntityRemovedEvent>(_events.Events.Last()).Entity.Id);
        Assert.Single(_events.Events.OfType<EntityRemovedEvent>());
    }

    [Fact]
    public async Task ForgetsEverythingOnRespawn()
    {
        await SpawnZombie();

        await _handler.HandleAsync(new Respawn());

        Assert.Empty(_entities.Entities);
        Assert.Equal(SelfId, _entities.SelfId);
    }

    [Fact]
    public async Task IgnoresUpdatesForUnknownEntities()
    {
        await _handler.HandleAsync(new MoveEntityPosition(123, 4096, 0, 0, OnGround: true));

        Assert.Empty(_entities.Entities);
    }

    [Fact]
    public async Task TracksExperienceOrbs()
    {
        await _handler.HandleAsync(new AddExperienceOrb(5, 1, 2, 3, Count: 7));

        Assert.Equal(EntityType.ExperienceOrb, _entities.Get(5)!.Type);
    }

    [Fact]
    public async Task FindsTheNearestEntity()
    {
        await SpawnZombie();
        await _handler.HandleAsync(new AddEntity(50, Guid.NewGuid(), EntityType.Cow, 0, 64, 0, 0, 0, 0, 0, 0, 0, 0));

        Assert.Equal(50, _entities.Nearest(new Vector3d(1, 64, 1))!.Id);
        Assert.Equal(ZombieId, _entities.Nearest(new Vector3d(1, 64, 1), e => e.Type == EntityType.Zombie)!.Id);
        Assert.Null(_entities.Nearest(Vector3d.Zero, e => e.Type == EntityType.Creeper));
    }

    [Fact]
    public async Task ReportsWhoPickedUpWhat()
    {
        await _handler.HandleAsync(new AddEntity(60, Guid.NewGuid(), EntityType.Item, 0, 64, 0, 0, 0, 0, 1, 0, 0, 0));

        await _handler.HandleAsync(new TakeItemEntity(60, SelfId, 3));

        var pickedUp = Assert.IsType<ItemPickedUpEvent>(_events.Events.Last());
        Assert.Equal(60, pickedUp.Item.Id);
        Assert.Equal(SelfId, pickedUp.CollectorId);
        Assert.Equal(3, pickedUp.Count);
    }

    private Task SpawnZombie()
        => _handler.HandleAsync(new AddEntity(ZombieId, ZombieUuid, EntityType.Zombie, 10.5, 64, -3.5, 0, 90f, 90f, 0, 0, 0, 0));
}
