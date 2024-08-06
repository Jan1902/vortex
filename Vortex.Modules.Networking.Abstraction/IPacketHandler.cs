namespace Vortex.Modules.Networking.Abstraction;

public interface IPacketHandler<TPacket> where TPacket : PacketBase
{
    Task HandleAsync(TPacket packet);
}
