using Vortex.Data;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.World;

internal class WorldManager : IWorldManager
{
    /// <summary>
    /// How far below y = 0 the first chunk section starts. Section indices are
    /// shifted by this so that the bottom of the world lands on index zero.
    /// </summary>
    private const int SectionsBelowZero = 4;

    private readonly Dictionary<Vector2i, Chunk> _chunks = [];

    /// <summary>
    /// Chunks arrive on the network thread while the physics loop and the
    /// pathfinder read them from their own threads.
    /// </summary>
    private readonly object _chunkLock = new();

    public void SetChunk(Vector2i position, Chunk chunk)
    {
        lock (_chunkLock)
            _chunks[position] = chunk;
    }

    public Chunk? GetChunk(Vector2i position)
    {
        lock (_chunkLock)
            return _chunks.TryGetValue(position, out var value) ? value : null;
    }

    public BlockState? GetBlock(Vector3i position)
    {
        lock (_chunkLock)
        {
            var section = GetSection(position);

            return section?.States[position.X & 0xF, position.Y & 0xF, position.Z & 0xF];
        }
    }

    /// <summary>
    /// Applies a change the server reported, so that the world does not go stale
    /// the moment anything edits it.
    /// </summary>
    /// <param name="position">The block that changed.</param>
    /// <param name="state">Its new state, or null for air.</param>
    /// <returns>
    /// Whether the change could be applied. A change in a chunk that is not
    /// loaded is dropped: there is nothing to apply it to, and the chunk will
    /// arrive with the change already in it.
    /// </returns>
    public bool SetBlock(Vector3i position, BlockState? state)
    {
        lock (_chunkLock)
        {
            var section = GetSection(position);

            if (section is null)
                return false;

            section.States[position.X & 0xF, position.Y & 0xF, position.Z & 0xF] = state;

            return true;
        }
    }

    /// <summary>
    /// The chunk section a block sits in, or null if that part of the world is
    /// not loaded or lies outside the world's height.
    /// </summary>
    /// <remarks>
    /// Callers ask about blocks above and below the one they care about --
    /// the pathfinder looks a block down to find the floor -- so a query just
    /// past the top or bottom of the world is ordinary rather than a mistake,
    /// and answering it with null keeps it from throwing.
    /// </remarks>
    /// <remarks>
    /// Goes through the sections the cube overlaps directly rather than asking
    /// for one block at a time, which would take the lock and look the chunk up
    /// again for every one of them.
    /// </remarks>
    public IReadOnlyList<Vector3i> FindBlocks(Vector3i center, int radius, Func<BlockState, bool> match, int limit = 16)
    {
        var found = new List<(Vector3i Position, int Distance)>();

        lock (_chunkLock)
        {
            for (var chunkX = (center.X - radius) >> 4; chunkX <= (center.X + radius) >> 4; chunkX++)
                for (var chunkZ = (center.Z - radius) >> 4; chunkZ <= (center.Z + radius) >> 4; chunkZ++)
                {
                    if (!_chunks.TryGetValue(new Vector2i(chunkX, chunkZ), out var chunk))
                        continue;

                    for (var index = 0; index < chunk.Sections.Length; index++)
                    {
                        var sectionY = (index - SectionsBelowZero) << 4;

                        if (sectionY + 15 < center.Y - radius || sectionY > center.Y + radius)
                            continue;

                        Scan(chunk.Sections[index], chunkX << 4, sectionY, chunkX, chunkZ);
                    }
                }
        }

        return found.OrderBy(entry => entry.Distance).Take(limit).Select(entry => entry.Position).ToList();

        void Scan(ChunkSection section, int baseX, int baseY, int chunkX, int chunkZ)
        {
            var baseZ = chunkZ << 4;

            for (var x = 0; x < 16; x++)
                for (var y = 0; y < 16; y++)
                    for (var z = 0; z < 16; z++)
                    {
                        var dx = baseX + x - center.X;
                        var dy = baseY + y - center.Y;
                        var dz = baseZ + z - center.Z;

                        if (Math.Abs(dx) > radius || Math.Abs(dy) > radius || Math.Abs(dz) > radius)
                            continue;

                        if (section.States[x, y, z] is { } state && match(state))
                            found.Add((new Vector3i(baseX + x, baseY + y, baseZ + z), dx * dx + dy * dy + dz * dz));
                    }
        }
    }

    private ChunkSection? GetSection(Vector3i position)
    {
        var chunk = _chunks.TryGetValue(new Vector2i(position.X >> 4, position.Z >> 4), out var value)
            ? value
            : null;

        if (chunk is null)
            return null;

        var index = (position.Y >> 4) + SectionsBelowZero;

        return index >= 0 && index < chunk.Sections.Length
            ? chunk.Sections[index]
            : null;
    }
}
