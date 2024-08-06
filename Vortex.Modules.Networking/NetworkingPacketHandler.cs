using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

internal class NetworkingPacketHandler : IPacketHandler<HandshakePacket>
{
    public async Task HandleAsync(HandshakePacket packet)
    {
        
    }
}
