using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Searches that run out of budget before they reach the goal.
/// </summary>
/// <remarks>
/// Budgeted in positions here rather than time, so that what the search gets
/// to see is the same on every machine.
/// </remarks>
public class TimeBudgetTests
{
    private static readonly Vector3i _start = new(0, 64, 0);

    [Fact]
    public void HandsBackPartOfTheWayWhenItRunsOutOfBudget()
    {
        var route = Find(Mountains(), new Vector3i(150, 64, 0), budget: 600);

        Assert.NotNull(route);
        Assert.True(route.Truncated);
        Assert.False(route.ReachesGoal);

        // Part of the way is still a way: it starts where the player is, and
        // gets it somewhere worth going.
        Assert.Equal(_start, route.Origin);
        Assert.True(route.Destination!.X > 5, $"only got to {route.Destination}");
    }

    [Fact]
    public void FindsTheWholeWayWhenTheBudgetAllowsIt()
    {
        var route = Find(Mountains(), new Vector3i(150, 64, 0), budget: 1_000_000);

        Assert.NotNull(route);
        Assert.False(route.Truncated);
        Assert.True(route.ReachesGoal);
        Assert.Equal(new Vector3i(150, 64, 0), route.Destination);
    }

    [Fact]
    public void CarryingOnFromWherePartOfTheWayEndsGetsThere()
    {
        var world = Mountains();
        var goal = new Vector3i(150, 64, 0);
        var at = _start;

        // Each part ends where the next search starts, as when the bot walks
        // one and searches on from its end.
        for (var part = 0; part < 50 && at != goal; part++)
        {
            var route = Find(world, goal, budget: 600, from: at);

            Assert.NotNull(route);

            at = route.Destination ?? at;
        }

        Assert.Equal(goal, at);
    }

    [Fact]
    public void StillSaysThereIsNoWayWhenThereIsNone()
    {
        // Walled in: the search runs dry rather than out of budget, and part of
        // a way to nowhere is no use to anyone.
        var world = new FakeWorld()
            .WithFloor(63, -8, 8, -8, 8)
            .WithWall(x: 2, y: 64, fromZ: -1, toZ: 1)
            .WithWall(x: 4, y: 64, fromZ: -1, toZ: 1)
            .WithWall(x: 3, y: 64, fromZ: -1, toZ: -1)
            .WithWall(x: 3, y: 64, fromZ: 1, toZ: 1);

        Assert.Null(Find(world, new Vector3i(3, 64, 0), budget: 1_000_000));
    }

    [Fact]
    public void HandsBackNothingThatDoesNotGetAnywhere()
    {
        // A budget so small that nothing it reaches is far enough to walk to.
        var route = Find(Mountains(), new Vector3i(150, 64, 0), budget: 1);

        Assert.Null(route);
    }

    /// <summary>
    /// A long strip with ridges across it every few blocks, each with a single
    /// gap to wind through, so that the way there takes a good deal of looking.
    /// </summary>
    private static FakeWorld Mountains()
    {
        var world = new FakeWorld().WithFloor(63, -5, 155, -20, 20);

        for (var x = 10; x <= 140; x += 10)
        {
            // The gap moves from one side to the other and back.
            var gap = x % 20 == 0 ? 15 : -15;

            world.WithWall(x, 64, -20, gap - 1, height: 4);
            world.WithWall(x, 64, gap + 1, 20, height: 4);
        }

        return world;
    }

    private static Route? Find(FakeWorld world, Vector3i goal, int budget, Vector3i? from = null)
    {
        var options = new PathfinderOptions(TimeSpan.MaxValue, TimeSpan.MaxValue, budget);

        return new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance, options)
            .FindRoute(from ?? _start, goal, MovementCapabilities.Athletic);
    }
}
