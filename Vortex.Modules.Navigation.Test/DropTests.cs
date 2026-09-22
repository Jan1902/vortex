using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Which drops the search is willing to plan.
/// </summary>
/// <remarks>
/// The movement lets go before the edge and brakes in the air, so it lands on
/// the block it aims at. Whatever lies past that block does not matter.
/// </remarks>
public class DropTests
{
    private static readonly Vector3i _start = new(0, 64, 0);
    private static readonly Vector3i _pillarTop = new(5, 62, 0);

    [Fact]
    public void DropsOntoAPillarWithNothingBeyondIt()
    {
        // One block of ground two below, and past it nothing at all -- the way
        // down from the top of a jump and run.
        var route = Find(Ledge());

        Assert.NotNull(route);
        Assert.Contains(route.Moves, move => move is Drop);
    }

    [Fact]
    public void DropsWhereTheGroundCarriesOn()
    {
        var route = Find(Ledge().WithFloor(61, 6, 8, -1, 1));

        Assert.NotNull(route);
        Assert.Contains(route.Moves, move => move is Drop);
    }

    [Fact]
    public void DropsAgainstAWallThatWouldStopIt()
    {
        // A wall past the landing: overshooting is not possible, so the landing
        // is exactly where it was aimed.
        var route = Find(Ledge().WithWall(x: 6, y: 62, fromZ: -1, toZ: 1));

        Assert.NotNull(route);
        Assert.Contains(route.Moves, move => move is Drop);
    }

    /// <summary>A ledge, and a single row of blocks two lower just past it.</summary>
    private static FakeWorld Ledge()
        => new FakeWorld()
            .WithFloor(63, -4, 4, -1, 1)
            .WithFloor(61, 5, 5, -1, 1);

    private static Route? Find(FakeWorld world)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRoute(_start, _pillarTop, MovementCapabilities.Walking);
}
