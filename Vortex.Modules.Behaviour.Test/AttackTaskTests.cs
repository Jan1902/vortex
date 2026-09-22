using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Behaviour.Tasks.Entities;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Test;

public class AttackTaskTests
{
    private readonly FakePlayer _player = new() { Position = new Vector3d(0.5, 64, 0.5) };
    private readonly FakeEntities _entities = new();
    private readonly FakeInventory _inventory = new();
    private readonly FakeInteraction _interaction = new();

    [Fact]
    public void ClosesInOnAnEntityOutOfReach()
    {
        Spawn(new Vector3d(10.5, 64, 0.5));

        Assert.Equal("stand at 10 64 0", Assert.Single(Task().Dependencies()).Description);
    }

    [Fact]
    public async Task HitsWithTheBestWeaponCarried()
    {
        Spawn(new Vector3d(2, 64, 0.5));
        _inventory.Hotbar(0, Item.Dirt);
        _inventory.Put(PlayerSlots.MainStart, Item.StoneSword);
        _inventory.Put(PlayerSlots.MainStart + 1, Item.IronSword);

        var task = Task();

        Assert.Empty(task.Dependencies());
        await task.ExecuteAsync(CancellationToken.None);

        Assert.Equal([7], _interaction.Attacks);
        Assert.Equal(Item.IronSword, _inventory.HeldItem?.Item);
    }

    [Fact]
    public void IsDoneOnceTheEntityIsGone()
    {
        var zombie = Spawn(new Vector3d(2, 64, 0.5));
        var task = Task();

        Assert.False(task.IsSatisfied());

        _entities.Tracked.Remove(zombie);

        Assert.True(task.IsSatisfied());
    }

    [Fact]
    public void IsDoneOnceTheEntityHasNoHealthLeft()
    {
        var zombie = Spawn(new Vector3d(2, 64, 0.5));
        _entities.Tracked.Remove(zombie);
        _entities.Tracked.Add(zombie with
        {
            Metadata = zombie.Metadata.SetItem(EntityDataKeys.IndexOf(EntityType.Zombie, EntityDataKey.Health), 0f),
        });

        Assert.True(Task().IsSatisfied());
    }

    private Entity Spawn(Vector3d position)
    {
        var zombie = new Entity(7, Guid.NewGuid(), EntityType.Zombie, position, 0, 0, 0, Vector3d.Zero, OnGround: true);
        _entities.Tracked.Add(zombie);

        return zombie;
    }

    private AttackTask Task()
        => new(
            7,
            _entities,
            _inventory,
            _player,
            _interaction,
            (target, capabilities) => new GoToTask(target, _player, null!, null!, capabilities, NullLogger<GoToTask>.Instance),
            NullLogger<AttackTask>.Instance);
}
