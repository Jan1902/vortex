using Vortex.Modules.Inventory.Abstraction;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Gets an item into the main hand before acting with it.
/// </summary>
internal static class Hold
{
    /// <summary>
    /// Gets the item in a slot of the player window into the main hand: by
    /// selecting it if it is on the hotbar, otherwise by swapping it into the
    /// selected hotbar slot.
    /// </summary>
    /// <returns>Whether the item could be reached from the window that is open.</returns>
    public static async Task<bool> InMainHandAsync(IInventoryManager inventory, int playerSlot)
    {
        var hotbar = playerSlot - PlayerSlots.HotbarStart;

        if (hotbar is >= 0 and < PlayerSlots.HotbarCount)
        {
            if (hotbar != inventory.SelectedHotbarSlot)
                await inventory.SelectHotbarSlotAsync(hotbar);

            return true;
        }

        if (inventory.ToActiveWindowSlot(playerSlot) is not { } windowSlot)
            return false;

        await inventory.SwapWithHotbarAsync(windowSlot, inventory.SelectedHotbarSlot);

        return true;
    }
}
