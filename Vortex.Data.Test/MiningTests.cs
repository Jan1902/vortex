namespace Vortex.Data.Test;

/// <summary>
/// Checks break times against the ones the Minecraft wiki lists, in ticks of
/// 1/20 second.
/// </summary>
public class MiningTests
{
    [Theory]
    [InlineData(Block.Stone, null, 150)]
    [InlineData(Block.Stone, Item.WoodenPickaxe, 23)]
    [InlineData(Block.Stone, Item.IronPickaxe, 8)]
    [InlineData(Block.Dirt, null, 15)]
    [InlineData(Block.Dirt, Item.WoodenShovel, 8)]
    [InlineData(Block.Obsidian, Item.DiamondPickaxe, 188)]
    [InlineData(Block.Obsidian, Item.IronPickaxe, 834)]
    [InlineData(Block.Cobweb, Item.IronSword, 8)]
    [InlineData(Block.OakLog, Item.StoneAxe, 15)]
    public void TakesAsLongAsTheWikiSays(Block block, Item? tool, int ticks)
        => Assert.Equal(ticks, Mining.BreakTicks(block, tool));

    [Fact]
    public void BreaksSomeBlocksAtOnce()
        => Assert.Equal(0, Mining.BreakTicks(Block.ShortGrass, null));

    [Fact]
    public void CannotBreakBedrock()
        => Assert.Null(Mining.BreakTicks(Block.Bedrock, Item.NetheritePickaxe));

    [Fact]
    public void CountsEfficiency()
        => Assert.Equal(2, Mining.BreakTicks(Block.Stone, Item.DiamondPickaxe, efficiency: 5));

    [Fact]
    public void SlowsDownOffTheGround()
    {
        // Five times the 7.5 ticks on the ground, rounded up once at the end.
        Assert.Equal(38, Mining.BreakTicks(Block.Stone, Item.IronPickaxe, onGround: false));
    }

    [Theory]
    [InlineData(Block.Stone, null, false)]
    [InlineData(Block.Stone, Item.WoodenPickaxe, true)]
    [InlineData(Block.DiamondOre, Item.StonePickaxe, false)]
    [InlineData(Block.DiamondOre, Item.IronPickaxe, true)]
    [InlineData(Block.Dirt, null, true)]
    public void KnowsWhichToolsHarvest(Block block, Item? tool, bool harvests)
        => Assert.Equal(harvests, Mining.CanHarvest(block, tool));

    [Fact]
    public void ReadsHardnessFromPrismarine()
    {
        Assert.Equal(1.5f, Block.Stone.Hardness());
        Assert.Equal(-1f, Block.Bedrock.Hardness());
        Assert.True(Block.Stone.RequiresCorrectToolForDrops());
        Assert.False(Block.Dirt.RequiresCorrectToolForDrops());
    }
}
