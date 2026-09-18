using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking.Packets;

[AutoSerializedPacket(PacketIds.Play.ClientBound.KeepAlive)]
public record ClientBoundKeepAlive(long KeepAliveId) : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ServerBound.KeepAlive, packetDirection: PacketDirection.ServerBound)]
public record ServerBoundKeepAlive(long KeepAliveId) : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.Login)]
public record LoginPlay : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.ServerData)]
public record ServerData : PacketBase;