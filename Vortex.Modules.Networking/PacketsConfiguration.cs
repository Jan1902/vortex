using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

[AutoSerializedPacket(0x01, ProtocolState.Configuration)]
public record PluginMessage(string Channel) : PacketBase;

[AutoSerializedPacket(0x0C, ProtocolState.Configuration)]
public record FeatureFlags(string[] Features) : PacketBase;

[AutoSerializedPacket(0x0E, ProtocolState.Configuration)]
public record ClientBoundKnownPacks(KnownPack[] KnownPacks) : PacketBase;

[AutoSerializedPacket(0x07, ProtocolState.Configuration, PacketDirection.ServerBound)]
public record ServerBoundKnownPacks(KnownPack[] KnownPacks) : PacketBase;

[PacketModel]
public record KnownPack(string PackNamespace, string PackId, string PackVersion);

[AutoSerializedPacket(0x07, ProtocolState.Configuration)]
public record RegistryData(string RegistryId, RegistryEntry[] Entries) : PacketBase;

[PacketModel]
public record RegistryEntry(string EntryId, [Conditional] string? Data);

[AutoSerializedPacket(0x03, ProtocolState.Configuration)]
public record FinishConfiguration : PacketBase;

[AutoSerializedPacket(0x03, ProtocolState.Configuration, PacketDirection.ServerBound)]
public record AcknowledgeFinishConfiguration : PacketBase;

[AutoSerializedPacket(0x0D, ProtocolState.Configuration)]
public record UpdateTags : PacketBase;