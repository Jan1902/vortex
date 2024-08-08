namespace Vortex.Modules.Networking.Abstraction;

public interface INetworkingManager
{
    Task Connect();
    Task ConnectAndWaitForPlay();

    Task SendPacket(PacketBase packet);
}
