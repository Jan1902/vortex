using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.Networking.PacketHandling;
using Vortex.Modules.Networking.Packets;

namespace Vortex.Modules.Networking;

internal class NetworkingEventHandler(NetworkingConnection connection, NetworkingController controller) : IEventHandler<ConnectionEstablishedEvent>
{
    public async Task HandleAsync(ConnectionEstablishedEvent @event)
    {
        await connection.SendPacket(new HandshakePacket(767, "localhost", 25565, (int)ProtocolState.Login));

        await controller.SetState(ProtocolState.Login);

        await connection.SendPacket(new LoginStartPacket("Jeff", Guid.NewGuid()));
    }
}
