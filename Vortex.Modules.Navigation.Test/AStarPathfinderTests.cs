using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

public class AStarPathfinderTests
{
    [Fact]
    public void WalksStraightAcrossFlatGround()
    {
        var world = new FakeWorld().WithFloor(63, -8, 8, -8, 8);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(3, 64, 0));

        Assert.NotNull(route);
        Assert.Equal(
            [new(1, 64, 0), new(2, 64, 0), new(3, 64, 0)],
            route.Positions);
    }

    [Fact]
    public void ReturnsAnEmptyRouteWhenAlreadyThere()
    {
        var world = new FakeWorld().WithFloor(63, -8, 8, -8, 8);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(0, 64, 0));

        Assert.NotNull(route);
        Assert.Empty(route.Positions);
    }

    [Fact]
    public void GoesAroundAWall()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 8, -8, 8)
            .WithWall(x: 1, y: 64, fromZ: -1, toZ: 1);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(3, 64, 0));

        Assert.NotNull(route);

        // Three blocks apart, but the wall forces two sideways steps out and two
        // back again.
        Assert.Equal(7, route.Moves.Count);
        Assert.DoesNotContain(route.Positions.ToList(), s => s.X == 1 && s.Z is >= -1 and <= 1);
    }

    [Fact]
    public void StepsUpOntoALedge()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 1, -8, 8)
            .WithFloor(64, 2, 8, -8, 8);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(3, 65, 0));

        Assert.NotNull(route);
        Assert.Equal(
            [new(1, 64, 0), new(2, 65, 0), new(3, 65, 0)],
            route.Positions);
    }

    [Fact]
    public void WillNotStepUpWithoutHeadroom()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 1, -8, 8)
            .WithFloor(64, 2, 8, -8, 8)
            // A ceiling over the spot the player would have to jump from.
            .WithBlock(new Vector3i(1, 66, 0));

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(3, 65, 0));

        Assert.NotNull(route);
        Assert.DoesNotContain(route.Positions.ToList(), s => s == new Vector3i(2, 65, 0));
    }

    [Fact]
    public void ClimbsOverASingleBlockRatherThanWalkingAllTheWayRound()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 8, -8, 8)
            .WithBlock(new Vector3i(1, 64, 0));

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(2, 64, 0));

        Assert.NotNull(route);

        // Over the top is two moves; round the side is four. Climbing is dearer
        // per move, but not dear enough to be worth a detour that long.
        Assert.Equal([new(1, 65, 0), new(2, 64, 0)], route.Positions);
    }

    [Fact]
    public void StaysFlatWhenThatCostsNoExtraSteps()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 8, -8, 8)
            .WithBlock(new Vector3i(1, 64, 1));

        // Four moves either way, but one of the ways goes over the raised block.
        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(2, 64, 2));

        Assert.NotNull(route);
        Assert.Equal(4, route.Moves.Count);
        Assert.DoesNotContain(route.Positions.ToList(), s => s == new Vector3i(1, 65, 1));
    }

    [Fact]
    public void DropsDownALedge()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 1, -8, 8)
            .WithFloor(60, 2, 8, -8, 8);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(3, 61, 0));

        Assert.NotNull(route);
        Assert.Equal(
            [new(1, 64, 0), new(2, 61, 0), new(3, 61, 0)],
            route.Positions);
    }

    [Fact]
    public void WillNotDropFurtherThanItCanSurvive()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 1, -8, 8)
            .WithFloor(50, 2, 8, -8, 8);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(3, 51, 0));

        Assert.Null(route);
    }

    [Fact]
    public void RefusesAGoalThereIsNothingToStandOn()
    {
        var world = new FakeWorld().WithFloor(63, -8, 8, -8, 8);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(3, 70, 0));

        Assert.Null(route);
    }

    [Fact]
    public void RefusesAGoalInsideABlock()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 8, -8, 8)
            .WithBlock(new Vector3i(3, 64, 0));

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(3, 64, 0));

        Assert.Null(route);
    }

    [Fact]
    public void TreatsUnloadedChunksAsSolid()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 8, -8, 8)
            .WithUnloaded(x: 1, fromY: 60, toY: 70, fromZ: -1, toZ: 1);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(3, 64, 0));

        Assert.NotNull(route);

        // A route through a part of the world the client cannot see yet would be
        // a guess, so it goes round instead.
        Assert.DoesNotContain(route.Positions.ToList(), s => s.X == 1 && s.Z is >= -1 and <= 1);
    }

    [Fact]
    public void GivesUpWhenTheGoalIsWalledIn()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 8, -8, 8)
            .WithWall(x: 2, y: 64, fromZ: -1, toZ: 1)
            .WithWall(x: 4, y: 64, fromZ: -1, toZ: 1)
            .WithWall(x: 3, y: 64, fromZ: -1, toZ: -1)
            .WithWall(x: 3, y: 64, fromZ: 1, toZ: 1);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(3, 64, 0));

        Assert.Null(route);
    }

    [Fact]
    public void FindsItsWayOutOfAMaze()
    {
        // A corridor along z = 0 with staggered walls, so the only way through is
        // to weave between them.
        var world = new FakeWorld()
            .WithFloor(63, -1, 10, -2, 2)
            .WithWall(x: 2, y: 64, fromZ: -2, toZ: 1)
            .WithWall(x: 5, y: 64, fromZ: -1, toZ: 2)
            .WithWall(x: 8, y: 64, fromZ: -2, toZ: 1);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(10, 64, 0));

        Assert.NotNull(route);

        var walls = new HashSet<Vector3i> { new(2, 64, 0), new(5, 64, 0), new(8, 64, 0) };
        Assert.DoesNotContain(route.Positions.ToList(), walls.Contains);

        // Every step is one block sideways from the one before it.
        var previous = new Vector3i(0, 64, 0);
        foreach (var step in route.Positions)
        {
            Assert.Equal(1, Math.Abs(step.X - previous.X) + Math.Abs(step.Z - previous.Z));
            previous = step;
        }

        Assert.Equal(new Vector3i(10, 64, 0), route.Destination);
    }

    [Fact]
    public void PrefersStraightLegsToAStaircase()
    {
        var world = new FakeWorld().WithFloor(63, -8, 20, -8, 20);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(10, 64, 10));

        Assert.NotNull(route);

        // Twenty moves either way -- ten east and ten north in some order. Of
        // all the orders, the search should pick one of the two that only turn
        // once, because a zigzag of the same length has to be walked one block
        // at a time.
        Assert.Equal(20, route.Moves.Count);
        Assert.Equal(1, Turns(route.Positions.ToList(), new Vector3i(0, 64, 0)));
    }

    [Fact]
    public void DoesNotTakeALongerWayRoundJustToStayStraight()
    {
        var world = new FakeWorld()
            .WithFloor(63, -8, 20, -8, 20)
            .WithWall(x: 3, y: 64, fromZ: -1, toZ: 1);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(6, 64, 0));

        Assert.NotNull(route);

        // Round the wall is ten moves and four turns; any straighter way is
        // longer. Straightness only ever settles a tie.
        Assert.Equal(10, route.Moves.Count);
    }

    /// <summary>Counts how often the route changes direction.</summary>
    private static int Turns(IReadOnlyList<Vector3i> steps, Vector3i from)
    {
        var turns = 0;
        var heading = steps[0] - from;

        for (var i = 1; i < steps.Count; i++)
        {
            var next = steps[i] - steps[i - 1];

            if (next != heading)
                turns++;

            heading = next;
        }

        return turns;
    }

    private static Abstraction.Route? Find(IWorldManager world, Vector3i start, Vector3i goal)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance).FindRoute(start, goal);
}
