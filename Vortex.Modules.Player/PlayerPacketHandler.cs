﻿using Microsoft.Extensions.Logging;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Player;

internal class PlayerPacketHandler(ILogger<PlayerPacketHandler> logger, INetworkingManager networking) : IPacketHandler<SynchronizePlayerPosition>
{
    public async Task HandleAsync(SynchronizePlayerPosition packet)
    {
        logger.LogInformation("Received SynchronizePlayerPosition packet with X: {X}, Y: {Y}, Z: {Z}, Yaw: {Yaw}, Pitch: {Pitch}, Flags: {Flags}, TeleportId: {TeleportId}",
            packet.X, packet.Y, packet.Z, packet.Yaw, packet.Pitch, packet.Flags, packet.TeleportId);

        await networking.SendPacket(new ConfirmTeleportation(packet.TeleportId));
    }
}
