namespace Vortex.Modules.Networking.Abstraction;

public record ConnectionEstablishedEvent;

public record PacketReceivedEvent<TPacket>(TPacket Packet) where TPacket : PacketBase;