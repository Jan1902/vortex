using Vortex.Data;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// A world built block by block, so a test can describe exactly the terrain it
/// is about.
/// </summary>
/// <remarks>
/// Everything is air unless it was made solid or left unloaded. Unloaded
/// positions return null, which is how a chunk that has not arrived looks to the
/// pathfinder.
/// </remarks>
internal class FakeWorld : IWorldManager
{
    private static readonly BlockState _stone = BlockState.Default(Block.Stone);
    private static readonly BlockState _air = BlockState.Default(Block.Air);

    private readonly HashSet<Vector3i> _solid = [];
    private readonly HashSet<Vector3i> _unloaded = [];
    private readonly Dictionary<Vector3i, BlockState> _named = [];

    /// <summary>
    /// The chunk columns this world has been told about.
    /// </summary>
    /// <remarks>
    /// Anywhere else reads back as null, the way an unloaded chunk does. The
    /// real client only ever has a window on the world, and code that plans
    /// routes has to behave sensibly at the edge of it.
    /// </remarks>
    private readonly HashSet<Vector2i> _loaded = [];

    public BlockState? GetBlock(Vector3i position)
    {
        if (_unloaded.Contains(position))
            return null;

        if (!_loaded.Contains(new Vector2i(position.X >> 4, position.Z >> 4)))
            return null;

        if (_named.TryGetValue(position, out var named))
            return named;

        return _solid.Contains(position) ? _stone : _air;
    }

    /// <summary>Marks the chunk a block sits in as having arrived.</summary>
    private void Load(int x, int z)
        => _loaded.Add(new Vector2i(x >> 4, z >> 4));

    public Chunk? GetChunk(Vector2i position)
        => throw new NotSupportedException("The pathfinder reads single blocks.");

    /// <summary>Lays a solid slab, which the player stands one block above.</summary>
    public FakeWorld WithFloor(int y, int fromX, int toX, int fromZ, int toZ)
    {
        for (var x = fromX; x <= toX; x++)
            for (var z = fromZ; z <= toZ; z++)
            {
                _solid.Add(new Vector3i(x, y, z));
                Load(x, z);
            }

        return this;
    }

    /// <summary>Puts up a solid wall, two blocks high by default.</summary>
    public FakeWorld WithWall(int x, int y, int fromZ, int toZ, int height = 2)
    {
        for (var z = fromZ; z <= toZ; z++)
        {
            Load(x, z);

            for (var offset = 0; offset < height; offset++)
                _solid.Add(new Vector3i(x, y + offset, z));
        }

        return this;
    }

    public FakeWorld WithBlock(Vector3i position)
    {
        _solid.Add(position);
        Load(position.X, position.Z);

        return this;
    }

    /// <summary>Puts a named block somewhere, for the ones that are not stone.</summary>
    public FakeWorld With(Vector3i position, Block block)
    {
        _named[position] = BlockState.Default(block);
        Load(position.X, position.Z);

        return this;
    }

    /// <summary>Fills a run of blocks with something named, such as a lava channel.</summary>
    public FakeWorld WithPool(Block block, int y, int fromX, int toX, int fromZ, int toZ)
    {
        for (var x = fromX; x <= toX; x++)
            for (var z = fromZ; z <= toZ; z++)
            {
                _named[new Vector3i(x, y, z)] = BlockState.Default(block);
                Load(x, z);
            }

        return this;
    }

    /// <summary>Takes a slice of the world away, as an unloaded chunk would.</summary>
    public FakeWorld WithUnloaded(int x, int fromY, int toY, int fromZ, int toZ)
    {
        for (var y = fromY; y <= toY; y++)
            for (var z = fromZ; z <= toZ; z++)
                _unloaded.Add(new Vector3i(x, y, z));

        return this;
    }
}
