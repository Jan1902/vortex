using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Chat;

[AutoSerializedPacket(0x06, packetDirection: PacketDirection.ServerBound)]
public record ChatMessage(string Message, long Timestamp, long Salt, [Conditional] byte[]? Signature, int MessageCount, [BitSet(20)] bool[] Acknowledged) : PacketBase;

[AutoSerializedPacket(0x6C)]
public record SystemChatMessage(string Text, bool Overlay) : PacketBase;

[AutoSerializedPacket(0x39)] 
public record PlayerChatMessage(Guid Sender, int Index, [Conditional][Length(256)] byte[]? MessageSignature, string Message, long Timestamp, long Salt) : PacketBase;

//[PacketModel]
//public record PreviousMessage(int MessageId, );

[AutoSerializedPacket(0x11)]
public record CommandsPacket : PacketBase;

[AutoSerializedPacket(0x1e)]
public record DisguisedChatMessage(NbtTag Message, int ChatType, NbtTag SenderName, [Conditional] NbtTag TargetName) : PacketBase;