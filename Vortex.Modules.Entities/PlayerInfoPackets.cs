using Vortex.Modules.Entities.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Entities;

/// <summary>
/// Which parts of the players' entries a <see cref="PlayerInfoUpdate"/> carries.
/// </summary>
[Flags]
public enum PlayerInfoActions : byte
{
    None = 0,
    AddPlayer = 0x01,
    InitializeChat = 0x02,
    UpdateGameMode = 0x04,
    UpdateListed = 0x08,
    UpdateLatency = 0x10,
    UpdateDisplayName = 0x20
}

/// <summary>
/// Adds players to the tab list or changes their entries.
/// </summary>
/// <remarks>
/// Every entry carries the same parts, those named by <see cref="Actions"/>,
/// which is why it has a serializer of its own.
/// </remarks>
[CustomSerialized<PlayerInfoUpdateSerializer, PlayerInfoUpdate>(PacketIds.Play.ClientBound.PlayerInfoUpdate)]
public record PlayerInfoUpdate(PlayerInfoActions Actions, PlayerInfoEntry[] Entries) : PacketBase;

/// <summary>
/// One player's part of a <see cref="PlayerInfoUpdate"/>. Parts the packet does
/// not carry are <c>null</c>.
/// </summary>
public record PlayerInfoEntry(Guid Uuid, string? Name, GameMode? GameMode, bool? Listed, int? Latency, NbtTag? DisplayName);

/// <summary>Removes players from the tab list.</summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.PlayerInfoRemove)]
public record PlayerInfoRemove(Guid[] Uuids) : PacketBase;

internal class PlayerInfoUpdateSerializer : IPacketSerializer<PlayerInfoUpdate>
{
    public PlayerInfoUpdate DeserializePacket(IMinecraftBinaryReader reader)
    {
        var actions = (PlayerInfoActions)reader.ReadByte();
        var entries = new PlayerInfoEntry[reader.ReadVarInt()];

        for (var i = 0; i < entries.Length; i++)
            entries[i] = ReadEntry(reader, actions);

        return new PlayerInfoUpdate(actions, entries);
    }

    public void SerializePacket(PlayerInfoUpdate packet, IMinecraftBinaryWriter writer)
        => throw new NotSupportedException("Only the server sends player info.");

    private static PlayerInfoEntry ReadEntry(IMinecraftBinaryReader reader, PlayerInfoActions actions)
    {
        var uuid = reader.ReadUUID();
        string? name = null;

        if (actions.HasFlag(PlayerInfoActions.AddPlayer))
        {
            name = reader.ReadStringWithVarIntPrefix();

            // The profile's properties, such as the skin. Read to get past them.
            var properties = reader.ReadVarInt();
            for (var i = 0; i < properties; i++)
            {
                reader.ReadStringWithVarIntPrefix();
                reader.ReadStringWithVarIntPrefix();

                if (reader.ReadBool())
                    reader.ReadStringWithVarIntPrefix();
            }
        }

        // The player's chat signing key, which a bot that does not verify
        // signatures only needs to get past.
        if (actions.HasFlag(PlayerInfoActions.InitializeChat) && reader.ReadBool())
        {
            reader.ReadUUID();
            reader.ReadLong();
            reader.ReadBytes(reader.ReadVarInt());
            reader.ReadBytes(reader.ReadVarInt());
        }

        GameMode? gameMode = actions.HasFlag(PlayerInfoActions.UpdateGameMode) ? (GameMode)reader.ReadVarInt() : null;
        bool? listed = actions.HasFlag(PlayerInfoActions.UpdateListed) ? reader.ReadBool() : null;
        int? latency = actions.HasFlag(PlayerInfoActions.UpdateLatency) ? reader.ReadVarInt() : null;
        var displayName = actions.HasFlag(PlayerInfoActions.UpdateDisplayName) && reader.ReadBool() ? reader.ReadNbtTag() : null;

        return new PlayerInfoEntry(uuid, name, gameMode, listed, latency, displayName);
    }
}
