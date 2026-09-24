using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Getting across water by swimming at the surface, and into it from above.
/// </summary>
public class WaterTests
{
    [Fact]
    public void SwimsAcrossALake()
    {
        var route = Find(Lake(), new Vector3i(0, 64, 0), new Vector3i(9, 64, 0));

        Assert.NotNull(route);
        Assert.Contains(route.Moves, move => move is Swim);
        Assert.Equal(new Vector3i(9, 64, 0), route.Destination);

        // At the surface all the way: feet in the top layer of water, head
        // above it.
        Assert.All(route.Moves.OfType<Swim>(), swim => Assert.Equal(63, swim.To.Y));
    }

    [Fact]
    public void ClimbsOutOntoTheBankWithAStepUp()
    {
        var route = Find(Lake(), new Vector3i(0, 64, 0), new Vector3i(9, 64, 0))!;

        // Out of the water at x = 5 onto the bank at x = 6, a block higher than
        // the feet of a swimmer.
        Assert.Contains(route.Moves, move => move is StepUp { To: { X: 6, Y: 64 } });
    }

    [Fact]
    public void DoesNotSwimWithoutWater()
    {
        // The same lake without water in it is a hole too wide to jump.
        var world = new FakeWorld()
            .WithFloor(63, -5, 0, -3, 3)
            .WithFloor(63, 6, 12, -3, 3);

        Assert.Null(Find(world, new Vector3i(0, 64, 0), new Vector3i(9, 64, 0)));
    }

    [Fact]
    public void FallsAnyDistanceIntoWater()
    {
        // A cliff twenty blocks above a pool. Onto rock that would kill; into
        // water it does not hurt at all.
        var world = new FakeWorld()
            .WithFloor(83, -3, 0, -3, 3)
            .WithFloor(60, 1, 8, -3, 3)
            .WithPool(Block.Water, 61, 1, 8, -3, 3)
            .WithPool(Block.Water, 62, 1, 8, -3, 3)
            .WithPool(Block.Water, 63, 1, 8, -3, 3);

        var route = Find(world, new Vector3i(0, 84, 0), new Vector3i(4, 63, 0));

        Assert.NotNull(route);
        Assert.Contains(route.Moves, move => move is Drop { Height: > 3 });
    }

    [Fact]
    public void StillWillNotFallThatFarOntoRock()
    {
        var world = new FakeWorld()
            .WithFloor(83, -3, 0, -3, 3)
            .WithFloor(63, 1, 8, -3, 3);

        Assert.Null(Find(world, new Vector3i(0, 84, 0), new Vector3i(4, 64, 0)));
    }

    [Fact]
    public void StartsSwimmingFromUnderTheSurface()
    {
        // Sunk to the bottom of the lake: the way out starts at the surface.
        var route = new AStarPathfinder(Lake(), NullLogger<AStarPathfinder>.Instance)
            .FindRoute(new Vector3d(3.5, 61, 0.5), new Vector3i(9, 64, 0), MovementCapabilities.Athletic);

        Assert.NotNull(route);
        Assert.Equal(new Vector3i(3, 63, 0), route.Origin);
    }

    /// <summary>
    /// Banks at x = 0 and below, and x = 6 and above, with a lake three deep
    /// between them. Its surface is level with the tops of the banks, so a
    /// swimmer's feet are a block below someone standing on them.
    /// </summary>
    private static FakeWorld Lake()
        => new FakeWorld()
            .WithFloor(63, -5, 0, -3, 3)
            .WithFloor(63, 6, 12, -3, 3)
            .WithFloor(60, 1, 5, -3, 3)
            .WithPool(Block.Water, 61, 1, 5, -3, 3)
            .WithPool(Block.Water, 62, 1, 5, -3, 3)
            .WithPool(Block.Water, 63, 1, 5, -3, 3);

    private static Route? Find(FakeWorld world, Vector3i start, Vector3i goal)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance).FindRoute(start, goal, MovementCapabilities.Athletic);
}
