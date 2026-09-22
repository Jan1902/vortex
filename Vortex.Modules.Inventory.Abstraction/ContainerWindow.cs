using System.Collections.Immutable;
using Vortex.Data;

namespace Vortex.Modules.Inventory.Abstraction;

/// <summary>
/// A window of slots the server keeps in sync with the client: the player's own
/// inventory, or a container opened on top of it, such as a chest.
/// </summary>
/// <remarks>
/// A snapshot that never changes; each update replaces it. A container's last
/// 36 slots are the player's main inventory and hotbar, the same items the
/// player window shows.
/// </remarks>
/// <param name="Id">The window's number; 0 is the player's inventory.</param>
/// <param name="Type">What kind of container it is, or <c>null</c> for the player's inventory.</param>
/// <param name="StateId">
/// The server's revision of the window's contents, which clicks have to name
/// so the server can tell whether the client was up to date.
/// </param>
/// <param name="Slots">The slots, <c>null</c> where empty.</param>
/// <param name="Properties">
/// Numbers the window shows besides its slots, such as a furnace's progress,
/// by the index the server gives them.
/// </param>
public sealed record ContainerWindow(
    int Id,
    Menu? Type,
    int StateId,
    ImmutableArray<ItemStack?> Slots,
    ImmutableDictionary<int, int> Properties)
{
    /// <summary>The ID of the player's own inventory window.</summary>
    public const int PlayerWindowId = 0;

    /// <summary>How many of a window's slots, at its end, are the player's main inventory and hotbar.</summary>
    public const int PlayerInventorySlots = 36;

    /// <summary>How many slots belong to the container itself rather than to the player.</summary>
    public int ContainerSize => Id == PlayerWindowId ? Slots.Length : Slots.Length - PlayerInventorySlots;
}

/// <summary>
/// Where things are in the player's inventory window.
/// </summary>
public static class PlayerSlots
{
    public const int CraftingResult = 0;
    public const int CraftingGridStart = 1;
    public const int CraftingGridSize = 4;
    public const int Head = 5;
    public const int Chest = 6;
    public const int Legs = 7;
    public const int Feet = 8;
    public const int MainStart = 9;
    public const int MainCount = 27;
    public const int HotbarStart = 36;
    public const int HotbarCount = 9;
    public const int Offhand = 45;
    public const int Count = 46;

    /// <summary>The window slot of a hotbar position, 0 to 8.</summary>
    public static int Hotbar(int index) => HotbarStart + index;

    /// <summary>Every slot items are carried in: the main inventory, the hotbar and the offhand.</summary>
    public static IEnumerable<int> Storage
        => Enumerable.Range(MainStart, MainCount + HotbarCount).Append(Offhand);
}
