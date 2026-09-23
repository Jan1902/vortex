using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Routes that go through blocks rather than round them, which only happens
/// when the bot is told it may dig.
/// </summary>
public class DiggingTests
{
    private static readonly Vector3i _start = new(0, 64, 0);

    [Fact]
    public void GetsPastAWallWhenItMayDig()
    {
        var route = Find(Walled(), new(5, 64, 0), MovementCapabilities.Digging);

        Assert.NotNull(route);
        Assert.Contains(route.Moves, move => move is MineThrough);
        Assert.Equal(new Vector3i(5, 64, 0), route.Moves[^1].To);
    }

    [Fact]
    public void TakesTheTopOffAWallRatherThanBreakThroughIt()
    {
        // Two blocks high: one block out of the top is less work than two out
        // of the middle, so it goes over rather than through.
        var route = Find(Walled(), new(5, 64, 0), MovementCapabilities.Digging);

        var through = Assert.Single(route!.Moves.OfType<MineThrough>());

        Assert.Equal([new Vector3i(3, 65, 0)], through.Blocking);
        Assert.Equal(new Vector3i(3, 65, 0), through.To);
    }

    [Fact]
    public void BreaksThroughAWallItCannotGetOverInstead()
    {
        // The same wall, carried on above head height.
        var world = Walled().WithWall(x: 3, y: 66, fromZ: -3, toZ: 3, height: 4);

        var route = Find(world, new(5, 64, 0), MovementCapabilities.Digging);

        Assert.NotNull(route);

        var through = route.Moves.OfType<MineThrough>().First();

        // Body height and the block above it, in that order or the other.
        Assert.Equal(2, through.Blocking.Count);
        Assert.Contains(new Vector3i(3, 64, 0), through.Blocking);
        Assert.Contains(new Vector3i(3, 65, 0), through.Blocking);
    }

    [Fact]
    public void GoesRoundRatherThanThroughWhereItCan()
    {
        // The wall has a doorway one block along; walking round beats digging.
        var world = new FakeWorld()
            .WithFloor(63, -3, 8, -3, 3)
            .WithWall(x: 3, y: 64, fromZ: -3, toZ: 0)
            .WithWall(x: 3, y: 64, fromZ: 2, toZ: 3);

        var route = Find(world, new(5, 64, 0), MovementCapabilities.Digging);

        Assert.NotNull(route);
        Assert.DoesNotContain(route.Moves, move => move is MineThrough);
    }

    [Fact]
    public void FindsNoWayThroughWhenItMayNotDig()
        => Assert.Null(Find(Walled(), new(5, 64, 0), MovementCapabilities.Athletic));

    [Fact]
    public void DigsItsWayDownToSomethingBuriedBelow()
    {
        // Ore under solid rock: the only way within reach of it is downwards.
        var world = new FakeWorld();

        for (var y = 56; y <= 63; y++)
            world.WithFloor(y, -2, 2, -2, 2);

        var route = new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRouteWithinReach(new Vector3d(0.5, 64, 0.5), new Vector3i(0, 58, 0), 3.5, MovementCapabilities.Digging);

        Assert.NotNull(route);
        Assert.Contains(route.Moves, move => move is MineThrough through && through.Blocking.Contains(new Vector3i(0, 63, 0)));
    }

    [Fact]
    public void TunnelsALongWayThroughAMountain()
    {
        // Twenty blocks of rock in every direction. Priced as a walk, the
        // estimate of what is left says nothing about where to look, and the
        // search runs out of budget spreading through the rock instead.
        var world = new FakeWorld();

        for (var y = 50; y <= 80; y++)
            world.WithFloor(y, -30, 30, -30, 30);

        foreach (var y in new[] { 60, 61 })
        {
            world.With(new Vector3i(0, y, 0), Block.Air);
            world.With(new Vector3i(20, y, 0), Block.Air);
        }

        var route = new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRoute(new Vector3i(0, 60, 0), new Vector3i(20, 60, 0), MovementCapabilities.Digging);

        Assert.NotNull(route);
        Assert.Equal(new Vector3i(20, 60, 0), route.Moves[^1].To);
    }

    [Fact]
    public void StillWalksRoundAHillRatherThanThroughIt()
    {
        // Going round is a good deal longer here, and still the better deal.
        var world = new FakeWorld().WithFloor(63, -60, 60, -60, 60);

        for (var x = 10; x <= 14; x++)
            for (var y = 64; y <= 67; y++)
                world.WithFloor(y, x, x, -20, 20);

        var route = new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRoute(new Vector3i(0, 64, 0), new Vector3i(40, 64, 10), MovementCapabilities.Digging);

        Assert.NotNull(route);
        Assert.DoesNotContain(route.Moves, move => move is MineThrough);
    }

    [Fact]
    public void LeavesBlocksNextToWaterAlone()
    {
        // Water sits on top of the wall: digging through lets it in, and it
        // does not stop coming.
        var world = Walled().WithPool(Block.Water, 66, 3, 3, -3, 3);

        Assert.Null(Find(world, new(5, 64, 0), MovementCapabilities.Digging));
    }

    /// <summary>Floor with a wall across it at x = 3, two blocks high.</summary>
    private static FakeWorld Walled()
        => new FakeWorld()
            .WithFloor(63, -3, 8, -3, 3)
            .WithWall(x: 3, y: 64, fromZ: -3, toZ: 3);

    private static Route? Find(FakeWorld world, Vector3i goal, MovementCapabilities capabilities)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance).FindRoute(_start, goal, capabilities);
}
