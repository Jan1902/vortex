using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Placing blocks to get somewhere: up by standing on them, across by walking
/// on them.
/// </summary>
public class BuildingTests
{
    [Fact]
    public void PillarsOutOfAPit()
    {
        var route = Find(Pit(), new Vector3i(0, 64, 0), new Vector3i(3, 68, 0), Builder(blocks: 16));

        Assert.NotNull(route);
        Assert.Equal(3, route.Moves.OfType<Pillar>().Count());
        Assert.Equal(new Vector3i(3, 68, 0), route.Destination);
    }

    [Fact]
    public void StaysInThePitWithoutBlocks()
        => Assert.Null(Find(Pit(), new Vector3i(0, 64, 0), new Vector3i(3, 68, 0), Builder(blocks: 0)));

    [Fact]
    public void DoesNotBuildWhenItMayNot()
    {
        var withBlocksButNoLeave = MovementCapabilities.Walking with { Loadout = new Loadout([], 16) };

        Assert.Null(Find(Pit(), new Vector3i(0, 64, 0), new Vector3i(3, 68, 0), withBlocksButNoLeave));
    }

    [Fact]
    public void BridgesAGap()
    {
        var route = Find(Chasm(), new Vector3i(0, 64, 0), new Vector3i(7, 64, 0), Builder(blocks: 16));

        Assert.NotNull(route);

        var bridges = route.Moves.OfType<Bridge>().ToList();

        Assert.Equal(5, bridges.Count);
        Assert.All(bridges, bridge => Assert.Equal(bridge.To with { Y = 63 }, bridge.Support));
    }

    [Fact]
    public void DoesNotStartABridgeItCannotFinish()
    {
        // Five blocks of gap, four blocks to fill it with.
        Assert.Null(Find(Chasm(), new Vector3i(0, 64, 0), new Vector3i(7, 64, 0), Builder(blocks: 4)));
    }

    [Fact]
    public void WalksRoundRatherThanBuildWhenRoundIsNotFar()
    {
        // Ground carries on round the end of the chasm, a few blocks along.
        var world = Chasm().WithFloor(63, -3, 10, 4, 5);

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(7, 64, 0), Builder(blocks: 16));

        Assert.NotNull(route);
        Assert.DoesNotContain(route.Moves, move => move is Bridge or Pillar);
    }

    /// <summary>
    /// Standing at the bottom of a pit four blocks deep, one block across, with
    /// solid rock that cannot be dug round on every side.
    /// </summary>
    private static FakeWorld Pit()
    {
        var world = new FakeWorld().WithFloor(67, -4, 6, -4, 4);

        for (var y = 63; y <= 67; y++)
            for (var x = -1; x <= 1; x++)
                for (var z = -1; z <= 1; z++)
                    if (y == 63 || x != 0 || z != 0)
                        world.With(new Vector3i(x, y, z), Block.Bedrock);

        world.With(new Vector3i(0, 67, 0), Block.Air);

        return world;
    }

    /// <summary>
    /// Ground to x = 0 and from x = 6, with five blocks of nothing between,
    /// right down to the bottom of the world.
    /// </summary>
    private static FakeWorld Chasm()
        => new FakeWorld()
            .WithFloor(63, -3, 0, -3, 3)
            .WithFloor(63, 6, 10, -3, 3)
            .WithFloor(-60, 1, 5, -3, 3);

    /// <summary>
    /// May build but not dig, so that digging a way out never competes with
    /// building one.
    /// </summary>
    private static MovementCapabilities Builder(int blocks)
        => MovementCapabilities.Walking with { Build = true, Loadout = new Loadout([], blocks) };

    private static Route? Find(FakeWorld world, Vector3i start, Vector3i goal, MovementCapabilities capabilities)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance).FindRoute(start, goal, capabilities);
}
