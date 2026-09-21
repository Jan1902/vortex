using Microsoft.Extensions.Logging;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.World.ChunkData;
using Vortex.Shared;

namespace Vortex.Modules.World;

internal class WorldPacketHandler(
    INetworkingManager networking,
    ILogger<WorldPacketHandler> logger,
    ChunkDataHandler chunkDataHandler,
    WorldManager worldManager)
    : IPacketHandler<ChunkBatchStart>,
    IPacketHandler<ChunkDataAndUpdateLight>,
    IPacketHandler<ChunkBatchFinished>
{
    public Task HandleAsync(ChunkBatchStart packet)
    {
        return Task.CompletedTask;
    }

    public Task HandleAsync(ChunkDataAndUpdateLight packet)
    {
        var chunk = chunkDataHandler.HandleChunkData(packet.Data);

        logger.LogDebug("Received ChunkData packet for chunk X: {X} Y: {Z}", packet.ChunkX, packet.ChunkZ);

        worldManager.SetChunk(new Vector2i(packet.ChunkX, packet.ChunkZ), chunk);

        return Task.CompletedTask;
    }

    /// <summary>
    /// How many chunks per tick the client asks the server to send. Parsing a chunk
    /// is cheap for a bot, so this is generous; the server clamps it anyway.
    /// </summary>
    private const float DesiredChunksPerTick = 20f;

    public async Task HandleAsync(ChunkBatchFinished packet)
    {
        await networking.SendPacket(new ChunkBatchReceived(DesiredChunksPerTick));
    }
}
