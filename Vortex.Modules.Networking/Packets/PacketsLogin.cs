using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking.Packets;

[AutoSerializedPacket(0x00, ProtocolState.Handshake, PacketDirection.ServerBound)]
public record HandshakePacket(int ProtocolVersion, string ServerAddress, ushort ServerPort, int NextState) : PacketBase;

[AutoSerializedPacket(0x00, ProtocolState.Login, PacketDirection.ServerBound)]
public record LoginStartPacket(string Name, Guid Uuid) : PacketBase;

[AutoSerializedPacket(0x02, ProtocolState.Login)]
public record LoginSuccessPacket(Guid Uuid, string Username, Property[] Properties, bool StrictErrorHandling) : PacketBase;

[PacketModel]
public record Property(string Name, string Value, bool IsSigned, string Signature);

[AutoSerializedPacket(0x00, ProtocolState.Login)]
public record LoginDisconnectPacket(string Reason) : PacketBase;

[AutoSerializedPacket(0x03, ProtocolState.Login)]
public record SetCompressionPacket(int Threshold) : PacketBase;

[AutoSerializedPacket(0x03, ProtocolState.Login, PacketDirection.ServerBound)]
public record LoginAcknowledgedPacket : PacketBase;