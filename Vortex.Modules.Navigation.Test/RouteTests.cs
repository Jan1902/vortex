using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

public class RouteTests
{
    private static readonly Vector3i _start = new(0, 64, 0);

    [Fact]
    public void RunsAStraightLineTogether()
    {
        var route = WalkLine(new Vector3i(1, 0, 0), 5);

        // Five blocks east is one walk, not five.
        Assert.Equal(new Vector3i(5, 64, 0), route.FurthestWalk(_start));
    }

    [Fact]
    public void StopsWhereTheRouteTurns()
    {
        var route = new Route([
            new Walk(new(1, 64, 0)),
            new Walk(new(2, 64, 0)),
            new Walk(new(3, 64, 0)),
            new Walk(new(3, 64, 1)),
            new Walk(new(3, 64, 2)),
        ]);

        Assert.Equal(new Vector3i(3, 64, 0), route.FurthestWalk(_start));
    }

    [Fact]
    public void StopsBeforeAStepUp()
    {
        var route = new Route([
            new Walk(new(1, 64, 0)),
            new Walk(new(2, 64, 0)),
            new StepUp(new(3, 65, 0)),
            new Walk(new(4, 65, 0)),
        ]);

        // The step up has to be walked into on its own so the jump fires
        // against it, rather than being swallowed into a longer walk.
        Assert.Equal(new Vector3i(2, 64, 0), route.FurthestWalk(_start));
    }

    [Fact]
    public void StopsBeforeADrop()
    {
        var route = new Route([
            new Walk(new(1, 64, 0)),
            new Walk(new(2, 64, 0)),
            new Drop(new(3, 61, 0), Height: 3),
            new Walk(new(4, 61, 0)),
        ]);

        Assert.Equal(new Vector3i(2, 64, 0), route.FurthestWalk(_start));
    }

    [Fact]
    public void HasNoWalkToRunWhenTheRouteStartsWithSomethingElse()
    {
        var route = new Route([
            new JumpGap(new(3, 64, 0), Distance: 2),
            new Walk(new(4, 64, 0)),
            new Walk(new(5, 64, 0)),
        ]);

        // A jump is its own movement. Nothing to compress here, and the caller
        // has to look at the move rather than being handed a block to walk to.
        Assert.Null(route.FurthestWalk(_start));
        Assert.IsType<JumpGap>(route.Next);
    }

    [Fact]
    public void ResumesTheLineAfterTheHeightChange()
    {
        var route = new Route([
            new Walk(new(1, 65, 0)),
            new Walk(new(2, 65, 0)),
            new Walk(new(3, 65, 0)),
        ]);

        // Once up there, the rest of the line runs together.
        Assert.Equal(new Vector3i(3, 65, 0), route.FurthestWalk(new Vector3i(0, 65, 0)));
    }

    [Fact]
    public void TakesTheFirstStepWhenTheRouteTurnsImmediately()
    {
        var route = new Route([
            new Walk(new(1, 64, 0)),
            new Walk(new(1, 64, 1)),
        ]);

        Assert.Equal(new Vector3i(1, 64, 0), route.FurthestWalk(_start));
    }

    [Fact]
    public void RunsTheWholeRouteWhenItIsOneLongLine()
    {
        var route = WalkLine(new Vector3i(0, 0, -1), 40);

        Assert.Equal(new Vector3i(0, 64, -40), route.FurthestWalk(_start));
    }

    [Fact]
    public void HasNothingToAimAtOnAnEmptyRoute()
    {
        var empty = new Route([]);

        Assert.Null(empty.FurthestWalk(_start));
        Assert.Null(empty.Next);
        Assert.Null(empty.Destination);
    }

    [Fact]
    public void FallsBackToTheFirstStepWhenItIsNotAdjacent()
    {
        // Should not happen -- a route is searched from where the player is --
        // but guessing a direction from a gap would run the bot off somewhere
        // the route never went.
        var route = WalkLine(new Vector3i(1, 0, 0), 5);

        Assert.Equal(new Vector3i(1, 64, 0), route.FurthestWalk(new Vector3i(-7, 64, 3)));
    }

    [Fact]
    public void NeverAimsPastTheEndOfTheRoute()
    {
        var route = WalkLine(new Vector3i(1, 0, 0), 3);

        Assert.Equal(route.Destination, route.FurthestWalk(_start));
    }

    [Fact]
    public void ReportsWhereItPassesWithoutSayingHow()
    {
        var route = new Route([
            new Walk(new(1, 64, 0)),
            new StepUp(new(2, 65, 0)),
            new Drop(new(3, 62, 0), Height: 3),
        ]);

        Assert.Equal(
            [new(1, 64, 0), new(2, 65, 0), new(3, 62, 0)],
            route.Positions);

        Assert.Equal(new Vector3i(3, 62, 0), route.Destination);
    }

    [Fact]
    public void CoversOpenGroundInASingleWalk()
    {
        var world = new FakeWorld().WithFloor(63, -8, 40, -8, 8);

        var start = new Vector3i(0, 64, 0);
        var route = new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRoute(start, new Vector3i(30, 64, 0));

        Assert.NotNull(route);
        Assert.Equal(30, route.Moves.Count);
        Assert.All(route.Moves, move => Assert.IsType<Walk>(move));

        // Nothing in the way, so the whole thing is one walk and one search.
        Assert.Single(WalkInLegs(route, start));
    }

    [Fact]
    public void TurnsARealRouteIntoAHandfulOfWalks()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 40, -8, 8)
            .WithWall(x: 20, y: 64, fromZ: -2, toZ: 2);

        var start = new Vector3i(0, 64, 0);
        var route = new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRoute(start, new Vector3i(30, 64, 0));

        Assert.NotNull(route);

        var legs = WalkInLegs(route, start);

        // Thirty-six blocks of walking become three movements, and three
        // searches instead of thirty-six: out to clear the wall, the long run
        // across, and back onto the line. Without the turn cost in the search
        // this was nine, because a staircase of the same length is the one shape
        // that cannot be run together.
        Assert.InRange(legs.Count, 1, 4);

        // Straighter, not longer: the detour is still the shortest one there is.
        Assert.Equal(36, route.Moves.Count);

        Assert.Equal(route.Destination, legs[^1]);
    }

    /// <summary>
    /// Follows a route the way the task does, taking the furthest walk in line
    /// each time, and reports the blocks it actually aimed at.
    /// </summary>
    private static List<Vector3i> WalkInLegs(Route route, Vector3i from)
    {
        var legs = new List<Vector3i>();
        var remaining = route;

        while (remaining.Next is { } next)
        {
            var leg = next is Walk ? remaining.FurthestWalk(from)! : next.To;

            legs.Add(leg);

            var taken = remaining.Moves.ToList().FindIndex(move => move.To == leg) + 1;

            remaining = new Route(remaining.Moves.Skip(taken).ToArray());
            from = leg;
        }

        return legs;
    }

    private static Route WalkLine(Vector3i direction, int length)
        => new(Enumerable.Range(1, length)
            .Select(i => (Move)new Walk(_start + direction * i))
            .ToArray());
}
