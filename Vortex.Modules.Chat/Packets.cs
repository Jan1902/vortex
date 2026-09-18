using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Chat;

[AutoSerializedPacket(PacketIds.Play.ServerBound.Chat, packetDirection: PacketDirection.ServerBound)]
public record ChatMessage(string Message, long Timestamp, long Salt, [Conditional] byte[]? Signature, int MessageCount, [BitSet(20)] bool[] Acknowledged) : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.SystemChat)]
public record SystemChatMessage(NbtTag Text, bool Overlay) : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.PlayerChat)] 
public record PlayerChatMessage(Guid Sender, int Index, [Conditional][Length(256)] byte[]? MessageSignature, string Message, long Timestamp, long Salt) : PacketBase;

//[PacketModel]
//public record PreviousMessage(int MessageId, );

[AutoSerializedPacket(PacketIds.Play.ClientBound.Commands)]
public record CommandsPacket : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.DisguisedChat)]
public record DisguisedChatMessage(NbtTag Message, int ChatType, NbtTag SenderName, [Conditional] NbtTag TargetName) : PacketBase;