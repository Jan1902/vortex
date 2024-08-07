using Vortex.Framework.Abstraction;

namespace Vortex.Modules.Networking.Abstraction;

public interface IPacketHandler<TPacket> : IEventHandler<PacketReceivedEvent<TPacket>>
    where TPacket : PacketBase
{
    Task IEventHandler<PacketReceivedEvent<TPacket>>.HandleAsync(PacketReceivedEvent<TPacket> @event)
        => HandleAsync(@event.Packet);

    Task HandleAsync(TPacket packet);
}
