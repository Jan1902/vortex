using Vortex.Data;
using Vortex.Modules.Behaviour.Tasks.Helper;

namespace Vortex.Modules.Behaviour.Test;

public class BlockDropsTests
{
    [Fact]
    public void KnowsWhichBlocksAnItemComesOutOf()
    {
        var blocks = BlockDrops.Producing([Item.RawIron]);

        Assert.Contains(Block.IronOre, blocks);
        Assert.Contains(Block.DeepslateIronOre, blocks);
        Assert.DoesNotContain(Block.Stone, blocks);
    }

    [Fact]
    public void FindsBlocksThatDropSomethingElseThanThemselves()
        => Assert.Contains(Block.Stone, BlockDrops.Producing([Item.Cobblestone]));

    [Fact]
    public void LeavesOutWhatPeopleBuildWith()
    {
        // Planks only ever give back planks, which are crafted: a house, not a tree.
        Assert.DoesNotContain(Block.OakPlanks, BlockDrops.Producing([Item.OakPlanks]));
        Assert.Contains(Block.OakLog, BlockDrops.Producing([Item.OakLog]));
    }

    [Fact]
    public void KnowsWhichToolsGetAnItemOutOfABlock()
    {
        var tools = BlockDrops.ToolsFor(BlockState.Default(Block.Stone), new HashSet<Item> { Item.Cobblestone });

        Assert.Contains(Item.WoodenPickaxe, tools);
        Assert.Contains(Item.DiamondPickaxe, tools);
        Assert.DoesNotContain(Item.WoodenShovel, tools);
    }

    [Fact]
    public void NeedsAtLeastAStonePickaxeForIron()
    {
        var tools = BlockDrops.ToolsFor(BlockState.Default(Block.IronOre), new HashSet<Item> { Item.RawIron });

        Assert.Contains(Item.StonePickaxe, tools);
        Assert.DoesNotContain(Item.WoodenPickaxe, tools);
    }
}
