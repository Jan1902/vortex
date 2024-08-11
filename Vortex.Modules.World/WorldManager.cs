using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.World;

internal class WorldManager : IWorldManager
{
    private readonly Dictionary<Vector2i, Chunk> _chunks = [];

    public void SetChunk(Vector2i position, Chunk chunk)
        => _chunks[position] = chunk;

    public Chunk? GetChunk(Vector2i position)
        => _chunks.TryGetValue(position, out var value) ? value : null;

    public BlockState? GetBlock(Vector3i position)
        => GetChunk(new Vector2i(position.X >> 4, position.Z >> 4))?
            .Sections[position.Y >> 4]
            .States[position.X & 0xF, position.Y & 0xF, position.Z & 0xF];
}
