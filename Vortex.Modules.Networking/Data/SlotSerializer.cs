using Vortex.Data;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking.Data;

/// <summary>
/// Reads and writes item slots, components included.
/// </summary>
/// <remarks>
/// <para>
/// A component carries no length, so every kind has to be understood just to
/// find where it ends -- one tool with durability would otherwise cut off the
/// rest of an inventory. The layouts follow PrismarineJS's description of the
/// 1.21.1 protocol; the numbering of the kinds is Mojang's own, from the
/// <c>minecraft:data_component_type</c> registry, which <see cref="DataComponentType"/>
/// is generated from.
/// </para>
/// <para>
/// Each component is kept as the bytes it came in, so a stack is written back
/// exactly as it was read without a writer for every kind. The common kinds are
/// decoded on top.
/// </para>
/// </remarks>
internal static class SlotSerializer
{
    public static ItemStack? Read(IMinecraftBinaryReader reader)
    {
        var count = reader.ReadVarInt();
        if (count <= 0)
            return null;

        var item = (Item)reader.ReadVarInt();
        var added = reader.ReadVarInt();
        var removed = reader.ReadVarInt();

        var components = new ItemComponent[added];
        for (var i = 0; i < added; i++)
        {
            var type = (DataComponentType)reader.ReadVarInt();
            object? value = null;
            var data = reader.Capture(r => value = ReadComponent(r, type));

            components[i] = new ItemComponent(type, data, value);
        }

        var removedComponents = new DataComponentType[removed];
        for (var i = 0; i < removed; i++)
            removedComponents[i] = (DataComponentType)reader.ReadVarInt();

        if (added == 0 && removed == 0)
            return new ItemStack(item, count);

        return new ItemStack(item, count) { Components = components, RemovedComponents = removedComponents };
    }

    public static void Write(IMinecraftBinaryWriter writer, ItemStack? stack)
    {
        if (stack is null || stack.Count <= 0)
        {
            writer.WriteVarInt(0);
            return;
        }

        writer.WriteVarInt(stack.Count);
        writer.WriteVarInt((int)stack.Item);
        writer.WriteVarInt(stack.Components.Count);
        writer.WriteVarInt(stack.RemovedComponents.Count);

        foreach (var component in stack.Components)
        {
            writer.WriteVarInt((int)component.Type);
            writer.WriteBytes(component.Data);
        }

        foreach (var type in stack.RemovedComponents)
            writer.WriteVarInt((int)type);
    }

    /// <summary>
    /// Reads one component's data.
    /// </summary>
    /// <returns>The decoded value for the kinds worth decoding, otherwise <c>null</c>.</returns>
    /// <exception cref="NotSupportedException">A kind this version does not have.</exception>
    private static object? ReadComponent(IMinecraftBinaryReader reader, DataComponentType type)
    {
        switch (type)
        {
            case DataComponentType.MaxStackSize or DataComponentType.MaxDamage or DataComponentType.Damage
                or DataComponentType.RepairCost or DataComponentType.CustomModelData or DataComponentType.MapId
                or DataComponentType.MapPostProcessing or DataComponentType.OminousBottleAmplifier or DataComponentType.BaseColor
                or DataComponentType.Rarity:
                return reader.ReadVarInt();

            case DataComponentType.Unbreakable or DataComponentType.EnchantmentGlintOverride:
                return reader.ReadBool();

            case DataComponentType.MapColor:
                return reader.ReadInt();

            case DataComponentType.CustomName or DataComponentType.ItemName:
                return reader.ReadNbtTag();

            case DataComponentType.CustomData or DataComponentType.IntangibleProjectile or DataComponentType.MapDecorations
                or DataComponentType.DebugStickState or DataComponentType.EntityData or DataComponentType.BucketEntityData
                or DataComponentType.BlockEntityData or DataComponentType.Recipes or DataComponentType.Lock
                or DataComponentType.ContainerLoot:
                reader.ReadNbtTag();
                return null;

            case DataComponentType.HideAdditionalTooltip or DataComponentType.HideTooltip
                or DataComponentType.CreativeSlotLock or DataComponentType.FireResistant:
                return null;

            case DataComponentType.Lore:
                Repeat(reader, () => reader.ReadNbtTag());
                return null;

            case DataComponentType.Enchantments or DataComponentType.StoredEnchantments:
                var enchantments = new Dictionary<int, int>();
                Repeat(reader, () => enchantments[reader.ReadVarInt()] = reader.ReadVarInt());
                reader.ReadBool();
                return enchantments;

            case DataComponentType.CanPlaceOn or DataComponentType.CanBreak:
                Repeat(reader, () => ReadBlockPredicate(reader));
                reader.ReadBool();
                return null;

            case DataComponentType.AttributeModifiers:
                Repeat(reader, () =>
                {
                    reader.ReadVarInt();
                    reader.ReadStringWithVarIntPrefix();
                    reader.ReadDouble();
                    reader.ReadVarInt();
                    reader.ReadVarInt();
                });
                reader.ReadBool();
                return null;

            case DataComponentType.Food:
                reader.ReadVarInt();
                reader.ReadFloat();
                reader.ReadBool();
                reader.ReadFloat();
                Read(reader);
                Repeat(reader, () =>
                {
                    reader.ReadVarInt();
                    reader.ReadFloat();
                });
                return null;

            case DataComponentType.Tool:
                Repeat(reader, () =>
                {
                    ReadHolderSet(reader);
                    Optional(reader, () => reader.ReadFloat());
                    Optional(reader, () => reader.ReadBool());
                });
                reader.ReadFloat();
                reader.ReadVarInt();
                return null;

            case DataComponentType.DyedColor:
                reader.ReadInt();
                reader.ReadBool();
                return null;

            case DataComponentType.ChargedProjectiles or DataComponentType.BundleContents or DataComponentType.Container:
                var contents = new List<ItemStack?>();
                Repeat(reader, () => contents.Add(Read(reader)));
                return contents;

            case DataComponentType.PotionContents:
                Optional(reader, () => reader.ReadVarInt());
                Optional(reader, () => reader.ReadInt());
                Repeat(reader, () =>
                {
                    reader.ReadVarInt();
                    ReadEffectDetails(reader);
                });
                Optional(reader, () => reader.ReadStringWithVarIntPrefix());
                return null;

            case DataComponentType.SuspiciousStewEffects:
                Repeat(reader, () =>
                {
                    reader.ReadVarInt();
                    reader.ReadVarInt();
                });
                return null;

            case DataComponentType.WritableBookContent:
                Repeat(reader, () =>
                {
                    reader.ReadStringWithVarIntPrefix();
                    Optional(reader, () => reader.ReadStringWithVarIntPrefix());
                });
                return null;

            case DataComponentType.WrittenBookContent:
                reader.ReadStringWithVarIntPrefix();
                Optional(reader, () => reader.ReadStringWithVarIntPrefix());
                reader.ReadStringWithVarIntPrefix();
                reader.ReadVarInt();
                Repeat(reader, () =>
                {
                    reader.ReadNbtTag();
                    reader.ReadNbtTag();
                });
                reader.ReadBool();
                return null;

            case DataComponentType.Trim:
                ReadHolder(reader, () =>
                {
                    reader.ReadStringWithVarIntPrefix();
                    reader.ReadVarInt();
                    Repeat(reader, () =>
                    {
                        reader.ReadStringWithVarIntPrefix();
                        reader.ReadStringWithVarIntPrefix();
                    });
                    reader.ReadNbtTag();
                });
                ReadHolder(reader, () =>
                {
                    reader.ReadStringWithVarIntPrefix();
                    reader.ReadVarInt();
                    reader.ReadNbtTag();
                    reader.ReadBool();
                });
                reader.ReadBool();
                return null;

            case DataComponentType.Instrument:
                ReadHolder(reader, () =>
                {
                    ReadSoundEvent(reader);
                    reader.ReadFloat();
                    reader.ReadFloat();
                    reader.ReadNbtTag();
                });
                return null;

            case DataComponentType.JukeboxPlayable:
                if (reader.ReadBool())
                {
                    ReadHolder(reader, () =>
                    {
                        ReadSoundEvent(reader);
                        reader.ReadNbtTag();
                        reader.ReadFloat();
                        reader.ReadVarInt();
                    });
                }
                else
                {
                    reader.ReadStringWithVarIntPrefix();
                }

                reader.ReadBool();
                return null;

            case DataComponentType.LodestoneTracker:
                Optional(reader, () =>
                {
                    reader.ReadStringWithVarIntPrefix();
                    reader.ReadPosition();
                });
                reader.ReadBool();
                return null;

            case DataComponentType.FireworkExplosion:
                ReadFireworkExplosion(reader);
                return null;

            case DataComponentType.Fireworks:
                reader.ReadVarInt();
                Repeat(reader, () => ReadFireworkExplosion(reader));
                return null;

            case DataComponentType.Profile:
                Optional(reader, () => reader.ReadStringWithVarIntPrefix());
                Optional(reader, () => reader.ReadUUID());
                Repeat(reader, () =>
                {
                    reader.ReadStringWithVarIntPrefix();
                    reader.ReadStringWithVarIntPrefix();
                    Optional(reader, () => reader.ReadStringWithVarIntPrefix());
                });
                return null;

            case DataComponentType.NoteBlockSound:
                reader.ReadStringWithVarIntPrefix();
                return null;

            case DataComponentType.BannerPatterns:
                Repeat(reader, () =>
                {
                    ReadHolder(reader, () =>
                    {
                        reader.ReadStringWithVarIntPrefix();
                        reader.ReadStringWithVarIntPrefix();
                    });
                    reader.ReadVarInt();
                });
                return null;

            case DataComponentType.PotDecorations:
                Repeat(reader, () => reader.ReadVarInt());
                return null;

            case DataComponentType.BlockState:
                Repeat(reader, () =>
                {
                    reader.ReadStringWithVarIntPrefix();
                    reader.ReadStringWithVarIntPrefix();
                });
                return null;

            case DataComponentType.Bees:
                Repeat(reader, () =>
                {
                    reader.ReadNbtTag();
                    reader.ReadVarInt();
                    reader.ReadVarInt();
                });
                return null;

            default:
                throw new NotSupportedException($"The item component {type} is not known to this version.");
        }
    }

    private static void Repeat(IMinecraftBinaryReader reader, Action read)
    {
        var count = reader.ReadVarInt();

        for (var i = 0; i < count; i++)
            read();
    }

    private static void Optional(IMinecraftBinaryReader reader, Action read)
    {
        if (reader.ReadBool())
            read();
    }

    /// <summary>
    /// A registry entry by ID, sent one higher so that 0 can say the entry follows
    /// written out in full.
    /// </summary>
    private static void ReadHolder(IMinecraftBinaryReader reader, Action readInline)
    {
        if (reader.ReadVarInt() == 0)
            readInline();
    }

    /// <summary>
    /// A set of registry entries: a tag by name when the first number is 0,
    /// otherwise that many IDs, sent one higher.
    /// </summary>
    private static void ReadHolderSet(IMinecraftBinaryReader reader)
    {
        var type = reader.ReadVarInt();

        if (type == 0)
        {
            reader.ReadStringWithVarIntPrefix();
            return;
        }

        for (var i = 0; i < type - 1; i++)
            reader.ReadVarInt();
    }

    private static void ReadBlockPredicate(IMinecraftBinaryReader reader)
    {
        Optional(reader, () => ReadHolderSet(reader));
        Optional(reader, () => Repeat(reader, () =>
        {
            reader.ReadStringWithVarIntPrefix();

            if (reader.ReadBool())
            {
                reader.ReadStringWithVarIntPrefix();
            }
            else
            {
                reader.ReadStringWithVarIntPrefix();
                reader.ReadStringWithVarIntPrefix();
            }
        }));
        reader.ReadNbtTag();
    }

    private static void ReadEffectDetails(IMinecraftBinaryReader reader)
    {
        reader.ReadVarInt();
        reader.ReadVarInt();
        reader.ReadBool();
        reader.ReadBool();
        reader.ReadBool();
        Optional(reader, () => ReadEffectDetails(reader));
    }

    private static void ReadSoundEvent(IMinecraftBinaryReader reader)
        => ReadHolder(reader, () =>
        {
            reader.ReadStringWithVarIntPrefix();
            Optional(reader, () => reader.ReadFloat());
        });

    private static void ReadFireworkExplosion(IMinecraftBinaryReader reader)
    {
        reader.ReadVarInt();
        Repeat(reader, () => reader.ReadInt());
        Repeat(reader, () => reader.ReadInt());
        reader.ReadBool();
        reader.ReadBool();
    }
}
