using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.World.ChunkData.Palettes.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.World.Test;

public class BlockUpdateHandlerTests
{
    private const int StoneId = 1;
    private const int DirtId = 10;

    [Fact]
    public async Task BlockUpdate_ChangesTheBlockTheServerNamed()
    {
        var (handler, world) = Create(loadedChunk: new Vector2i(0, 0));

        await handler.HandleAsync(new BlockUpdate(new Vector3i(3, 70, 5), StoneId));

        Assert.Equal(Block.Stone, world.GetBlock(new Vector3i(3, 70, 5))?.Block);
    }

    [Fact]
    public async Task BlockUpdate_LeavesTheBlocksAroundItAlone()
    {
        var (handler, world) = Create(loadedChunk: new Vector2i(0, 0));

        await handler.HandleAsync(new BlockUpdate(new Vector3i(3, 70, 5), StoneId));

        Assert.Null(world.GetBlock(new Vector3i(4, 70, 5)));
        Assert.Null(world.GetBlock(new Vector3i(3, 71, 5)));
        Assert.Null(world.GetBlock(new Vector3i(3, 70, 6)));
    }

    [Fact]
    public async Task BlockUpdate_WorksInAChunkWithNegativeCoordinates()
    {
        var (handler, world) = Create(loadedChunk: new Vector2i(-1, -1));

        var position = new Vector3i(-5, -20, -13);

        await handler.HandleAsync(new BlockUpdate(position, StoneId));

        Assert.Equal(Block.Stone, world.GetBlock(position)?.Block);
    }

    [Fact]
    public async Task BlockUpdate_IsDroppedForAChunkThatIsNotLoaded()
    {
        var (handler, world) = Create(loadedChunk: new Vector2i(0, 0));

        // Must not throw: updates for chunks the client never received are
        // ordinary, and the chunk will arrive with the change already applied.
        await handler.HandleAsync(new BlockUpdate(new Vector3i(500, 70, 500), StoneId));

        Assert.Null(world.GetBlock(new Vector3i(500, 70, 500)));
    }

    [Fact]
    public async Task BlockUpdate_KeepsTheOldBlockWhenTheStateIsUnknown()
    {
        var (handler, world) = Create(loadedChunk: new Vector2i(0, 0));
        var position = new Vector3i(3, 70, 5);

        await handler.HandleAsync(new BlockUpdate(position, StoneId));
        await handler.HandleAsync(new BlockUpdate(position, 999_999));

        // Writing an unknown id as air would be worse than keeping what is
        // there: the bot would walk into a block it thinks is gone.
        Assert.Equal(Block.Stone, world.GetBlock(position)?.Block);
    }

    [Fact]
    public async Task SectionBlocksUpdate_UnpacksEveryBlockItCarries()
    {
        var (handler, world) = Create(loadedChunk: new Vector2i(0, 0));

        // Section (0, 4, 0) covers y 64 to 79.
        await handler.HandleAsync(new SectionBlocksUpdate(
            SectionPosition(0, 4, 0),
            [
                Entry(StoneId, x: 1, y: 2, z: 3),
                Entry(DirtId, x: 15, y: 0, z: 15),
            ]));

        Assert.Equal(Block.Stone, world.GetBlock(new Vector3i(1, 64 + 2, 3))?.Block);
        Assert.Equal(Block.Dirt, world.GetBlock(new Vector3i(15, 64, 15))?.Block);
    }

    [Fact]
    public async Task SectionBlocksUpdate_HandlesNegativeSectionCoordinates()
    {
        var (handler, world) = Create(loadedChunk: new Vector2i(-1, -1));

        // Section (-1, -4, -1) is the bottom of the world in that chunk, y -64
        // upwards.
        await handler.HandleAsync(new SectionBlocksUpdate(
            SectionPosition(-1, -4, -1),
            [Entry(StoneId, x: 2, y: 5, z: 7)]));

        Assert.Equal(Block.Stone, world.GetBlock(new Vector3i(-16 + 2, -64 + 5, -16 + 7))?.Block);
    }

    [Fact]
    public async Task SectionBlocksUpdate_DoesNotConfuseTheXAndZAxes()
    {
        var (handler, world) = Create(loadedChunk: new Vector2i(0, 0));

        await handler.HandleAsync(new SectionBlocksUpdate(
            SectionPosition(0, 4, 0),
            [Entry(StoneId, x: 1, y: 0, z: 9)]));

        Assert.Equal(Block.Stone, world.GetBlock(new Vector3i(1, 64, 9))?.Block);
        Assert.Null(world.GetBlock(new Vector3i(9, 64, 1)));
    }

    /// <summary>
    /// Packs a section position the way the protocol does: 22 bits of chunk X,
    /// then 22 bits of chunk Z, then 20 bits of section Y.
    /// </summary>
    private static long SectionPosition(int x, int y, int z)
        => ((long)(x & 0x3FFFFF) << 42) | ((long)(z & 0x3FFFFF) << 20) | (uint)(y & 0xFFFFF);

    /// <summary>
    /// Packs one changed block: the state id, then the position within the
    /// section as x, z, y nibbles.
    /// </summary>
    private static long Entry(int blockStateId, int x, int y, int z)
        => ((long)blockStateId << 12) | (uint)(x << 8 | z << 4 | y);

    private static (BlockUpdateHandler Handler, WorldManager World) Create(Vector2i loadedChunk)
    {
        var world = new WorldManager();
        world.SetChunk(loadedChunk, EmptyChunk());

        var handler = new BlockUpdateHandler(
            world,
            new FakePalette(),
            NullLogger<BlockUpdateHandler>.Instance);

        return (handler, world);
    }

    /// <summary>A chunk of nothing but air, twenty-four sections tall.</summary>
    private static Chunk EmptyChunk()
        => new(Enumerable.Range(0, 24)
            .Select(_ => new ChunkSection(new BlockState?[16, 16, 16]))
            .ToArray());

    private class FakePalette : IGlobalPaletteProvider
    {
        private static readonly Dictionary<int, BlockState> _states = new()
        {
            [StoneId] = BlockState.FromId(StoneId),
            [DirtId] = BlockState.FromId(DirtId),
        };

        public BlockState GetStateFromId(int id)
            => _states[id];

        public bool TryGetStateFromId(int id, out BlockState state)
            => _states.TryGetValue(id, out state!);
    }
}
