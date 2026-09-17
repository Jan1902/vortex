using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Networking.Packets;

[AutoSerializedPacket(PacketIds.Configuration.ClientBound.CustomPayload, ProtocolState.Configuration)]
public record PluginMessage(string Channel) : PacketBase;

[AutoSerializedPacket(PacketIds.Configuration.ClientBound.UpdateEnabledFeatures, ProtocolState.Configuration)]
public record FeatureFlags(string[] Features) : PacketBase;

[AutoSerializedPacket(PacketIds.Configuration.ClientBound.SelectKnownPacks, ProtocolState.Configuration)]
public record ClientBoundKnownPacks(KnownPack[] KnownPacks) : PacketBase;

[AutoSerializedPacket(PacketIds.Configuration.ServerBound.SelectKnownPacks, ProtocolState.Configuration, PacketDirection.ServerBound)]
public record ServerBoundKnownPacks(KnownPack[] KnownPacks) : PacketBase;

[PacketModel]
public record KnownPack(string PackNamespace, string PackId, string PackVersion);

[AutoSerializedPacket(PacketIds.Configuration.ClientBound.RegistryData, ProtocolState.Configuration)]
public record RegistryData(string RegistryId, RegistryEntry[] Entries) : PacketBase;

[PacketModel]
public record RegistryEntry(string EntryId, [Conditional] NbtTag? Data);

[AutoSerializedPacket(PacketIds.Configuration.ClientBound.FinishConfiguration, ProtocolState.Configuration)]
public record FinishConfiguration : PacketBase;

[AutoSerializedPacket(PacketIds.Configuration.ServerBound.FinishConfiguration, ProtocolState.Configuration, PacketDirection.ServerBound)]
public record AcknowledgeFinishConfiguration : PacketBase;

[AutoSerializedPacket(PacketIds.Configuration.ClientBound.UpdateTags, ProtocolState.Configuration)]
public record UpdateTags : PacketBase;