using Vortex.Data;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.Networking.Data;

namespace Vortex.Modules.Networking.Test;

public class SlotTests
{
    [Fact]
    public void ReadsAnEmptySlot()
        => Assert.Null(Read(writer => writer.WriteVarInt(0)));

    [Fact]
    public void ReadsAPlainStack()
        => Assert.Equal(new ItemStack(Item.Cobblestone, 64), Read(writer => Stack(writer, Item.Cobblestone, 64)));

    [Fact]
    public void DecodesTheCommonComponents()
    {
        var stack = Read(writer =>
        {
            Stack(writer, Item.DiamondPickaxe, 1, added: 3);

            writer.WriteVarInt((int)DataComponentType.Damage);
            writer.WriteVarInt(100);

            writer.WriteVarInt((int)DataComponentType.Enchantments);
            writer.WriteVarInt(2);
            writer.WriteVarInt(5);
            writer.WriteVarInt(3);
            writer.WriteVarInt(9);
            writer.WriteVarInt(1);
            writer.WriteBool(true);

            writer.WriteVarInt((int)DataComponentType.MaxStackSize);
            writer.WriteVarInt(1);
        })!;

        Assert.Equal(100, stack.Damage);
        Assert.Equal(1561, stack.MaxDamage);
        Assert.Equal(new Dictionary<int, int> { [5] = 3, [9] = 1 }, stack.Enchantments);
    }

    [Fact]
    public void GetsPastComponentsItDoesNotDecode()
    {
        using var stream = new MemoryStream();
        var writer = new MinecraftBinaryWriter(stream);

        Stack(writer, Item.IronPickaxe, 1, added: 2, removed: 1);

        // A tool rule list: a tag by name, a speed, no drop override.
        writer.WriteVarInt((int)DataComponentType.Tool);
        writer.WriteVarInt(1);
        writer.WriteVarInt(0);
        writer.WriteStringWithVarIntPrefix("minecraft:mineable/pickaxe");
        writer.WriteBool(true);
        writer.WriteFloat(6f);
        writer.WriteBool(false);
        writer.WriteFloat(1f);
        writer.WriteVarInt(1);

        // Attribute modifiers.
        writer.WriteVarInt((int)DataComponentType.AttributeModifiers);
        writer.WriteVarInt(1);
        writer.WriteVarInt(2);
        writer.WriteStringWithVarIntPrefix("minecraft:base_attack_damage");
        writer.WriteDouble(4);
        writer.WriteVarInt(0);
        writer.WriteVarInt(1);
        writer.WriteBool(true);

        writer.WriteVarInt((int)DataComponentType.Rarity);

        // Something after the slot, which must still be where it is expected.
        writer.WriteVarInt(4242);

        stream.Position = 0;
        var reader = new MinecraftBinaryReader(stream);
        var stack = reader.ReadSlot()!;

        Assert.Equal([DataComponentType.Tool, DataComponentType.AttributeModifiers], stack.Components.Select(c => c.Type));
        Assert.Equal([DataComponentType.Rarity], stack.RemovedComponents);
        Assert.Equal(4242, reader.ReadVarInt());
    }

    [Fact]
    public void WritesAStackBackExactlyAsItWasRead()
    {
        using var original = new MemoryStream();
        var writer = new MinecraftBinaryWriter(original);

        Stack(writer, Item.Bow, 1, added: 2);
        writer.WriteVarInt((int)DataComponentType.Damage);
        writer.WriteVarInt(7);
        writer.WriteVarInt((int)DataComponentType.Lore);
        writer.WriteVarInt(1);
        writer.WriteByte(8);
        writer.WriteStringWithShortPrefix("old");

        original.Position = 0;
        var stack = new MinecraftBinaryReader(original).ReadSlot();

        using var copy = new MemoryStream();
        new MinecraftBinaryWriter(copy).WriteSlot(stack);

        Assert.Equal(original.ToArray(), copy.ToArray());
    }

    private static ItemStack? Read(Action<IMinecraftBinaryWriter> write)
    {
        using var stream = new MemoryStream();
        write(new MinecraftBinaryWriter(stream));
        stream.Position = 0;

        var stack = new MinecraftBinaryReader(stream).ReadSlot();

        Assert.Equal(stream.Length, stream.Position);

        return stack;
    }

    private static void Stack(IMinecraftBinaryWriter writer, Item item, int count, int added = 0, int removed = 0)
    {
        writer.WriteVarInt(count);
        writer.WriteVarInt((int)item);
        writer.WriteVarInt(added);
        writer.WriteVarInt(removed);
    }
}
