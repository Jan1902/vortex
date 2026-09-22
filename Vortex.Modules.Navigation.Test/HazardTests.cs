using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// What the search refuses to walk the player into.
/// </summary>
public class HazardTests
{
    [Fact]
    public void GoesRoundLavaRatherThanThroughIt()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 12, -8, 8)
            .WithPool(Block.Lava, y: 64, fromX: 3, toX: 3, fromZ: -2, toZ: 2);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(6, 64, 0));

        Assert.NotNull(route);

        // Lava does not stop the player moving, so nothing but the hazard check
        // keeps this route out of it.
        Assert.DoesNotContain(route.Positions, p => p.X == 3 && p.Z is >= -2 and <= 2);
    }

    [Fact]
    public void WillNotStandOnMagma()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 12, -8, 8)
            .WithPool(Block.MagmaBlock, y: 63, fromX: 3, toX: 3, fromZ: -2, toZ: 2);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(6, 64, 0));

        Assert.NotNull(route);

        // The floor itself is what burns here, not the space above it.
        Assert.DoesNotContain(route.Positions, p => p.X == 3 && p.Z is >= -2 and <= 2);
    }

    [Fact]
    public void WillNotWalkThroughFireAtHeadHeight()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 12, -8, 8)
            .WithPool(Block.Fire, y: 65, fromX: 3, toX: 3, fromZ: -2, toZ: 2);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(6, 64, 0));

        Assert.NotNull(route);
        Assert.DoesNotContain(route.Positions, p => p.X == 3 && p.Z is >= -2 and <= 2);
    }

    [Fact]
    public void WadesThroughShallowWater()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 12, -8, 8)
            .WithPool(Block.Water, y: 64, fromX: 3, toX: 3, fromZ: -8, toZ: 8);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(6, 64, 0));

        Assert.NotNull(route);

        // Ankle deep and harmless, and the straight way through is still the
        // cheapest. Refusing it would make the bot walk round every puddle.
        Assert.Contains(route.Positions, p => p.X == 3);
    }

    [Fact]
    public void WillNotWalkIntoWaterDeepEnoughToDrownIn()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 12, -8, 8)
            .WithPool(Block.Water, y: 64, fromX: 3, toX: 3, fromZ: -2, toZ: 2)
            .WithPool(Block.Water, y: 65, fromX: 3, toX: 3, fromZ: -2, toZ: 2);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(6, 64, 0));

        Assert.NotNull(route);

        // Swimming is not modelled: the bot would walk along the bottom until it
        // drowned.
        Assert.DoesNotContain(route.Positions, p => p.X == 3 && p.Z is >= -2 and <= 2);
    }

    [Fact]
    public void GivesUpWhenTheOnlyWayIsThroughLava()
    {
        var world = new FakeWorld()
            .WithFloor(63, -4, 12, -1, 1)
            .WithPool(Block.Lava, y: 64, fromX: 3, toX: 3, fromZ: -1, toZ: 1);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(6, 64, 0));

        // Better no route at all than one that ends in the lava.
        Assert.Null(route);
    }

    [Fact]
    public void RefusesAGoalThatWouldHurtToStandOn()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 12, -8, 8)
            .With(new Vector3i(6, 63, 0), Block.MagmaBlock);

        Assert.Null(Find(world, new Vector3i(0, 64, 0), new Vector3i(6, 64, 0)));
    }

    private static Route? Find(FakeWorld world, Vector3i start, Vector3i goal)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance).FindRoute(start, goal);
}
