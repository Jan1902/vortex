using Vortex.Data;
using Vortex.Shared;

namespace Vortex.Modules.World.Test;

public class WorldManagerTests
{
    [Fact]
    public void GetBlock_ReturnsNullForAChunkThatIsNotLoaded()
    {
        var world = new WorldManager();

        Assert.Null(world.GetBlock(new Vector3i(0, 64, 0)));
    }

    [Fact]
    public void GetBlock_ReturnsNullAboveAndBelowTheWorldInsteadOfThrowing()
    {
        var world = Loaded();

        // The pathfinder looks a block below the one it is considering, so a
        // query just past the bottom of the world is ordinary rather than a
        // mistake. Standing on bedrock used to throw here.
        Assert.Null(world.GetBlock(new Vector3i(0, -65, 0)));
        Assert.Null(world.GetBlock(new Vector3i(0, -200, 0)));
        Assert.Null(world.GetBlock(new Vector3i(0, 320, 0)));
        Assert.Null(world.GetBlock(new Vector3i(0, 5000, 0)));
    }

    [Fact]
    public void SetBlock_IsVisibleToGetBlock()
    {
        var world = Loaded();
        var position = new Vector3i(7, 64, 9);

        Assert.True(world.SetBlock(position, BlockState.Default(Block.Stone)));

        Assert.Equal(Block.Stone, world.GetBlock(position)?.Block);
    }

    [Fact]
    public void SetBlock_ClearsABlockWhenGivenNothing()
    {
        var world = Loaded();
        var position = new Vector3i(7, 64, 9);

        world.SetBlock(position, BlockState.Default(Block.Stone));
        world.SetBlock(position, null);

        Assert.Null(world.GetBlock(position));
    }

    [Fact]
    public void SetBlock_ReportsFailureForAChunkThatIsNotLoaded()
    {
        var world = Loaded();

        Assert.False(world.SetBlock(new Vector3i(900, 64, 900), BlockState.Default(Block.Stone)));
    }

    [Fact]
    public void SetBlock_ReportsFailureOutsideTheWorldHeight()
    {
        var world = Loaded();

        Assert.False(world.SetBlock(new Vector3i(0, -65, 0), BlockState.Default(Block.Stone)));
        Assert.False(world.SetBlock(new Vector3i(0, 320, 0), BlockState.Default(Block.Stone)));
    }

    [Fact]
    public void SetBlock_AndGetBlock_SurviveBeingCalledFromSeveralThreads()
    {
        // Chunks arrive on the network thread while the physics loop and the
        // pathfinder read the world from their own.
        var world = Loaded();
        var stone = BlockState.Default(Block.Stone);

        Parallel.For(0, 2000, i =>
        {
            var position = new Vector3i(i % 16, 64 + i % 16, i / 16 % 16);

            world.SetBlock(position, stone);
            world.GetBlock(position);
            // Replacing a different chunk, so this does not wipe what the
            // assertion below reads back.
            world.SetChunk(new Vector2i(1 + i % 3, 1 + i % 3), EmptyChunk());
        });

        Assert.Equal(Block.Stone, world.GetBlock(new Vector3i(0, 64, 0))?.Block);
    }

    [Fact]
    public void FindBlocks_ReturnsTheNearestMatchesFirst()
    {
        var world = Loaded();
        world.SetChunk(new Vector2i(-1, 0), EmptyChunk());

        world.SetBlock(new Vector3i(9, 64, 3), BlockState.Default(Block.OakLog));
        world.SetBlock(new Vector3i(2, 65, 3), BlockState.Default(Block.OakLog));
        world.SetBlock(new Vector3i(-3, 64, 3), BlockState.Default(Block.OakLog));
        world.SetBlock(new Vector3i(1, 64, 3), BlockState.Default(Block.Stone));

        var logs = world.FindBlocks(new Vector3i(0, 64, 3), radius: 16, state => state.Block == Block.OakLog);

        Assert.Equal([new Vector3i(2, 65, 3), new Vector3i(-3, 64, 3), new Vector3i(9, 64, 3)], logs);
    }

    [Fact]
    public void FindBlocks_StaysWithinTheRadiusAndTheLimit()
    {
        var world = Loaded();

        for (var x = 1; x <= 10; x++)
            world.SetBlock(new Vector3i(x, -60, 0), BlockState.Default(Block.IronOre));

        // Far below, out of the cube.
        world.SetBlock(new Vector3i(0, 40, 0), BlockState.Default(Block.IronOre));

        var ores = world.FindBlocks(new Vector3i(0, -60, 0), radius: 8, state => state.Block == Block.IronOre, limit: 3);

        Assert.Equal([new Vector3i(1, -60, 0), new Vector3i(2, -60, 0), new Vector3i(3, -60, 0)], ores);
        Assert.Equal(8, world.FindBlocks(new Vector3i(0, -60, 0), radius: 8, state => state.Block == Block.IronOre, limit: 100).Count);
    }

    private static WorldManager Loaded()
    {
        var world = new WorldManager();

        world.SetChunk(new Vector2i(0, 0), EmptyChunk());

        return world;
    }

    private static Chunk EmptyChunk()
        => new(Enumerable.Range(0, 24)
            .Select(_ => new ChunkSection(new BlockState?[16, 16, 16]))
            .ToArray());
}
