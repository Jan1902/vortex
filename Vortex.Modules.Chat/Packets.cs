using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Chat;

[AutoSerializedPacket(0x06, packetDirection: PacketDirection.ServerBound)]
public record ChatMessage(string Message, long Timestamp, long Salt, [Conditional] byte[]? Signature, int MessageCount, [BitSet(20)] bool[] Acknowledged) : PacketBase;

[AutoSerializedPacket(0x6C)]
public record SystemChatMessage(string Text, bool Overlay) : PacketBase;

[AutoSerializedPacket(0x39)] 
public record PlayerChatMessage : PacketBase;

[AutoSerializedPacket(0x11)]
public record CommandsPacket : PacketBase;