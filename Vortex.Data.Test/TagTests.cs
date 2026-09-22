namespace Vortex.Data.Test;

public class TagTests
{
    [Fact]
    public void ResolvesTagsThatIncludeOtherTags()
    {
        // #logs lists no blocks itself, only #logs_that_burn and the nether stems.
        Assert.Contains(Block.OakLog, BlockTags.Logs);
        Assert.Contains(Block.CrimsonStem, BlockTags.Logs);
        Assert.DoesNotContain(Block.Stone, BlockTags.Logs);
    }

    [Fact]
    public void ResolvesTagsInSubfolders()
    {
        Assert.Contains(Block.Stone, BlockTags.MineablePickaxe);
        Assert.Contains(Block.DiamondOre, BlockTags.NeedsIronTool);
    }

    [Fact]
    public void GeneratesItemAndEntityTypeTags()
    {
        Assert.Contains(Item.OakPlanks, ItemTags.Planks);
        Assert.Contains(EntityType.Skeleton, EntityTypeTags.Skeletons);
    }
}
