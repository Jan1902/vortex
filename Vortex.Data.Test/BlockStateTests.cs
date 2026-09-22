namespace Vortex.Data.Test;

/// <summary>
/// Checks that state IDs decode into the blocks and properties Mojang's blocks
/// report gives for them in 1.21.1.
/// </summary>
public class BlockStateTests
{
    [Theory]
    [InlineData(0, Block.Air)]
    [InlineData(1, Block.Stone)]
    [InlineData(10, Block.Dirt)]
    [InlineData(130, Block.OakLog)]
    [InlineData(132, Block.OakLog)]
    [InlineData(2975, Block.Chest)]
    public void FindsTheBlockOfAState(int id, Block block)
        => Assert.Equal(block, BlockState.FromId(id).Block);

    [Fact]
    public void KnowsTheDefaultState()
    {
        var log = BlockState.Default(Block.OakLog);

        Assert.Equal(131, log.Id);
        Assert.True(log.IsDefault);
        Assert.Equal(AxisValue.Y, log.Get(BlockProperties.Axis));
        Assert.False(BlockState.FromId(130).IsDefault);
    }

    [Fact]
    public void DecodesEnumBoolAndIntProperties()
    {
        var stairs = BlockState.FromId(2900);

        Assert.Equal(FacingValue.South, stairs.Get(BlockProperties.Facing));
        Assert.Equal(HalfValue.Top, stairs.Get(BlockProperties.Half));
        Assert.Equal(ShapeValue.OuterLeft, stairs.Get(BlockProperties.Shape));
        Assert.True(stairs.Get(BlockProperties.Waterlogged));

        Assert.Equal(7, BlockState.FromId(4285).Get(BlockProperties.Age));
    }

    [Fact]
    public void DecodesPropertiesListedOutOfOrderInTheReport()
    {
        // The report lists a chest's properties as type, facing, waterlogged, but
        // the game numbers the states by the names in alphabetical order.
        var chest = BlockState.FromId(2975);

        Assert.Equal(TypeValue.Left, chest.Get(BlockProperties.Type));
        Assert.Equal(FacingValue.East, chest.Get(BlockProperties.Facing));
        Assert.False(chest.Get(BlockProperties.Waterlogged));
    }

    [Fact]
    public void ReturnsNullForAPropertyTheBlockDoesNotHave()
    {
        var stone = BlockState.Default(Block.Stone);

        Assert.Null(stone.Get(BlockProperties.Waterlogged));
        Assert.Null(stone.Get(BlockProperties.Facing));
        Assert.False(stone.Has(BlockProperty.Waterlogged));
    }

    [Fact]
    public void GivesOneInstancePerState()
        => Assert.Same(BlockState.FromId(2900), BlockState.FromId(2900));

    [Fact]
    public void RejectsIdsOutsideTheTable()
    {
        Assert.True(BlockState.TryFromId(26683, out _));
        Assert.False(BlockState.TryFromId(26684, out _));
        Assert.False(BlockState.TryFromId(-1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => BlockState.FromId(26684));
    }
}
