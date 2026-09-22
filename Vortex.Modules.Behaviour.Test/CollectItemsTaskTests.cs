using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Behaviour.Tasks.Items;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Test;

public class CollectItemsTaskTests
{
    private readonly FakePlayer _player = new();
    private readonly FakeEntities _entities = new();
    private readonly FakeInventory _inventory = new();

    [Fact]
    public void IsDoneWhenNothingLiesAround()
        => Assert.True(Task().IsSatisfied());

    [Fact]
    public void WalksToTheNearestItemFirst()
    {
        _entities.Drop(10, Item.Cobblestone, 1, new Vector3d(6.5, 64, 0.5));
        _entities.Drop(11, Item.Diamond, 1, new Vector3d(2.5, 64, 0.5));

        var task = Task();

        Assert.False(task.IsSatisfied());
        Assert.Equal("stand at 2 64 0", Assert.Single(task.Dependencies()).Description);
    }

    [Fact]
    public void LeavesWhatDoesNotFit()
    {
        _entities.Drop(10, Item.Cobblestone, 1, new Vector3d(1.5, 64, 0.5));
        _inventory.Full.Add(Item.Cobblestone);

        Assert.True(Task().IsSatisfied());
    }

    [Fact]
    public void LeavesWhatLiesOutsideTheArea()
    {
        _entities.Drop(10, Item.Cobblestone, 1, new Vector3d(30.5, 64, 0.5));

        Assert.True(Task().IsSatisfied());
    }

    private CollectItemsTask Task()
        => new(
            new Vector3d(0, 64, 0),
            radius: 16,
            _entities,
            _inventory,
            _player,
            (target, capabilities) => new GoToTask(target, _player, null!, null!, capabilities, NullLogger<GoToTask>.Instance),
            NullLogger<CollectItemsTask>.Instance);
}
