using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.Networking.Data;

namespace Vortex.Modules.Entities.Test;

public class EntityDataTests
{
    private const int ItemEntityId = 20;
    private const int ZombieId = 21;

    private readonly EntityManager _entities = new();
    private readonly EntityPacketHandler _handler;

    public EntityDataTests()
    {
        _handler = new EntityPacketHandler(NullLogger<EntityPacketHandler>.Instance, new RecordingEventBus(), _entities);
        _handler.HandleAsync(new AddEntity(ItemEntityId, Guid.NewGuid(), EntityType.Item, 0, 64, 0, 0, 0, 0, 1, 0, 0, 0)).Wait();
        _handler.HandleAsync(new AddEntity(ZombieId, Guid.NewGuid(), EntityType.Zombie, 0, 64, 0, 0, 0, 0, 0, 0, 0, 0)).Wait();
    }

    [Fact]
    public void ReadsValuesUntilTheEnd()
    {
        var packet = Read(writer =>
        {
            writer.WriteVarInt(ZombieId);
            Entry(writer, 0, EntityDataType.Byte, () => writer.WriteByte(0x02));
            Entry(writer, 9, EntityDataType.Float, () => writer.WriteFloat(17.5f));
            Entry(writer, 11, EntityDataType.OptionalUnsignedInt, () => writer.WriteVarInt(0));
            Entry(writer, 16, EntityDataType.Boolean, () => writer.WriteBool(true));
            writer.WriteByte(0xFF);
        });

        Assert.True(packet.Complete);
        Assert.Equal([0, 9, 11, 16], packet.Values.Select(value => value.Index));
        Assert.Equal(17.5f, packet.Values[1].Value);
        Assert.Null(packet.Values[2].Value);
        Assert.Equal(true, packet.Values[3].Value);
    }

    [Fact]
    public void ReadsItemStacksWithoutComponents()
    {
        var packet = Read(writer =>
        {
            writer.WriteVarInt(ItemEntityId);
            Entry(writer, 8, EntityDataType.ItemStack, () => Stack(writer, Item.Diamond, 3));
            writer.WriteByte(0xFF);
        });

        Assert.True(packet.Complete);
        Assert.Equal(new ItemStack(Item.Diamond, 3), packet.Values.Single().Value);
    }

    [Fact]
    public void KeepsTheItemButStopsAtComponents()
    {
        var packet = Read(writer =>
        {
            writer.WriteVarInt(ItemEntityId);
            Entry(writer, 8, EntityDataType.ItemStack, () =>
            {
                writer.WriteVarInt(1);
                writer.WriteVarInt((int)Item.DiamondSword);
                writer.WriteVarInt(1);
                writer.WriteVarInt(0);
                writer.WriteBytes([9, 9, 9]);
            });
        });

        Assert.False(packet.Complete);
        Assert.Equal(new ItemStack(Item.DiamondSword, 1, HasComponents: true), packet.Values.Single().Value);
    }

    [Fact]
    public void StopsAtValuesThatCannotBeRead()
    {
        var packet = Read(writer =>
        {
            writer.WriteVarInt(ZombieId);
            Entry(writer, 9, EntityDataType.Float, () => writer.WriteFloat(20f));
            Entry(writer, 10, EntityDataType.Particles, () => writer.WriteBytes([1, 2, 3]));
        });

        Assert.False(packet.Complete);
        Assert.Equal(20f, packet.Values.Single().Value);
    }

    [Fact]
    public async Task NamesTheValuesByEntityType()
    {
        await _handler.HandleAsync(new SetEntityData(ItemEntityId, [new EntityDataValue(8, new ItemStack(Item.Cobblestone, 12))], Complete: true));
        await _handler.HandleAsync(new SetEntityData(ZombieId, [new EntityDataValue(9, 15f)], Complete: true));

        Assert.Equal(new ItemStack(Item.Cobblestone, 12), _entities.Get(ItemEntityId)!.Item);
        Assert.Equal(15f, _entities.Get(ZombieId)!.Health);
        Assert.Null(_entities.Get(ZombieId)!.Item);
    }

    [Fact]
    public async Task MergesUpdatesIntoWhatIsKnown()
    {
        await _handler.HandleAsync(new SetEntityData(ZombieId, [new EntityDataValue(9, 15f), new EntityDataValue(16, true)], Complete: true));
        await _handler.HandleAsync(new SetEntityData(ZombieId, [new EntityDataValue(9, 12f)], Complete: true));

        var zombie = _entities.Get(ZombieId)!;

        Assert.Equal(12f, zombie.Health);
        Assert.True(zombie.TryGetData<bool>(EntityDataKey.Baby, out var baby) && baby);
    }

    private static SetEntityData Read(Action<IMinecraftBinaryWriter> write)
    {
        using var stream = new MemoryStream();
        write(new MinecraftBinaryWriter(stream));
        stream.Position = 0;

        return new SetEntityDataSerializer().DeserializePacket(new MinecraftBinaryReader(stream));
    }

    private static void Entry(IMinecraftBinaryWriter writer, byte index, EntityDataType type, Action value)
    {
        writer.WriteByte(index);
        writer.WriteVarInt((int)type);
        value();
    }

    private static void Stack(IMinecraftBinaryWriter writer, Item item, int count)
    {
        writer.WriteVarInt(count);
        writer.WriteVarInt((int)item);
        writer.WriteVarInt(0);
        writer.WriteVarInt(0);
    }
}
