using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Keeping a route up to date while it is walked: noticing when a move no
/// longer works, and not swapping it for another one of the same price when
/// it is searched again.
/// </summary>
public class ReplanningTests
{
    private static readonly Vector3i _start = new(0, 64, 0);

    [Fact]
    public void AMoveStillWorksWhileNothingHasChanged()
    {
        var world = Field();
        var route = Finder(world).FindRoute(_start, new Vector3i(5, 64, 0))!;

        Assert.True(Finder(world).CanStillMake(_start, route.Moves[0]));
    }

    [Fact]
    public void AWalkStopsWorkingOnceSomethingIsBuiltInTheWay()
    {
        var world = Field();
        var route = Finder(world).FindRoute(_start, new Vector3i(5, 64, 0))!;

        world.WithBlock(new Vector3i(1, 64, 0));

        Assert.False(Finder(world).CanStillMake(_start, route.Moves[0]));
    }

    [Fact]
    public void AWalkStopsWorkingOnceTheFloorIsGone()
    {
        var world = Field();
        var route = Finder(world).FindRoute(_start, new Vector3i(5, 64, 0))!;

        world.With(new Vector3i(1, 63, 0), Block.Air);

        Assert.False(Finder(world).CanStillMake(_start, route.Moves[0]));
    }

    [Fact]
    public void AJumpHasToBeTheSameJump()
    {
        var world = Field();

        // Walking reaches across one block of gap; this route claims it needs a
        // sprint, which is not what the search would plan here.
        world.With(new Vector3i(1, 63, 0), Block.Air);

        Assert.True(Finder(world).CanStillMake(_start, new JumpGap(new(2, 64, 0), 1), MovementCapabilities.Athletic));
        Assert.False(Finder(world).CanStillMake(_start, new JumpGap(new(2, 64, 0), 1, Sprinting: true), MovementCapabilities.Athletic));
    }

    [Fact]
    public void DiggingThroughStopsWorkingWhenTheBlocksAreAlreadyGone()
    {
        var world = Field().WithWall(x: 1, y: 64, fromZ: 0, toZ: 0, height: 3);
        var through = new MineThrough(new(1, 64, 0), [new(1, 65, 0), new(1, 64, 0)]);

        Assert.True(Finder(world).CanStillMake(_start, through, MovementCapabilities.Digging));

        // Someone else took one of them out: the plan no longer describes what
        // is there, and should be made again rather than carried out.
        world.With(new Vector3i(1, 65, 0), Block.Air);

        Assert.False(Finder(world).CanStillMake(_start, through, MovementCapabilities.Digging));
    }

    [Fact]
    public void AMoveTheCallerMayNotMakeIsNotOneItCanStillMake()
    {
        var world = Field();

        world.With(new Vector3i(1, 63, 0), Block.Air);

        Assert.False(Finder(world).CanStillMake(_start, new JumpGap(new(2, 64, 0), 1), MovementCapabilities.Walking));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void KeepsToTheRouteItIsReplacing(int side)
    {
        // A wall straight across the way with ways round at both ends, the
        // same length either way. Which one is taken is a coin toss -- unless
        // there is already a route going one way.
        var world = new FakeWorld()
            .WithFloor(63, -2, 12, -8, 8)
            .WithWall(x: 5, y: 64, fromZ: -5, toZ: 5, height: 3);

        var goal = new Vector3i(10, 64, 0);
        var previous = new Route(
            [.. Enumerable.Range(1, 6).Select(z => new Walk(new(0, 64, z * side)))],
            Origin: _start);

        var route = Finder(world).FindRoute(_start, goal, MovementCapabilities.Walking, previous);

        Assert.NotNull(route);
        Assert.All(route.Positions, position => Assert.True(position.Z * side >= 0, $"went round the other side at {position}"));
    }

    [Fact]
    public void StillLeavesTheRouteItIsReplacingForAMuchBetterOne()
    {
        // The old route goes a long way off to the side; straight on is now
        // open and far shorter. Keeping to the old one is only worth it when
        // the two are close.
        var world = Field();

        var previous = new Route(
            [.. Enumerable.Range(1, 8).Select(z => new Walk(new(0, 64, z)))],
            Origin: _start);

        var route = Finder(world).FindRoute(_start, new Vector3i(6, 64, 0), MovementCapabilities.Walking, previous);

        Assert.NotNull(route);
        Assert.Equal(6, route.Moves.Count);
    }

    [Fact]
    public void ABridgeStopsWorkingOnceTheRoomAboveItIsTaken()
    {
        var world = new FakeWorld().WithFloor(63, -2, 0, -2, 2).WithFloor(63, 3, 5, -2, 2).WithFloor(-60, 1, 2, -2, 2);
        var building = MovementCapabilities.Building with { Loadout = new Loadout([], 8) };
        var bridge = new Bridge(new(1, 64, 0), new(1, 63, 0));

        Assert.True(Finder(world).CanStillMake(_start, bridge, building));

        world.WithBlock(new Vector3i(1, 65, 0));

        Assert.False(Finder(world).CanStillMake(_start, bridge, building));
    }

    [Fact]
    public void ABridgeNeedsBlocksToBuildItWith()
    {
        var world = new FakeWorld().WithFloor(63, -2, 0, -2, 2).WithFloor(63, 3, 5, -2, 2).WithFloor(-60, 1, 2, -2, 2);
        var bridge = new Bridge(new(1, 64, 0), new(1, 63, 0));

        Assert.False(Finder(world).CanStillMake(_start, bridge, MovementCapabilities.Building));
    }

    private static FakeWorld Field()
        => new FakeWorld().WithFloor(63, -8, 12, -8, 12);

    private static AStarPathfinder Finder(FakeWorld world)
        => new(world, NullLogger<AStarPathfinder>.Instance);
}
