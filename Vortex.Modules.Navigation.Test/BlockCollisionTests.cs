using Vortex.Data;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

public class BlockCollisionTests
{
    [Theory]
    [InlineData(Block.Stone)]
    [InlineData(Block.OakPlanks)]
    [InlineData(Block.Azalea)]
    [InlineData(Block.PottedOakSapling)]
    public void SolidBlocksBlockMovement(Block block)
        => Assert.True(BlockCollision.IsSolid(BlockState.Default(block)));

    [Theory]
    [InlineData(Block.Air)]
    [InlineData(Block.Water)]
    [InlineData(Block.ShortGrass)]
    [InlineData(Block.OakSapling)]
    [InlineData(Block.MangrovePropagule)]
    [InlineData(Block.Rail)]
    [InlineData(Block.PoweredRail)]
    [InlineData(Block.Candle)]
    [InlineData(Block.StoneButton)]
    [InlineData(Block.OakWallSign)]
    [InlineData(Block.WhiteCarpet)]
    [InlineData(Block.MossCarpet)]
    [InlineData(Block.BrainCoral)]
    [InlineData(Block.DeadBrainCoralWallFan)]
    public void PassableBlocksDoNot(Block block)
        => Assert.False(BlockCollision.IsSolid(BlockState.Default(block)));

    [Fact]
    public void AnUnloadedChunkIsSolid()
        => Assert.True(BlockCollision.IsSolid(null));

    [Theory]
    [InlineData(Block.OakFence)]
    [InlineData(Block.NetherBrickFence)]
    [InlineData(Block.CobblestoneWall)]
    [InlineData(Block.BirchFenceGate)]
    public void FencesAndWallsAreTall(Block block)
        => Assert.True(BlockCollision.IsTall(BlockState.Default(block)));

    [Theory]
    [InlineData(Block.Stone)]
    [InlineData(Block.OakSlab)]
    public void OtherBlocksAreNot(Block block)
        => Assert.False(BlockCollision.IsTall(BlockState.Default(block)));

    [Theory]
    [InlineData(Block.OakSlab)]
    [InlineData(Block.RedBed)]
    [InlineData(Block.OakTrapdoor)]
    [InlineData(Block.Repeater)]
    public void SomeSolidBlocksAreTooLowToStandOnAsAFullBlock(Block block)
        => Assert.True(BlockCollision.HasLowTop(BlockState.Default(block)));

    [Theory]
    [InlineData(Block.Stone)]
    [InlineData(Block.DirtPath)]
    [InlineData(Block.SoulSand)]
    [InlineData(Block.OakStairs)]
    public void OthersAreNearEnoughToFull(Block block)
        => Assert.False(BlockCollision.HasLowTop(BlockState.Default(block)));

    [Fact]
    public void OnlyTheBottomHalfOfASlabIsLow()
    {
        var slab = BlockState.Default(Block.OakSlab);
        var top = Enumerable.Range(0, 1 << 16)
            .Select(id => BlockState.TryFromId(id, out var state) ? state : null)
            .First(state => state?.Block == Block.OakSlab && state.Get(BlockProperties.Type) == TypeValue.Top)!;

        Assert.Equal(TypeValue.Bottom, slab.Get(BlockProperties.Type));
        Assert.False(BlockCollision.HasLowTop(top));
    }
}
