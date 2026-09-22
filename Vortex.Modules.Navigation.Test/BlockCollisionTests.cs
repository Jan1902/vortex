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
}
