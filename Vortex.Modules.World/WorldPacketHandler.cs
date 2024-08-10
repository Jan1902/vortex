using Microsoft.Extensions.Logging;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.World;

internal class WorldPacketHandler(INetworkingManager networking, ILogger<WorldPacketHandler> logger)
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
        logger.LogInformation("Received ChunkData packet for chunk X: {X} Y: {Z}", packet.ChunkX, packet.ChunkZ);

        return Task.CompletedTask;
    }

    public async Task HandleAsync(ChunkBatchFinished packet)
    {
        await networking.SendPacket(new ChunkBatchReceived(1 / 4));
    }
}
