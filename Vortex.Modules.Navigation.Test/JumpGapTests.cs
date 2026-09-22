using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Jumping gaps, and the fact that it only happens when it was asked for.
/// </summary>
public class JumpGapTests
{
    [Fact]
    public void FindsNoWayAcrossAGapWhenItMayNotJump()
    {
        // The trench spans the corridor, so jumping is the only way over it.
        // Without that ability there is simply no route, which is the honest
        // answer rather than a route the bot would fall into.
        Assert.Null(Find(Trench(width: 1), MovementCapabilities.Walking));
    }

    [Fact]
    public void JumpsTheGapWhenItMay()
    {
        var route = Find(Trench(width: 1), MovementCapabilities.Athletic);

        Assert.NotNull(route);

        var jump = Assert.Single(route.Moves.OfType<JumpGap>());

        Assert.Equal(1, jump.Distance);
        Assert.Equal(new Vector3i(6, 64, 0), jump.To);
    }

    [Fact]
    public void JumpsATwoBlockGap()
    {
        var route = Find(Trench(width: 2), MovementCapabilities.Athletic);

        Assert.NotNull(route);

        var jump = Assert.Single(route.Moves.OfType<JumpGap>());

        Assert.Equal(2, jump.Distance);
        Assert.Equal(new Vector3i(7, 64, 0), jump.To);
    }

    [Fact]
    public void SprintsAtAThreeBlockGap()
    {
        var route = Find(Trench(width: 3), MovementCapabilities.Athletic);

        Assert.NotNull(route);

        var jump = Assert.Single(route.Moves.OfType<JumpGap>());

        // Walking does not carry three blocks. Rather than refuse the gap, the
        // search asks for the run-up that does -- and says so, because whoever
        // walks the route has to know to sprint.
        Assert.Equal(3, jump.Distance);
        Assert.True(jump.Sprinting);
    }

    [Fact]
    public void WalksAtAGapItDoesNotHaveToSprintAt()
    {
        var route = Find(Trench(width: 2), MovementCapabilities.Athletic);

        // The faster the take-off the less say there is in where it comes down,
        // so speed is for gaps that need it and nothing else.
        Assert.False(Assert.Single(route!.Moves.OfType<JumpGap>()).Sprinting);
    }

    [Fact]
    public void WillNotPlanAThreeBlockGapItMayNotSprintAt()
        => Assert.Null(Find(Trench(width: 3), new MovementCapabilities(JumpGaps: true, Diagonals: true)));

    [Fact]
    public void WillNotPlanAGapBeyondAnyRunUp()
    {
        // Four blocks of nothing, on the level, is past what the physics does.
        // Refusing is the honest answer rather than a route to fall into.
        Assert.Null(Find(Trench(width: 4), MovementCapabilities.Athletic));
    }

    [Fact]
    public void DoesNotJumpWhereThereIsFloorToWalkOn()
    {
        var world = new FakeWorld().WithFloor(63, -8, 12, -8, 8);

        var route = Find(world, MovementCapabilities.Athletic);

        Assert.NotNull(route);

        // A jump costs more than the walking it replaces, so open ground stays
        // a walk even when jumping is allowed.
        Assert.DoesNotContain(route.Moves, move => move is JumpGap);
    }

    [Fact]
    public void TakesTheJumpOverALongWayRound()
    {
        // A hole in an open field: two walks and a jump beats going round it.
        var world = new FakeWorld()
            .WithFloor(63, -8, 12, -8, 8)
            .WithPool(Block.Air, y: 63, fromX: 5, toX: 5, fromZ: -3, toZ: 3);

        var route = Find(world, MovementCapabilities.Athletic);

        Assert.NotNull(route);
        Assert.Contains(route.Moves, move => move is JumpGap);
    }

    [Fact]
    public void WillNotJumpUnderACeiling()
    {
        var world = Trench(width: 1)
            // A roof over the gap: the arc would put the player's head through
            // it and drop it in.
            .WithPool(Block.Stone, y: 66, fromX: 4, toX: 7, fromZ: -1, toZ: 1);

        Assert.Null(Find(world, MovementCapabilities.Athletic));
    }

    [Fact]
    public void JumpsOverLavaButNeverIntoIt()
    {
        // Lava in the gap itself is nothing to be afraid of -- the arc passes
        // over it without touching.
        var overIt = Trench(width: 1)
            .WithPool(Block.Lava, y: 63, fromX: 5, toX: 5, fromZ: -1, toZ: 1);

        Assert.Contains(Find(overIt, MovementCapabilities.Athletic)!.Moves, move => move is JumpGap);

        // Lava where the feet would come down is another matter. Spread wide
        // enough that no landing within reach of any run-up is dry, there is
        // nowhere left to come down and the search says so.
        var intoIt = Trench(width: 1)
            .WithPool(Block.Lava, y: 64, fromX: 6, toX: 10, fromZ: -1, toZ: 1);

        Assert.Null(Find(intoIt, MovementCapabilities.Athletic));
    }

    /// <summary>
    /// A corridor three blocks wide with the floor missing for a run of blocks,
    /// so the only ways across are a jump or nothing.
    /// </summary>
    private static FakeWorld Trench(int width)
        => new FakeWorld()
            .WithFloor(63, -4, 12, -1, 1)
            .WithPool(Block.Air, y: 63, fromX: 5, toX: 4 + width, fromZ: -1, toZ: 1);

    private static Route? Find(FakeWorld world, MovementCapabilities capabilities)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRoute(new Vector3i(0, 64, 0), new Vector3i(10, 64, 0), capabilities);
}
