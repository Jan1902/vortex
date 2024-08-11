using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking.Packets;

[AutoSerializedPacket(0x26)]
public record ClientBoundKeepAlive(long KeepAliveId) : PacketBase;

[AutoSerializedPacket(0x18, packetDirection: PacketDirection.ServerBound)]
public record ServerBoundKeepAlive(long KeepAliveId) : PacketBase;

[AutoSerializedPacket(0x2b)]
public record LoginPlay : PacketBase;

[AutoSerializedPacket(0x4b)]
public record ServerData : PacketBase;