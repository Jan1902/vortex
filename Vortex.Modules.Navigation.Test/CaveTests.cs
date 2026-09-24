using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Digging through into a hollow, and dropping into it, rather than only ever
/// onto a floor just the other side.
/// </summary>
public class CaveTests
{
    [Fact]
    public void DigsDownThroughTheRoofOfACave()
    {
        var world = Cave();

        var route = Find(world, new Vector3i(0, 64, 0), new Vector3i(3, 60, 0));

        Assert.NotNull(route);

        // The last block of roof opens onto two blocks of cave: it falls in,
        // rather than having to stand on the roof's last layer first.
        Assert.Contains(route.Moves, move => move is MineThrough { To.Y: 60 } through && through.Blocking.Contains(new Vector3i(0, 62, 0)));
        Assert.Equal(new Vector3i(3, 60, 0), route.Destination);
    }

    [Fact]
    public void TunnelsIntoACaveAndDropsIntoIt()
    {
        // Standing in a pocket in the rock, level with the top of the cave
        // next door: breaking through the side opens straight onto the drop.
        var world = Cave();

        foreach (var y in new[] { 62, 63 })
            world.With(new Vector3i(-5, y, 0), Block.Air);

        var route = Find(world, new Vector3i(-5, 62, 0), new Vector3i(3, 60, 0));

        Assert.NotNull(route);
        Assert.Contains(route.Moves, move => move is MineThrough { To.Y: 60 });
    }

    [Fact]
    public void DoesNotDigIntoACaveTooDeepToDropInto()
    {
        // Six blocks of cave: opening the roof would be a seven block fall.
        // Digging down beside it and in at the bottom is fine; falling in is
        // not.
        var world = Cave(depth: 6);
        var start = new Vector3i(0, 64, 0);

        var route = Find(world, start, new Vector3i(3, 56, 0));

        Assert.NotNull(route);

        var from = start;

        foreach (var move in route.Moves)
        {
            Assert.True(from.Y - move.To.Y <= 3, $"{move} falls {from.Y - move.To.Y} blocks");

            from = move.To;
        }
    }

    /// <summary>
    /// Solid rock up to y = 63, with a cave under it: <paramref name="depth"/>
    /// blocks of air up to y = 61, from x = -4 to 8.
    /// </summary>
    private static FakeWorld Cave(int depth = 2)
    {
        var world = new FakeWorld();
        var floor = 61 - depth;

        for (var y = floor - 3; y <= 63; y++)
            world.WithFloor(y, -8, 8, -3, 3);

        for (var x = -4; x <= 8; x++)
            for (var z = -1; z <= 1; z++)
                for (var y = floor + 1; y <= 61; y++)
                    world.With(new Vector3i(x, y, z), Block.Air);

        return world;
    }

    private static Route? Find(FakeWorld world, Vector3i start, Vector3i goal)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRoute(start, goal, MovementCapabilities.Digging with { Loadout = new Loadout([Item.DiamondPickaxe], 0) });
}
