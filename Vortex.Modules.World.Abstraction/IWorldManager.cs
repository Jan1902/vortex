using Vortex.Data;
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

    /// <summary>
    /// Finds the loaded blocks that match, nearest to a position first.
    /// </summary>
    /// <remarks>
    /// Looks through a cube around the position, so it is only as good as what
    /// has been loaded there. Meant for finding things to go to -- the nearest
    /// log, the nearest ore -- not for scanning the world every tick.
    /// </remarks>
    /// <param name="center">Where to measure from.</param>
    /// <param name="radius">How far to look in every direction, in blocks.</param>
    /// <param name="match">Which blocks count.</param>
    /// <param name="limit">How many to return at most.</param>
    /// <returns>The positions of the matching blocks, nearest first.</returns>
    public IReadOnlyList<Vector3i> FindBlocks(Vector3i center, int radius, Func<BlockState, bool> match, int limit = 16)
    {
        var found = new List<(Vector3i Position, int Distance)>();

        for (var x = -radius; x <= radius; x++)
            for (var y = -radius; y <= radius; y++)
                for (var z = -radius; z <= radius; z++)
                {
                    var position = new Vector3i(center.X + x, center.Y + y, center.Z + z);

                    if (GetBlock(position) is { } state && match(state))
                        found.Add((position, x * x + y * y + z * z));
                }

        return found.OrderBy(entry => entry.Distance).Take(limit).Select(entry => entry.Position).ToList();
    }
}
