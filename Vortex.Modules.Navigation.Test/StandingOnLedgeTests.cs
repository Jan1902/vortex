using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Working out where the player is standing when it is not squarely on one
/// block.
/// </summary>
/// <remarks>
/// The player is 0.6 wide, so at the lip of a drop its feet are still on the
/// ledge while its middle is already over the edge. Reading that as "in mid-air
/// above the floor below" put the search three blocks too low, and every route
/// planned from there was wrong -- including one that came out empty because
/// the floor below happened to be the goal, which spun the task at full speed.
/// </remarks>
public class StandingOnLedgeTests
{
    /// <summary>Ground to x = 10, then a three block drop from x = 11.</summary>
    private static FakeWorld Ledge()
        => new FakeWorld()
            .WithFloor(63, -8, 10, -8, 8)
            .WithFloor(60, 11, 30, -8, 8);

    [Fact]
    public void PlansFromTheLedgeWhileHangingOverTheEdge()
    {
        // Middle just past the edge at x = 11.1, feet still on block 10.
        var route = Find(Ledge(), new Vector3d(11.1, 64, 0.5), new Vector3i(5, 64, 0));

        Assert.NotNull(route);
        Assert.Equal(new Vector3i(10, 64, 0), route.Origin);

        // Back along the ledge, not down the drop and up again.
        Assert.All(route.Moves, move => Assert.IsType<Walk>(move));
    }

    [Fact]
    public void DoesNotMistakeTheLedgeForMidAir()
    {
        var onLedge = Find(Ledge(), new Vector3d(11.1, 64, 0.5), new Vector3i(20, 61, 0));

        Assert.NotNull(onLedge);

        // It really is still up top, so getting down is part of the route.
        Assert.Contains(onLedge.Moves, move => move is Drop);
    }

    [Fact]
    public void TreatsAPlayerWithNothingUnderItAsFalling()
    {
        // Clear of the ledge entirely, over the drop.
        var route = Find(Ledge(), new Vector3d(12.5, 64, 0.5), new Vector3i(20, 61, 0));

        Assert.NotNull(route);

        // Planned from the floor it is about to land on.
        Assert.Equal(new Vector3i(12, 61, 0), route.Origin);
        Assert.All(route.Moves, move => Assert.IsType<Walk>(move));
    }

    [Fact]
    public void DoesNotHandBackAnEmptyRouteForTheBlockBelowTheLedge()
    {
        // This is the parkour case: standing on the ledge at x = 10 with the
        // middle over the edge, asked to reach the block three down. Resolving
        // straight down made the start equal the goal, so the route came back
        // empty and the task had nothing to do but ask again immediately.
        var route = Find(Ledge(), new Vector3d(11.1, 64, 0.5), new Vector3i(11, 61, 0));

        Assert.NotNull(route);
        Assert.NotEmpty(route.Moves);
        Assert.Equal(new Vector3i(10, 64, 0), route.Origin);
        Assert.Equal(new Vector3i(11, 61, 0), route.Destination);
    }

    [Fact]
    public void StandingSquarelyOnABlockNeedsNoWorkingOut()
    {
        var route = Find(Ledge(), new Vector3d(5.5, 64, 0.5), new Vector3i(9, 64, 0));

        Assert.NotNull(route);
        Assert.Equal(new Vector3i(5, 64, 0), route.Origin);
    }

    [Fact]
    public void KnowsItHasArrivedWhenItIsOnTheGoalBlock()
    {
        var route = Find(Ledge(), new Vector3d(11.1, 64, 0.5), new Vector3i(10, 64, 0));

        Assert.NotNull(route);
        Assert.Empty(route.Moves);
        Assert.Equal(new Vector3i(10, 64, 0), route.Origin);
    }

    private static Route? Find(FakeWorld world, Vector3d start, Vector3i goal)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance).FindRoute(start, goal);
}
