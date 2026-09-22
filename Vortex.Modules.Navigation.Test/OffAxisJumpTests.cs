using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Jumps to blocks off every one of the eight directions, such as two on and
/// one to the side -- the everyday shape of a jump and run.
/// </summary>
public class OffAxisJumpTests
{
    private static readonly Vector3i _start = new(0, 64, 0);

    [Fact]
    public void JumpsTwoOnAndOneToTheSide()
    {
        var route = Find(Lone(new Vector3i(2, 63, 1)), new(2, 64, 1));

        Assert.NotNull(route);

        var jump = Assert.IsType<JumpGap>(Assert.Single(route.Moves));

        Assert.Equal(new Vector3i(2, 64, 1), jump.To);
        Assert.False(jump.Sprinting);
    }

    [Fact]
    public void SprintsWhereWalkingDoesNotReach()
    {
        // Three on and one to the side is further than a walking jump carries.
        var jump = Assert.IsType<JumpGap>(Assert.Single(Find(Lone(new Vector3i(3, 63, 1)), new(3, 64, 1))!.Moves));

        Assert.True(jump.Sprinting);
    }

    [Fact]
    public void JumpsUpOntoABlockTwoOnAndOneToTheSide()
    {
        var jump = Assert.IsType<JumpGap>(Assert.Single(Find(Lone(new Vector3i(2, 64, 1)), new(2, 65, 1))!.Moves));

        Assert.Equal(new Vector3i(2, 65, 1), jump.To);
    }

    [Fact]
    public void DoesNotJumpWhenItMayNot()
        => Assert.Null(Find(Lone(new Vector3i(2, 63, 1)), new(2, 64, 1), MovementCapabilities.Walking));

    [Fact]
    public void DoesNotJumpThroughAWall()
    {
        // A block at head height on the line between the two.
        var world = Lone(new Vector3i(2, 63, 1)).WithBlock(new(1, 65, 1));

        Assert.Null(Find(world, new(2, 64, 1)));
    }

    [Fact]
    public void WalksWhereThereIsNothingToJumpOver()
    {
        var world = new FakeWorld().WithFloor(63, -1, 3, -1, 2);

        var route = Find(world, new(2, 64, 1));

        Assert.NotNull(route);
        Assert.DoesNotContain(route.Moves, move => move is JumpGap);
    }

    [Fact]
    public void FollowsAJumpAndRun()
    {
        // Each block two on and one to the side of the last, far enough apart
        // that none can be skipped.
        var world = Lone(new Vector3i(2, 63, 1), new Vector3i(4, 63, 2), new Vector3i(6, 63, 3));

        var route = Find(world, new(6, 64, 3));

        Assert.NotNull(route);
        Assert.Equal(3, route.Moves.Count);
        Assert.All(route.Moves, move => Assert.IsType<JumpGap>(move));
    }

    /// <summary>The block the start stands on, and lone blocks in the void.</summary>
    private static FakeWorld Lone(params Vector3i[] blocks)
        => blocks.Aggregate(new FakeWorld().WithBlock(new(0, 63, 0)), (world, block) => world.WithBlock(block));

    private static Route? Find(FakeWorld world, Vector3i goal, MovementCapabilities? capabilities = null)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRoute(_start, goal, capabilities ?? MovementCapabilities.Athletic);
}
