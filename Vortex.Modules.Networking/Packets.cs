using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

[AutoSerializedPacket(0x00)]
public record HandshakePacket(int ProtocolVersion, string ServerAddress, ushort ServerPort, int NextState) : PacketBase;

[AutoSerializedPacket(0x00)]
public record LoginStartPacket(string Name, Guid Uuid) : PacketBase;

[AutoSerializedPacket(0x02)]
public record LoginSuccessPacket(Guid Uuid, string Username, Property[] Properties, bool StrictErrorHandling) : PacketBase;

[PacketModel]
public record Property(string Name, string Value, bool IsSigned, string Signature) : PacketBase;

[AutoSerializedPacket(0x00)]
public record LoginDisconnectPacket(string Reason) : PacketBase;

[AutoSerializedPacket(0x03)]
public record SetCompressionPacket(int Threshold) : PacketBase;

[AutoSerializedPacket(0x03)]
public record LoginAcknowledgedPacket : PacketBase;