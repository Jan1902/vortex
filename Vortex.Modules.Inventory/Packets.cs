using Vortex.Data;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Inventory;

/// <summary>
/// The whole contents of a window, and the stack on the cursor.
/// </summary>
/// <param name="WindowId">The window; 0 is the player's inventory.</param>
/// <param name="StateId">The server's revision of the window, for clicks to name.</param>
[AutoSerializedPacket(PacketIds.Play.ClientBound.ContainerSetContent)]
public record SetContainerContent(byte WindowId, int StateId, ItemStack?[] Slots, ItemStack? Carried) : PacketBase;

/// <summary>
/// One slot of a window changed.
/// </summary>
/// <param name="WindowId">
/// The window, or one of two special values: <see cref="CursorWindow"/> sets
/// the stack on the cursor, <see cref="PlayerInventoryWindow"/> a slot of the
/// player's inventory counted the way the inventory itself counts them.
/// </param>
[AutoSerializedPacket(PacketIds.Play.ClientBound.ContainerSetSlot)]
public record SetContainerSlot(byte WindowId, int StateId, short Slot, ItemStack? Item) : PacketBase
{
    /// <summary>-1 as a byte: the slot is the cursor.</summary>
    public const byte CursorWindow = 255;

    /// <summary>-2 as a byte: the slot is one of the player's inventory, by inventory index.</summary>
    public const byte PlayerInventoryWindow = 254;
}

/// <summary>A number a window shows besides its slots changed, such as a furnace's progress.</summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.ContainerSetData)]
public record SetContainerData(byte WindowId, short Property, short Value) : PacketBase;

/// <summary>A container opened on top of the inventory. Its contents follow.</summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.OpenScreen)]
public record OpenScreen(int WindowId, Menu Type, NbtTag Title) : PacketBase;

/// <summary>The server closed a container.</summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.ContainerClose)]
public record ClientBoundContainerClose(byte WindowId) : PacketBase;

/// <summary>Selects a hotbar slot.</summary>
[AutoSerializedPacket(PacketIds.Play.ServerBound.SetCarriedItem, packetDirection: PacketDirection.ServerBound)]
public record ServerBoundSetCarriedItem(short Slot) : PacketBase;

/// <summary>The server selected a hotbar slot.</summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.SetCarriedItem)]
public record ClientBoundSetCarriedItem(byte Slot) : PacketBase;
