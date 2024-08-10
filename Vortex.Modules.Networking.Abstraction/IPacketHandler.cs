using Vortex.Framework.Abstraction;

namespace Vortex.Modules.Networking.Abstraction;

/// <summary>
/// Represents a packet handler that handles packets of type <typeparamref name="TPacket"/>.
/// </summary>
/// <typeparam name="TPacket">The type of the packet to handle.</typeparam>
public interface IPacketHandler<TPacket> : IEventHandler<PacketReceivedEvent<TPacket>>
    where TPacket : PacketBase
{
    Task IEventHandler<PacketReceivedEvent<TPacket>>.HandleAsync(PacketReceivedEvent<TPacket> @event)
        => HandleAsync(@event.Packet);

    /// <summary>
    /// Handles the specified packet asynchronously.
    /// </summary>
    /// <param name="packet">The packet to handle.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleAsync(TPacket packet);
}
