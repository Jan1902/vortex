using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Digging priced by how long the blocks really take to break with what the
/// player carries.
/// </summary>
public class BreakTimeTests
{
    private static readonly Vector3i _start = new(0, 64, 0);
    private static readonly Vector3i _goal = new(5, 64, 0);

    [Fact]
    public void LeavesObsidianAloneWithoutADiamondPickaxe()
    {
        var world = Walled(Block.Obsidian);

        Assert.Null(Find(world, MovementCapabilities.Digging));
    }

    [Fact]
    public void BreaksObsidianWithADiamondPickaxe()
    {
        var world = Walled(Block.Obsidian);

        var route = Find(world, MovementCapabilities.Digging with { Loadout = new Loadout([Item.DiamondPickaxe], 0) });

        Assert.NotNull(route);
        Assert.Contains(route.Moves, move => move is MineThrough);
    }

    [Fact]
    public void WalksRoundByHandButDigsThroughWithAPickaxe()
    {
        // A stone wall with a gap a long way along it. By hand, stone takes
        // long enough that the walk round is quicker; with a good pickaxe it
        // is not.
        var world = new FakeWorld()
            .WithFloor(63, -3, 8, -20, 20)
            .WithWall(x: 3, y: 64, fromZ: -20, toZ: 14)
            .WithWall(x: 3, y: 64, fromZ: 16, toZ: 20);

        var byHand = Find(world, MovementCapabilities.Digging);
        var withPickaxe = Find(world, MovementCapabilities.Digging with { Loadout = new Loadout([Item.DiamondPickaxe], 0) });

        Assert.NotNull(byHand);
        Assert.DoesNotContain(byHand.Moves, move => move is MineThrough);

        Assert.NotNull(withPickaxe);
        Assert.Contains(withPickaxe.Moves, move => move is MineThrough);
    }

    [Fact]
    public void KnowsWhatBreaksFastestWithWhatItCarries()
    {
        var loadout = new Loadout([Item.WoodenShovel, Item.IronPickaxe], 0);

        Assert.Equal(Mining.BreakTicks(Block.Stone, Item.IronPickaxe), loadout.BreakTicks(Block.Stone));
        Assert.Equal(Mining.BreakTicks(Block.Dirt, Item.WoodenShovel), loadout.BreakTicks(Block.Dirt));
        Assert.Null(loadout.BreakTicks(Block.Bedrock));
    }

    /// <summary>Floor with a wall of something across it at x = 3, two blocks high, and no way round.</summary>
    private static FakeWorld Walled(Block block)
    {
        var world = new FakeWorld().WithFloor(63, -3, 8, -3, 3);

        for (var z = -3; z <= 3; z++)
            world.With(new Vector3i(3, 64, z), block).With(new Vector3i(3, 65, z), block);

        return world;
    }

    private static Route? Find(FakeWorld world, MovementCapabilities capabilities)
        => new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance).FindRoute(_start, _goal, capabilities);
}
