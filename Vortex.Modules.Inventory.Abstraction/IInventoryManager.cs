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
}
