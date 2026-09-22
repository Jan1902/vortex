using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Modules.Networking.Data;

namespace Vortex.Modules.Entities.Test;

public class PlayerListTests
{
    private static readonly Guid JanUuid = Guid.NewGuid();

    private readonly EntityManager _entities = new();
    private readonly EntityPacketHandler _handler;

    public PlayerListTests()
        => _handler = new EntityPacketHandler(NullLogger<EntityPacketHandler>.Instance, new RecordingEventBus(), _entities);

    [Fact]
    public void ReadsOnlyThePartsTheActionsName()
    {
        using var stream = new MemoryStream();
        var writer = new MinecraftBinaryWriter(stream);

        writer.WriteByte((byte)(PlayerInfoActions.AddPlayer | PlayerInfoActions.UpdateGameMode | PlayerInfoActions.UpdateLatency));
        writer.WriteVarInt(1);
        writer.WriteUUID(JanUuid);
        writer.WriteStringWithVarIntPrefix("Jan");
        writer.WriteVarInt(1);
        writer.WriteStringWithVarIntPrefix("textures");
        writer.WriteStringWithVarIntPrefix("abc");
        writer.WriteBool(true);
        writer.WriteStringWithVarIntPrefix("signature");
        writer.WriteVarInt((int)GameMode.Creative);
        writer.WriteVarInt(42);

        stream.Position = 0;
        var packet = new PlayerInfoUpdateSerializer().DeserializePacket(new MinecraftBinaryReader(stream));

        var entry = Assert.Single(packet.Entries);
        Assert.Equal(new PlayerInfoEntry(JanUuid, "Jan", GameMode.Creative, null, 42, null), entry);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void SkipsChatSessions()
    {
        using var stream = new MemoryStream();
        var writer = new MinecraftBinaryWriter(stream);

        writer.WriteByte((byte)(PlayerInfoActions.InitializeChat | PlayerInfoActions.UpdateListed));
        writer.WriteVarInt(1);
        writer.WriteUUID(JanUuid);
        writer.WriteBool(true);
        writer.WriteUUID(Guid.NewGuid());
        writer.WriteLong(123);
        writer.WriteVarInt(3);
        writer.WriteBytes([1, 2, 3]);
        writer.WriteVarInt(2);
        writer.WriteBytes([4, 5]);
        writer.WriteBool(true);

        stream.Position = 0;
        var packet = new PlayerInfoUpdateSerializer().DeserializePacket(new MinecraftBinaryReader(stream));

        Assert.True(Assert.Single(packet.Entries).Listed);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public async Task FindsAPlayersEntityByName()
    {
        await _handler.HandleAsync(new PlayerInfoUpdate(PlayerInfoActions.AddPlayer, [new PlayerInfoEntry(JanUuid, "Jan", null, null, null, null)]));
        await _handler.HandleAsync(new AddEntity(9, JanUuid, EntityType.Player, 1, 64, 1, 0, 0, 0, 0, 0, 0, 0));

        Assert.Equal(9, _entities.FindPlayer("jan")!.Id);
        Assert.Equal("Jan", _entities.GetPlayer(JanUuid)!.Name);
    }

    [Fact]
    public async Task KeepsWhatAnUpdateDoesNotCarry()
    {
        await _handler.HandleAsync(new PlayerInfoUpdate(PlayerInfoActions.AddPlayer, [new PlayerInfoEntry(JanUuid, "Jan", null, null, null, null)]));
        await _handler.HandleAsync(new PlayerInfoUpdate(PlayerInfoActions.UpdateLatency, [new PlayerInfoEntry(JanUuid, null, null, null, 80, null)]));

        var jan = _entities.GetPlayer(JanUuid)!;

        Assert.Equal("Jan", jan.Name);
        Assert.Equal(80, jan.Latency);
    }

    [Fact]
    public async Task ForgetsRemovedPlayers()
    {
        await _handler.HandleAsync(new PlayerInfoUpdate(PlayerInfoActions.AddPlayer, [new PlayerInfoEntry(JanUuid, "Jan", null, null, null, null)]));
        await _handler.HandleAsync(new PlayerInfoRemove([JanUuid]));

        Assert.Empty(_entities.Players);
    }

    [Fact]
    public async Task KeepsPlayersAcrossRespawns()
    {
        await _handler.HandleAsync(new PlayerInfoUpdate(PlayerInfoActions.AddPlayer, [new PlayerInfoEntry(JanUuid, "Jan", null, null, null, null)]));
        await _handler.HandleAsync(new Respawn());

        Assert.NotNull(_entities.GetPlayer(JanUuid));
    }
}
