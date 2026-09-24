using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Only acting on blocks that can be seen, never on one behind something else.
/// </summary>
public class LineOfSightTests
{
    private static readonly Vector3d _eyes = LineOfSight.Eyes(new Vector3d(0.5, 64, 0.5));

    [Fact]
    public void SeesABlockInTheOpen()
    {
        var solid = Solid(new Vector3i(3, 64, 0));

        Assert.NotNull(LineOfSight.Sight(_eyes, new(3, 64, 0), solid.Contains));
    }

    [Fact]
    public void DoesNotSeeABlockBehindAnother()
    {
        // Coal behind dirt, with the dirt against every side of it that faces
        // the eyes.
        var solid = Solid(new Vector3i(2, 64, 0), new Vector3i(1, 64, 0), new Vector3i(2, 65, 0), new Vector3i(1, 65, 0), new Vector3i(2, 64, 1), new Vector3i(2, 64, -1), new Vector3i(2, 63, 0));

        Assert.Null(LineOfSight.Sight(_eyes, new(2, 64, 0), solid.Contains));
    }

    [Fact]
    public void SeesTheTopOfABlockOverTheOneInFront()
    {
        // One block in front, but the target sticks up past it: its top is in
        // plain view even though its near side is not.
        var solid = Solid(new Vector3i(1, 64, 0), new Vector3i(2, 64, 0));

        var sight = LineOfSight.Sight(_eyes, new(2, 64, 0), solid.Contains);

        Assert.NotNull(sight);
        Assert.Equal(new Vector3i(0, 1, 0), sight.Value.Side);
    }

    [Fact]
    public void DoesNotSeeASideThatIsCoveredEvenFromStraightOn()
    {
        // Buried on every side: nothing of it to see at all.
        var target = new Vector3i(0, 62, 0);
        var solid = Solid(target, new Vector3i(0, 63, 0), new Vector3i(0, 61, 0), new Vector3i(1, 62, 0), new Vector3i(-1, 62, 0), new Vector3i(0, 62, 1), new Vector3i(0, 62, -1));

        Assert.Null(LineOfSight.Sight(_eyes, target, solid.Contains));
    }

    [Fact]
    public void DoesNotReachThroughTheWallOfAPocketForTheCoalBehindIt()
    {
        // Standing in a two-block pocket in the rock, with coal two blocks off
        // behind a block of dirt: close enough, but out of sight. Without
        // digging there is nowhere it can be seen from.
        var world = Pocket();

        Assert.Null(FindWithinReach(world, MovementCapabilities.Athletic));
    }

    [Fact]
    public void DigsToWhereItCanSeeTheCoal()
    {
        var world = Pocket();

        var route = FindWithinReach(world, MovementCapabilities.Digging with { Loadout = new Loadout([Item.IronPickaxe], 0) });

        Assert.NotNull(route);
        Assert.Contains(route.Moves, move => move is MineThrough through && through.Blocking.Contains(new Vector3i(1, 64, 0)));
    }

    [Fact]
    public void StaysPutWhenTheCoalIsInSightAlready()
    {
        // The way to it open, two blocks high: nothing between the eyes and
        // the side of the coal facing them.
        var world = Pocket().With(new Vector3i(1, 64, 0), Block.Air).With(new Vector3i(1, 65, 0), Block.Air);

        var route = FindWithinReach(world, MovementCapabilities.Athletic);

        Assert.NotNull(route);
        Assert.Empty(route.Moves);
    }

    [Fact]
    public void DigsDownToOreFarBelowWithoutSearchingTheWholeSurfaceFirst()
    {
        // Wide open ground on top of solid rock, with the ore fifteen blocks
        // down. Every place on the surface is as close to it sideways as the
        // one above it; only going down gets anywhere.
        var world = new FakeWorld();

        for (var y = 50; y <= 79; y++)
            world.WithFloor(y, -8, 8, -8, 8);

        world.WithFloor(79, -60, 60, -60, 60).With(new Vector3i(2, 64, 1), Block.CoalOre);

        var options = new PathfinderOptions(TimeSpan.MaxValue, TimeSpan.MaxValue, MaxExpandedPositions: 5_000);
        var route = new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance, options)
            .FindRouteWithinReach(new Vector3d(0.5, 80, 0.5), new Vector3i(2, 64, 1), 3.5, MovementCapabilities.Digging with { Loadout = new Loadout([Item.IronPickaxe], 0) });

        Assert.NotNull(route);
        Assert.False(route.Truncated, "ran out of budget before it got there");
        Assert.True(route.Destination!.Y < 70, $"ended at {route.Destination}");
    }

    private static HashSet<Vector3i> Solid(params Vector3i[] blocks)
        => [.. blocks];

    /// <summary>
    /// Solid rock with a pocket at x = 0 for the player to stand in, dirt at
    /// x = 1 and coal at x = 2, level with its feet.
    /// </summary>
    private static FakeWorld Pocket()
    {
        var world = new FakeWorld();

        for (var y = 60; y <= 68; y++)
            world.WithFloor(y, -4, 6, -4, 4);

        return world
            .With(new Vector3i(0, 64, 0), Block.Air)
            .With(new Vector3i(0, 65, 0), Block.Air)
            .With(new Vector3i(1, 64, 0), Block.Dirt)
            .With(new Vector3i(2, 64, 0), Block.CoalOre);
    }

    private static Route? FindWithinReach(FakeWorld world, MovementCapabilities capabilities)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRouteWithinReach(new Vector3d(0.5, 64, 0.5), new Vector3i(2, 64, 0), 3.5, capabilities);
}
