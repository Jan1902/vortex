using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Moving corner to corner, and the corners that cannot be cut.
/// </summary>
public class DiagonalTests
{
    private static readonly Vector3i _start = new(0, 64, 0);
    private static readonly Vector3i _goal = new(6, 64, 6);

    [Fact]
    public void CutsTheCornerWhenItMay()
    {
        var route = Find(Field(), MovementCapabilities.Athletic);

        Assert.NotNull(route);
        Assert.Contains(Steps(route), step => step.X != 0 && step.Z != 0);
    }

    [Fact]
    public void StaysOnTheAxesWhenItMayNot()
    {
        var route = Find(Field(), MovementCapabilities.Walking);

        Assert.NotNull(route);

        // Not a style preference: a route is only walkable by something that can
        // make every move in it, so offering diagonals to a caller that did not
        // ask for them hands it a route it cannot walk.
        Assert.All(Steps(route), step => Assert.True(step.X == 0 || step.Z == 0));
    }

    [Fact]
    public void GoesTheShorterWayRound()
    {
        var straight = Find(Field(), MovementCapabilities.Walking)!;
        var cornering = Find(Field(), MovementCapabilities.Athletic)!;

        // Twelve blocks along the sides against six across the corner. The point
        // of diagonals is not only the distance but the stopping: the route is
        // re-searched between movements, so half the moves is half the searches.
        Assert.True(
            cornering.Moves.Count < straight.Moves.Count,
            $"cornering took {cornering.Moves.Count} moves, straight took {straight.Moves.Count}");
    }

    [Fact]
    public void WillNotSqueezeBetweenTwoCorners()
    {
        // Two walls that meet at a corner, leaving a diagonal the width of
        // nothing at all. The player is wider than the line between two blocks,
        // so this is not a way through however it looks on the grid.
        Assert.Null(Find(Pinched(), MovementCapabilities.Athletic));
    }

    [Fact]
    public void GoesThroughACornerThatIsActuallyOpen()
    {
        // The same two walls, with the second starting a block further along so
        // there is room beside the corner. This is what says the refusal above
        // is about the pinch and not about the walls.
        Assert.NotNull(Find(Walls(secondWallFrom: 2), MovementCapabilities.Athletic));
    }

    /// <summary>Open ground, with the goal diagonally across it.</summary>
    private static FakeWorld Field()
        => new FakeWorld().WithFloor(63, -2, 8, -2, 8);

    /// <summary>
    /// Two walls meeting corner to corner, so the only line between them runs
    /// exactly through the join.
    /// </summary>
    private static FakeWorld Pinched()
        => Walls(secondWallFrom: 1);

    /// <summary>
    /// A wall across the near half and another across the far half, one column
    /// closer. Where the second starts decides whether the join is a corner to
    /// squeeze through or a gap to walk round.
    /// </summary>
    private static FakeWorld Walls(int secondWallFrom)
        => new FakeWorld()
            .WithFloor(63, -2, 8, -2, 8)
            .WithWall(x: 5, y: 64, fromZ: -2, toZ: 0)
            .WithWall(x: 4, y: 64, fromZ: secondWallFrom, toZ: 8);

    /// <summary>The move-to-move offsets of a route, starting from its origin.</summary>
    private static IEnumerable<Vector3i> Steps(Route route)
    {
        var at = route.Origin!;

        foreach (var move in route.Moves)
        {
            yield return move.To - at;

            at = move.To;
        }
    }

    private static Route? Find(FakeWorld world, MovementCapabilities capabilities)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRoute(_start, _goal, capabilities);
}
