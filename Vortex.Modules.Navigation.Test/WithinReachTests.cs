using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Getting within reach of a block rather than onto one.
/// </summary>
public class WithinReachTests
{
    private const double Reach = 3.5;

    [Fact]
    public void StaysWhereItIsWhenTheBlockIsAlreadyInReach()
    {
        var route = Find(new FakeWorld().WithFloor(63, -5, 5, -5, 5), new(0, 64, 0), new(2, 64, 0));

        Assert.NotNull(route);
        Assert.Empty(route.Moves);
    }

    [Fact]
    public void StopsAsSoonAsTheBlockIsInReach()
    {
        var route = Find(new FakeWorld().WithFloor(63, -2, 20, -2, 2), new(0, 64, 0), new(12, 64, 0));

        Assert.NotNull(route);

        var end = route.Moves[^1].To;

        Assert.InRange(12 - end.X, 1, 3);
    }

    [Fact]
    public void GoesRoundAWallToTheSideTheBlockCanBeReachedFrom()
    {
        // A wall too high to climb between the player and the block, which is
        // out of reach from anywhere on this side of it.
        var world = new FakeWorld()
            .WithFloor(63, -8, 10, -8, 8)
            .WithWall(x: 3, y: 64, fromZ: -6, toZ: 6, height: 3)
            .WithBlock(new(7, 65, 0));

        var route = Find(world, new(0, 64, 0), new(7, 65, 0));

        Assert.NotNull(route);

        var end = route.Moves[^1].To;

        Assert.True(end.X > 3, $"ended at {end}, still on the near side of the wall");
    }

    [Fact]
    public void DoesNotStandInTheBlockItIsReachingFor()
    {
        // Placing a block where the player would otherwise stand.
        var route = Find(new FakeWorld().WithFloor(63, -5, 5, -5, 5), new(2, 64, 0), new(2, 64, 0));

        Assert.NotNull(route);
        Assert.NotEmpty(route.Moves);
        Assert.NotEqual(new Vector3i(2, 64, 0), route.Moves[^1].To);
    }

    [Fact]
    public void FindsNothingWhereNoPlaceIsCloseEnough()
    {
        // An island far out of reach of the only ground there is.
        var world = new FakeWorld()
            .WithFloor(63, -3, 3, -3, 3)
            .WithFloor(63, 12, 12, 0, 0);

        Assert.Null(Find(world, new(0, 64, 0), new(12, 64, 0)));
    }

    private static Route? Find(FakeWorld world, Vector3i start, Vector3i target)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRouteWithinReach(new Vector3d(start.X + 0.5, start.Y, start.Z + 0.5), target, Reach, MovementCapabilities.Athletic);
}
