using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Chat;

[AutoSerializedPacket(0x06, ProtocolState.Play, PacketDirection.ServerBound)]
public record ChatMessage(string Message, long Timestamp, long Salt, [Conditional] byte[]? Signature, int MessageCount) : PacketBase;