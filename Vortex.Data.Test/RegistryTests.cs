namespace Vortex.Data.Test;

/// <summary>
/// Checks the generated registries against protocol IDs of 1.21.1, the version
/// the data was generated for.
/// </summary>
public class RegistryTests
{
    [Theory]
    [InlineData(Block.Air, 0)]
    [InlineData(Block.Stone, 1)]
    [InlineData(Block.OakLog, 46)]
    public void BlocksCarryTheirProtocolId(Block block, int id)
        => Assert.Equal(id, (int)block);

    [Theory]
    [InlineData(Item.Air, 0)]
    [InlineData(Item.DiamondPickaxe, 840)]
    [InlineData(Item.Stick, 848)]
    public void ItemsCarryTheirProtocolId(Item item, int id)
        => Assert.Equal(id, (int)item);

    [Theory]
    [InlineData(EntityType.Zombie, 124)]
    [InlineData(EntityType.Player, 128)]
    public void EntityTypesCarryTheirProtocolId(EntityType entityType, int id)
        => Assert.Equal(id, (int)entityType);

    [Fact]
    public void NamesWithDotsAndDigitsBecomeIdentifiers()
    {
        Assert.Equal(992, (int)SoundEvent.BlockNoteBlockHarp);
        Assert.Equal(2, (int)Menu.Generic9x3);
    }

    [Fact]
    public void EveryItemIsGenerated()
        => Assert.Equal(1333, Enum.GetValues<Item>().Length);
}
