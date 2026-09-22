namespace Vortex.Data.Test;

public class ItemPropertiesTests
{
    [Theory]
    [InlineData(Item.Stone, 64)]
    [InlineData(Item.EnderPearl, 16)]
    [InlineData(Item.DiamondPickaxe, 1)]
    public void KnowsHowFarItemsStack(Item item, int stackSize)
        => Assert.Equal(stackSize, item.MaxStackSize());

    [Theory]
    [InlineData(Item.DiamondPickaxe, 1561)]
    [InlineData(Item.WoodenSword, 59)]
    [InlineData(Item.Stone, 0)]
    public void KnowsHowLongItemsLast(Item item, int maxDamage)
        => Assert.Equal(maxDamage, item.MaxDamage());
}
