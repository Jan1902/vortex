using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Behaviour.Tasks.Blocks;
using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Test;

public class PlaceBlockTaskTests
{
    private static readonly Vector3i Target = new(3, 64, 0);

    private readonly FakePlayer _player = new() { Position = new Vector3d(0.5, 64, 0.5) };
    private readonly FakeBlocks _world = new();
    private readonly FakeInventory _inventory = new();
    private readonly FakeInteraction _interaction;

    public PlaceBlockTaskTests()
    {
        // Plays the server for placing: puts cobblestone where a click on a side points.
        _interaction = new FakeInteraction
        {
            OnUseItemOnBlock = (block, face) => _world.Blocks[block + face.Offset()] = BlockState.Default(Block.Cobblestone),
        };

        // Flat ground, empty above.
        for (var x = -2; x <= 6; x++)
            for (var z = -2; z <= 2; z++)
                _world.Blocks[new Vector3i(x, 63, z)] = BlockState.Default(Block.Stone);
    }

    [Fact]
    public async Task PlacesOnTopOfTheBlockBelow()
    {
        _inventory.Put(PlayerSlots.MainStart, Item.Cobblestone, 8);

        var task = Task(Item.Cobblestone);
        var result = await task.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.ToString());
        Assert.True(task.IsSatisfied());

        var click = Assert.Single(_interaction.Clicks);
        Assert.Equal(new Vector3i(3, 63, 0), click.Block);
        Assert.Equal(BlockFace.Up, click.Face);
        Assert.Equal(new Vector3f(0.5f, 1f, 0.5f), click.Cursor);

        // The cobblestone was swapped into the selected hotbar slot first.
        Assert.Contains(_inventory.Clicks, c => c.Mode == ClickMode.Swap && c.Slot == PlayerSlots.MainStart);
    }

    [Fact]
    public async Task LeansAgainstASideWithNothingBelow()
    {
        _world.Blocks.Remove(new Vector3i(3, 63, 0));
        _world.Blocks[new Vector3i(3, 63, 0)] = BlockState.Default(Block.Air);
        _world.Blocks[new Vector3i(3, 64, -1)] = BlockState.Default(Block.OakPlanks);
        _inventory.Hotbar(0, Item.Cobblestone);

        await Task(Item.Cobblestone).ExecuteAsync(CancellationToken.None);

        var click = Assert.Single(_interaction.Clicks);
        Assert.Equal(new Vector3i(3, 64, -1), click.Block);
        Assert.Equal(BlockFace.South, click.Face);
    }

    [Fact]
    public async Task WillNotBuildIntoTheBot()
    {
        _player.Position = new Vector3d(3.5, 64, 0.5);
        _inventory.Hotbar(0, Item.Cobblestone);

        var result = await Task(Item.Cobblestone).ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_interaction.Clicks);
    }

    [Fact]
    public async Task NeedsTheItem()
        => Assert.True((await Task(Item.Cobblestone).ExecuteAsync(CancellationToken.None)).IsFailure);

    private PlaceBlockTask Task(Item item)
        => new(
            item,
            Target,
            _world,
            _player,
            _inventory,
            _interaction,
            _ => null!,
            NullLogger<PlaceBlockTask>.Instance);

    /// <summary>A world of individual blocks; anything not set is air, and loaded.</summary>
    private class FakeBlocks : IWorldManager
    {
        public Dictionary<Vector3i, BlockState> Blocks { get; } = [];

        public BlockState? GetBlock(Vector3i position)
            => Blocks.TryGetValue(position, out var state) ? state : BlockState.Default(Block.Air);

        public Chunk? GetChunk(Vector2i position) => null;
    }
}
