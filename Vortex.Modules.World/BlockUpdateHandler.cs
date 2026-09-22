using Microsoft.Extensions.Logging;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.World.ChunkData.Palettes.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.World;

/// <summary>
/// Keeps the loaded world current as the server edits it.
/// </summary>
/// <remarks>
/// Without this the world is only ever the snapshot that came with the chunk,
/// and everything reading it -- the pathfinder deciding where to walk, the
/// player's own collision deciding what stops it -- works from a picture that
/// quietly ages.
/// </remarks>
internal class BlockUpdateHandler(
    WorldManager world,
    IGlobalPaletteProvider palette,
    ILogger<BlockUpdateHandler> logger)
    : IPacketHandler<BlockUpdate>,
    IPacketHandler<SectionBlocksUpdate>
{
    /// <summary>
    /// A section blocks update packs the block state id into everything above
    /// the low twelve bits of each entry.
    /// </summary>
    private const int PositionBits = 12;

    public Task HandleAsync(BlockUpdate packet)
    {
        Apply(packet.Location, packet.BlockStateId);

        return Task.CompletedTask;
    }

    public Task HandleAsync(SectionBlocksUpdate packet)
    {
        // The section position packs the chunk coordinates and the section's own
        // height into one long.
        var sectionX = (int)(packet.SectionPosition >> 42);
        var sectionY = (int)(packet.SectionPosition << 44 >> 44);
        var sectionZ = (int)(packet.SectionPosition << 22 >> 42);

        foreach (var entry in packet.Blocks)
        {
            var stateId = (int)(entry >>> PositionBits);
            var packedPosition = (int)(entry & 0xFFF);

            var position = new Vector3i(
                (sectionX << 4) + (packedPosition >> 8 & 0xF),
                (sectionY << 4) + (packedPosition & 0xF),
                (sectionZ << 4) + (packedPosition >> 4 & 0xF));

            Apply(position, stateId);
        }

        return Task.CompletedTask;
    }

    private void Apply(Vector3i position, int blockStateId)
    {
        if (!palette.TryGetStateFromId(blockStateId, out var state))
        {
            // An id outside the palette would otherwise be written as air, which
            // is worse than leaving the old block: the bot would walk into it.
            logger.LogWarning("Ignoring block update at {Position}: unknown block state id {Id}", position, blockStateId);

            return;
        }

        if (world.SetBlock(position, state))
            logger.LogDebug("Block at {Position} is now {Block}", position, state);
    }
}
