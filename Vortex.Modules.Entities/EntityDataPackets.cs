using System.Numerics;
using Vortex.Data;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Entities;

/// <summary>
/// Changes some of an entity's metadata: its health, the item it is, its name.
/// </summary>
/// <param name="Complete">
/// Whether every value was read. A value of a kind that cannot be read ends the
/// list early; the values before it are still good.
/// </param>
[CustomSerialized<SetEntityDataSerializer, SetEntityData>(PacketIds.Play.ClientBound.SetEntityData)]
public record SetEntityData(int EntityId, EntityDataValue[] Values, bool Complete) : PacketBase;

/// <summary>
/// One metadata value. What it means depends on the entity type; see
/// <see cref="EntityDataKeys"/>.
/// </summary>
public record EntityDataValue(int Index, object? Value);

/// <summary>
/// The kinds of value entity metadata can hold, by the number the protocol
/// uses for them.
/// </summary>
public enum EntityDataType
{
    Byte = 0,
    Int = 1,
    Long = 2,
    Float = 3,
    String = 4,
    Component = 5,
    OptionalComponent = 6,
    ItemStack = 7,
    Boolean = 8,
    Rotations = 9,
    BlockPosition = 10,
    OptionalBlockPosition = 11,
    Direction = 12,
    OptionalUuid = 13,
    BlockState = 14,
    OptionalBlockState = 15,
    CompoundTag = 16,
    Particle = 17,
    Particles = 18,
    VillagerData = 19,
    OptionalUnsignedInt = 20,
    Pose = 21,
    CatVariant = 22,
    WolfVariant = 23,
    FrogVariant = 24,
    OptionalGlobalPosition = 25,
    PaintingVariant = 26,
    SnifferState = 27,
    ArmadilloState = 28,
    Vector3 = 29,
    Quaternion = 30
}

internal class SetEntityDataSerializer : IPacketSerializer<SetEntityData>
{
    /// <summary>Marks the end of the list, where the next index would be.</summary>
    private const byte EndOfData = 0xFF;

    public SetEntityData DeserializePacket(IMinecraftBinaryReader reader)
    {
        var entityId = reader.ReadVarInt();
        var values = new List<EntityDataValue>();

        while (true)
        {
            var index = reader.ReadByte();
            if (index == EndOfData)
                return new SetEntityData(entityId, [.. values], Complete: true);

            var type = (EntityDataType)reader.ReadVarInt();

            if (!TryReadValue(reader, type, out var value, out var canContinue))
                return new SetEntityData(entityId, [.. values], Complete: false);

            values.Add(new EntityDataValue(index, value));

            if (!canContinue)
                return new SetEntityData(entityId, [.. values], Complete: false);
        }
    }

    public void SerializePacket(SetEntityData packet, IMinecraftBinaryWriter writer)
        => throw new NotSupportedException("Only the server sends entity data.");

    /// <summary>
    /// Reads one value.
    /// </summary>
    /// <param name="canContinue">
    /// Whether the reader stands at the next entry afterwards. It does not after
    /// an item stack with components, whose layouts are not known here; the
    /// stack itself is still returned.
    /// </param>
    /// <returns>Whether the value could be read at all.</returns>
    private static bool TryReadValue(IMinecraftBinaryReader reader, EntityDataType type, out object? value, out bool canContinue)
    {
        canContinue = true;

        switch (type)
        {
            case EntityDataType.Byte:
                value = reader.ReadByte();
                return true;
            case EntityDataType.Int or EntityDataType.Direction or EntityDataType.Pose or EntityDataType.CatVariant
                or EntityDataType.FrogVariant or EntityDataType.SnifferState or EntityDataType.ArmadilloState:
                value = reader.ReadVarInt();
                return true;
            case EntityDataType.Long:
                value = reader.ReadVarLong();
                return true;
            case EntityDataType.Float:
                value = reader.ReadFloat();
                return true;
            case EntityDataType.String:
                value = reader.ReadStringWithVarIntPrefix();
                return true;
            case EntityDataType.Component or EntityDataType.CompoundTag:
                value = reader.ReadNbtTag();
                return true;
            case EntityDataType.OptionalComponent:
                value = reader.ReadBool() ? reader.ReadNbtTag() : null;
                return true;
            case EntityDataType.ItemStack:
                value = ReadItemStack(reader, out canContinue);
                return true;
            case EntityDataType.Boolean:
                value = reader.ReadBool();
                return true;
            case EntityDataType.Rotations or EntityDataType.Vector3:
                value = new Vector3f(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                return true;
            case EntityDataType.BlockPosition:
                value = reader.ReadPosition();
                return true;
            case EntityDataType.OptionalBlockPosition:
                value = reader.ReadBool() ? reader.ReadPosition() : null;
                return true;
            case EntityDataType.OptionalUuid:
                value = reader.ReadBool() ? reader.ReadUUID() : null;
                return true;
            case EntityDataType.BlockState:
                value = BlockState.TryFromId(reader.ReadVarInt(), out var state) ? state : null;
                return true;
            case EntityDataType.OptionalBlockState:
                // 0 is air, which here stands for no block at all.
                var stateId = reader.ReadVarInt();
                value = stateId != 0 && BlockState.TryFromId(stateId, out var optionalState) ? optionalState : null;
                return true;
            case EntityDataType.VillagerData:
                value = new VillagerData(reader.ReadVarInt(), reader.ReadVarInt(), reader.ReadVarInt());
                return true;
            case EntityDataType.OptionalUnsignedInt:
                // Sent one higher, so that 0 can mean absent.
                var number = reader.ReadVarInt();
                value = number == 0 ? null : number - 1;
                return true;
            case EntityDataType.WolfVariant or EntityDataType.PaintingVariant:
                // A registry ID one higher, or 0 followed by the variant written
                // out in full, which is not read here.
                var holder = reader.ReadVarInt();
                value = holder - 1;
                return holder != 0;
            case EntityDataType.OptionalGlobalPosition:
                value = reader.ReadBool() ? new GlobalPosition(reader.ReadStringWithVarIntPrefix(), reader.ReadPosition()) : null;
                return true;
            case EntityDataType.Quaternion:
                value = new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                return true;
            default:
                // Particles, and anything newer: their layout depends on data not
                // modelled here, so nothing after them can be found either.
                value = null;
                return false;
        }
    }

    /// <summary>
    /// Reads an item stack. Its components, such as enchantments, each have a
    /// layout of their own and no length, so a stack that has any ends the list.
    /// </summary>
    private static ItemStack? ReadItemStack(IMinecraftBinaryReader reader, out bool canContinue)
    {
        canContinue = true;

        var count = reader.ReadVarInt();
        if (count <= 0)
            return null;

        var item = (Item)reader.ReadVarInt();
        var added = reader.ReadVarInt();
        var removed = reader.ReadVarInt();

        if (added > 0)
        {
            canContinue = false;

            return new ItemStack(item, count, HasComponents: true);
        }

        // Removed components are only listed by type, which can be read past.
        for (var i = 0; i < removed; i++)
            reader.ReadVarInt();

        return new ItemStack(item, count, HasComponents: removed > 0);
    }
}
