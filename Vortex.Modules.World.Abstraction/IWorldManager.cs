using Vortex.Shared;

namespace Vortex.Modules.World.Abstraction;

public interface IWorldManager
{
    /// <summary>
    /// Gets the block at the specified position.
    /// </summary>
    /// <param name="position">The position of the block.</param>
    /// <returns>The block state at the specified position, or null if no block exists.</returns>
    public BlockState? GetBlock(Vector3i position);

    /// <summary>
    /// Gets the chunk at the specified position.
    /// </summary>
    /// <param name="position">The position of the chunk.</param>
    /// <returns>The chunk at the specified position, or null if no chunk exists.</returns>
    public Chunk? GetChunk(Vector2i position);
}
