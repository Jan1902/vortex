using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Heading for somewhere the client has not been sent yet.
/// </summary>
/// <remarks>
/// The world arrives a chunk at a time as the player moves, so anything past
/// the view distance reads as solid. Treating that as "no way there" is what
/// stopped the bot from ever walking further than it could already see.
/// </remarks>
public class UnknownGoalTests
{
    private static readonly Vector3i _start = new(0, 64, 0);

    [Fact]
    public void HeadsTowardsAGoalItCannotSeeYet()
    {
        // Ground for eighty blocks, then nothing loaded.
        var world = new FakeWorld().WithFloor(63, -8, 80, -8, 8);

        var route = Find(world, new Vector3i(200, 64, 0));

        Assert.NotNull(route);
        Assert.False(route.ReachesGoal);

        // As far as it can see, in the right direction.
        Assert.Equal(new Vector3i(80, 64, 0), route.Destination);
    }

    [Fact]
    public void SaysSoWhenItDoesReachTheGoal()
    {
        var world = new FakeWorld().WithFloor(63, -8, 80, -8, 8);

        var route = Find(world, new Vector3i(40, 64, 0));

        Assert.NotNull(route);
        Assert.True(route.ReachesGoal);
        Assert.Equal(new Vector3i(40, 64, 0), route.Destination);
    }

    [Fact]
    public void StillRefusesAGoalItCanSeeAndCannotReach()
    {
        // Loaded, and walled in on every side: this one really is hopeless, and
        // saying so is better than walking to the wall to find out.
        var world = new FakeWorld()
            .WithFloor(63, -8, 20, -8, 8)
            .WithWall(x: 8, y: 64, fromZ: -8, toZ: 8);

        Assert.Null(Find(world, new Vector3i(12, 64, 0)));
    }

    [Fact]
    public void GivesUpWhenNothingAtAllIsKnownThatWay()
    {
        // One block of ground and nothing else: there is nowhere to head for
        // that is not already where the player is standing.
        var world = new FakeWorld().WithFloor(63, 0, 0, 0, 0);

        Assert.Null(Find(world, new Vector3i(200, 64, 0)));
    }

    [Fact]
    public void FindsGroundAtADifferentHeightOnTheWay()
    {
        // The staging point does not have to be at the player's own level --
        // only somewhere it can actually climb to.
        var world = new FakeWorld()
            .WithFloor(63, -8, 20, -8, 8)
            .WithFloor(64, 21, 40, -8, 8);

        var route = Find(world, new Vector3i(200, 64, 0));

        Assert.NotNull(route);
        Assert.False(route.ReachesGoal);
        Assert.Equal(new Vector3i(40, 65, 0), route.Destination);
    }

    [Fact]
    public void WalksTheStagingRouteRatherThanStandingStill()
    {
        var world = new FakeWorld().WithFloor(63, -8, 80, -8, 8);

        var route = Find(world, new Vector3i(200, 64, 0));

        Assert.NotNull(route);

        // Eighty blocks of actual progress, not an empty route that leaves the
        // bot where it started.
        Assert.Equal(80, route.Moves.Count);
        Assert.All(route.Moves, move => Assert.IsType<Walk>(move));
    }

    [Fact]
    public void PlansFromTheGroundWhenTheGivenBlockIsOverAnEdge()
    {
        // Ground to x = 10, then a three block drop. The player's centre is
        // already over the edge at x = 11 while its feet are still on the ledge.
        var world = new FakeWorld()
            .WithFloor(63, -8, 10, -8, 8)
            .WithFloor(60, 11, 30, -8, 8);

        var route = new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRoute(new Vector3i(11, 64, 0), new Vector3i(20, 61, 0));

        Assert.NotNull(route);

        // Straight on along the lower floor, not back up onto the ledge, which
        // is what planning from mid-air used to produce.
        Assert.All(route.Moves, move => Assert.IsType<Walk>(move));
        Assert.Equal(9, route.Moves.Count);
    }

    private static Route? Find(FakeWorld world, Vector3i goal)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance).FindRoute(_start, goal);
}
