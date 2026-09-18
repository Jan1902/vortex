using Microsoft.Extensions.Logging;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Player;

internal class PlayerPacketHandler(
    ILogger<PlayerPacketHandler> logger,
    INetworkingManager networking,
    PlayerManager player) : IPacketHandler<SynchronizePlayerPosition>
{
    public async Task HandleAsync(SynchronizePlayerPosition packet)
    {
        player.Synchronize(packet);

        // Movement sent before this is measured against the server's old position,
        // so the loop stays quiet until the acknowledgement is on the wire.
        await networking.SendPacket(new ConfirmTeleportation(packet.TeleportId));

        player.TeleportConfirmed();

        logger.LogDebug("Confirmed teleport {TeleportId}", packet.TeleportId);
    }
}
