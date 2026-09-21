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

        Assert.True(world.SetBlock(position, new BlockState(1, "minecraft:stone")));

        Assert.Equal("minecraft:stone", world.GetBlock(position)?.BlockName);
    }

    [Fact]
    public void SetBlock_ClearsABlockWhenGivenNothing()
    {
        var world = Loaded();
        var position = new Vector3i(7, 64, 9);

        world.SetBlock(position, new BlockState(1, "minecraft:stone"));
        world.SetBlock(position, null);

        Assert.Null(world.GetBlock(position));
    }

    [Fact]
    public void SetBlock_ReportsFailureForAChunkThatIsNotLoaded()
    {
        var world = Loaded();

        Assert.False(world.SetBlock(new Vector3i(900, 64, 900), new BlockState(1, "minecraft:stone")));
    }

    [Fact]
    public void SetBlock_ReportsFailureOutsideTheWorldHeight()
    {
        var world = Loaded();

        Assert.False(world.SetBlock(new Vector3i(0, -65, 0), new BlockState(1, "minecraft:stone")));
        Assert.False(world.SetBlock(new Vector3i(0, 320, 0), new BlockState(1, "minecraft:stone")));
    }

    [Fact]
    public void SetBlock_AndGetBlock_SurviveBeingCalledFromSeveralThreads()
    {
        // Chunks arrive on the network thread while the physics loop and the
        // pathfinder read the world from their own.
        var world = Loaded();
        var stone = new BlockState(1, "minecraft:stone");

        Parallel.For(0, 2000, i =>
        {
            var position = new Vector3i(i % 16, 64 + i % 16, i / 16 % 16);

            world.SetBlock(position, stone);
            world.GetBlock(position);
            // Replacing a different chunk, so this does not wipe what the
            // assertion below reads back.
            world.SetChunk(new Vector2i(1 + i % 3, 1 + i % 3), EmptyChunk());
        });

        Assert.Equal("minecraft:stone", world.GetBlock(new Vector3i(0, 64, 0))?.BlockName);
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
