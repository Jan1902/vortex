using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking.Packets;

[AutoSerializedPacket(PacketIds.Handshake.ServerBound.Intention, ProtocolState.Handshake, PacketDirection.ServerBound)]
public record HandshakePacket(int ProtocolVersion, string ServerAddress, ushort ServerPort, int NextState) : PacketBase;

[AutoSerializedPacket(PacketIds.Login.ServerBound.Hello, ProtocolState.Login, PacketDirection.ServerBound)]
public record LoginStartPacket(string Name, Guid Uuid) : PacketBase;

[AutoSerializedPacket(PacketIds.Login.ClientBound.GameProfile, ProtocolState.Login)]
public record LoginSuccessPacket(Guid Uuid, string Username, Property[] Properties, bool StrictErrorHandling) : PacketBase;

[PacketModel]
public record Property(string Name, string Value, bool IsSigned, string Signature);

[AutoSerializedPacket(PacketIds.Login.ClientBound.LoginDisconnect, ProtocolState.Login)]
public record LoginDisconnectPacket(string Reason) : PacketBase;

[AutoSerializedPacket(PacketIds.Login.ClientBound.LoginCompression, ProtocolState.Login)]
public record SetCompressionPacket(int Threshold) : PacketBase;

[AutoSerializedPacket(PacketIds.Login.ServerBound.LoginAcknowledged, ProtocolState.Login, PacketDirection.ServerBound)]
public record LoginAcknowledgedPacket : PacketBase;