namespace Vortex.Modules.Networking.Abstraction;

/// <summary>
/// Represents an event that is raised when a connection is established.
/// </summary>
public record ConnectionEstablishedEvent;

/// <summary>
/// Represents an event that is raised when a packet is received.
/// </summary>
/// <typeparam name="TPacket">The type of the received packet.</typeparam>
public record PacketReceivedEvent<TPacket>(TPacket Packet) where TPacket : PacketBase;

/// <summary>
/// Represents an event that is raised when the protocol state changes.
/// </summary>
public record ProtocolStateChanged(ProtocolState State);
