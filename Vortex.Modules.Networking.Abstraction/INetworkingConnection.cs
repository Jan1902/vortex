using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking.Abstraction;

public interface INetworkingConnection
{
    Task Connect();
    Task SendPacket(PacketBase packet);
}