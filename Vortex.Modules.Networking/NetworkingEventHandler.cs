﻿using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

internal class NetworkingEventHandler(INetworkingConnection connection, NetworkingController controller) : IEventHandler<ConnectionEstablishedEvent>
{
    public async Task HandleAsync(ConnectionEstablishedEvent @event)
    {
        await connection.SendPacket(new HandshakePacket(767, "localhost", 25565, (int)ProtocolState.Login));
        controller.SetState(ProtocolState.Login);
        await connection.SendPacket(new LoginStartPacket("Jeff", Guid.NewGuid()));
    }
}
