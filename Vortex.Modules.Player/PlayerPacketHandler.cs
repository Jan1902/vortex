using Microsoft.Extensions.Logging;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Player;

internal class PlayerPacketHandler(
    ILogger<PlayerPacketHandler> logger,
    INetworkingManager networking,
    VortexClientConfiguration configuration,
    PlayerManager player)
    : IPacketHandler<SynchronizePlayerPosition>,
    IPacketHandler<SetHealth>
{
    public async Task HandleAsync(SynchronizePlayerPosition packet)
    {
        player.Synchronize(packet);

        // The server ignores movement until the teleport is acknowledged.
        await networking.SendPacket(new ConfirmTeleportation(packet.TeleportId));

        player.TeleportConfirmed();

        logger.LogDebug("Confirmed teleport {TeleportId}", packet.TeleportId);
    }

    public async Task HandleAsync(SetHealth packet)
    {
        var wasAlive = player.IsAlive;

        player.UpdateHealth(packet.Health);

        if (packet.Health > 0)
            return;

        if (wasAlive)
            logger.LogInformation("Player died");

        if (!configuration.AutoRespawn)
        {
            // Worth saying out loud: a dead player is stuck on the death screen and
            // the server stops sending it chunks, so the bot goes blind until it
            // respawns.
            logger.LogWarning("Player is dead and automatic respawning is off; no world updates will arrive until it respawns");

            return;
        }

        logger.LogInformation("Respawning");

        await networking.SendPacket(new ClientCommand(ClientCommandAction.PerformRespawn));
    }
}
