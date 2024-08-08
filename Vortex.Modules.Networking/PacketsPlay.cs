using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

[AutoSerializedPacket(0x26)]
public record ClientBoundKeepAlive(long KeepAliveId) : PacketBase;

[AutoSerializedPacket(0x18, packetDirection: PacketDirection.ServerBound)]
public record ServerBoundKeepAlive(long KeepAliveId) : PacketBase;