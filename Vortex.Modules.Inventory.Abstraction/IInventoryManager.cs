using Vortex.Data;

namespace Vortex.Modules.Inventory.Abstraction;

/// <summary>
/// The player's inventory and whatever container is open, as the server
/// reports them.
/// </summary>
public interface IInventoryManager
{
    /// <summary>The player's own inventory window, kept current even while a container is open.</summary>
    ContainerWindow Player { get; }

    /// <summary>The container open on top of the inventory, or <c>null</c> if none is.</summary>
    ContainerWindow? OpenContainer { get; }

    /// <summary>The stack held on the cursor between clicks, or <c>null</c>.</summary>
    ItemStack? Cursor { get; }

    /// <summary>The selected hotbar position, 0 to 8.</summary>
    int SelectedHotbarSlot { get; }

    /// <summary>The stack in the main hand: the selected hotbar slot.</summary>
    ItemStack? HeldItem { get; }

    /// <summary>How many of an item the player carries, across the main inventory, hotbar and offhand.</summary>
    int Count(Item item);

    /// <summary>
    /// The slots of the player window holding stacks that match, among the main
    /// inventory, hotbar and offhand.
    /// </summary>
    IReadOnlyList<(int Slot, ItemStack Stack)> Find(Func<ItemStack, bool> match);

    /// <summary>
    /// How many more of an item the player can take in: into empty slots, and
    /// onto stacks of it that are not full.
    /// </summary>
    int SpaceFor(Item item);

    /// <summary>
    /// Selects a hotbar slot, which puts what is in it into the main hand.
    /// </summary>
    /// <param name="slot">The hotbar position, 0 to 8.</param>
    Task SelectHotbarSlotAsync(int slot);

    /// <summary>
    /// The window clicks go to: the open container, or the player's inventory.
    /// </summary>
    ContainerWindow ActiveWindow { get; }

    /// <summary>
    /// Where a slot of the player's inventory is in <see cref="ActiveWindow"/>,
    /// which shows the main inventory and hotbar at its end.
    /// </summary>
    /// <returns>The slot, or <c>null</c> if the active window does not show it, as a chest does not show armour.</returns>
    int? ToActiveWindowSlot(int playerSlot);

    /// <summary>
    /// Clicks a slot of <see cref="ActiveWindow"/> and waits for the server to
    /// say what changed.
    /// </summary>
    /// <param name="slot">The slot, or <see cref="ClickSlots.Outside"/> to click outside the window.</param>
    /// <param name="button">Which button, or for some modes which key: see <see cref="ClickMode"/>.</param>
    Task ClickAsync(int slot, int button, ClickMode mode);

    /// <summary>Picks up the stack in a slot onto the cursor, or puts the cursor's down there.</summary>
    Task PickUpAsync(int slot);

    /// <summary>Shift-clicks a slot, moving its stack to the other part of the window.</summary>
    Task QuickMoveAsync(int slot);

    /// <summary>Swaps a slot with a hotbar slot, as pressing its number key over it does.</summary>
    Task SwapWithHotbarAsync(int slot, int hotbarSlot);

    /// <summary>Throws the stack in a slot on the ground, or one item of it.</summary>
    Task DropAsync(int slot, bool wholeStack = true);

    /// <summary>Closes the open container, if any.</summary>
    Task CloseContainerAsync();
}

/// <summary>
/// The ways of clicking a slot, as the protocol numbers them.
/// </summary>
public enum ClickMode
{
    /// <summary>A plain click; button 0 is left, 1 right.</summary>
    PickUp = 0,

    /// <summary>A shift-click; button 0 or 1.</summary>
    QuickMove = 1,

    /// <summary>A number key over the slot; the button is the hotbar position, 0 to 8, or 40 for the offhand.</summary>
    Swap = 2,

    /// <summary>A middle click, copying the stack in creative mode.</summary>
    Clone = 3,

    /// <summary>The drop key over the slot; button 0 drops one, 1 the stack.</summary>
    Throw = 4,

    /// <summary>Dragging across slots.</summary>
    QuickCraft = 5,

    /// <summary>A double click, gathering stacks of the same item onto the cursor.</summary>
    PickUpAll = 6
}

public static class ClickSlots
{
    /// <summary>The slot number of a click outside the window, which drops the cursor's stack.</summary>
    public const int Outside = -999;
}
