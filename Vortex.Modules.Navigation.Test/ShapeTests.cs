using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Blocks that are not simply full cubes, where treating them as one plans a
/// route the player cannot walk.
/// </summary>
public class ShapeTests
{
    private static readonly Vector3i _start = new(0, 64, 0);

    [Theory]
    [InlineData(Block.OakFence)]
    [InlineData(Block.CobblestoneWall)]
    [InlineData(Block.OakFenceGate)]
    public void DoesNotClimbOverSomethingABlockAndAHalfTall(Block tall)
    {
        // A line of it right across the way. A block would be one step up and
        // one down; this is too tall to get onto at all.
        var world = Field();

        for (var z = -8; z <= 8; z++)
            world.With(new Vector3i(2, 64, z), tall);

        Assert.Null(Find(world, new Vector3i(4, 64, 0), MovementCapabilities.Athletic));
    }

    [Fact]
    public void DoesNotStandOnABottomSlab()
    {
        // A slab in the way, with room to walk round it. Standing on it puts
        // the player half a block lower than a route through its top expects.
        var world = Field().With(new Vector3i(2, 64, 0), Block.OakSlab);

        var route = Find(world, new Vector3i(4, 64, 0), MovementCapabilities.Athletic);

        Assert.NotNull(route);
        Assert.DoesNotContain(new Vector3i(2, 65, 0), route.Positions);
    }

    [Fact]
    public void StillStepsOntoAFullBlock()
    {
        // The counterpart of the two above: a full block is still a step up.
        var world = new FakeWorld()
            .WithFloor(63, -2, 6, 0, 0)
            .WithBlock(new Vector3i(2, 64, 0));

        var route = Find(world, new Vector3i(4, 64, 0), MovementCapabilities.Athletic);

        Assert.NotNull(route);
        Assert.Contains(new Vector3i(2, 65, 0), route.Positions);
    }

    [Fact]
    public void KnowsWhereItStandsOnAPath()
    {
        // A path is a sixteenth short of a full block, so a player on it has
        // its feet inside the path block itself.
        var world = Field();

        for (var x = -2; x <= 6; x++)
            world.With(new Vector3i(x, 64, 0), Block.DirtPath);

        var route = new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRoute(new Vector3d(0.5, 64.9375, 0.5), new Vector3i(3, 65, 0), MovementCapabilities.Athletic);

        Assert.NotNull(route);
        Assert.Equal(new Vector3i(0, 65, 0), route.Origin);
        Assert.Equal([new(1, 65, 0), new(2, 65, 0), new(3, 65, 0)], route.Positions);
    }

    [Fact]
    public void GoesRoundACobweb()
    {
        var world = Field().With(new Vector3i(2, 64, 0), Block.Cobweb);

        var route = Find(world, new Vector3i(4, 64, 0), MovementCapabilities.Athletic);

        Assert.NotNull(route);
        Assert.DoesNotContain(new Vector3i(2, 64, 0), route.Positions);
    }

    [Fact]
    public void CutsThroughACobwebOnlyWithSomethingToCutItWith()
    {
        // A corridor the web fills completely. By hand it takes twenty
        // seconds; with a sword, a moment.
        // Bedrock all round, so there is no digging a way past it instead.
        var world = new FakeWorld();

        for (var x = -2; x <= 6; x++)
            for (var y = 63; y <= 66; y++)
                for (var z = -1; z <= 1; z++)
                    if (y == 63 || y == 66 || z != 0)
                        world.With(new Vector3i(x, y, z), Block.Bedrock);

        world.With(new Vector3i(2, 64, 0), Block.Cobweb).With(new Vector3i(2, 65, 0), Block.Cobweb);

        Assert.Null(Find(world, new Vector3i(4, 64, 0), MovementCapabilities.Digging));

        var withSword = MovementCapabilities.Digging with { Loadout = new Loadout([Item.IronSword], 0) };
        var route = Find(world, new Vector3i(4, 64, 0), withSword);

        Assert.NotNull(route);
        Assert.Contains(route.Moves, move => move is MineThrough through && through.Blocking.Contains(new Vector3i(2, 64, 0)));
    }

    private static FakeWorld Field()
        => new FakeWorld().WithFloor(63, -8, 8, -8, 8);

    private static Route? Find(FakeWorld world, Vector3i goal, MovementCapabilities capabilities)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance).FindRoute(_start, goal, capabilities);
}
