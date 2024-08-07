using Microsoft.Extensions.Logging;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

internal class NetworkingPacketHandler(ILogger<NetworkingPacketHandler> logger, INetworkingConnection connection)
    : IPacketHandler<LoginSuccessPacket>
{
    public async Task HandleAsync(LoginSuccessPacket packet)
    {
        logger.LogInformation("Received login success packet for {Username} with UUID {Uuid}", packet.Username, packet.Uuid);
        await connection.SendPacket(new LoginAcknowledgedPacket());
    }
}
