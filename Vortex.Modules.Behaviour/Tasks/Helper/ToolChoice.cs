using Vortex.Data;
using Vortex.Modules.Inventory.Abstraction;

namespace Vortex.Modules.Behaviour.Tasks.Helper;

/// <summary>
/// Picks what to break a block with from what the bot carries.
/// </summary>
internal static class ToolChoice
{
    /// <summary>
    /// The best thing to break a block with: one that makes the block drop its
    /// loot if anything does, and among those the fastest.
    /// </summary>
    /// <remarks>
    /// Every hotbar slot counts, empty ones as the bare hand, and every tool in
    /// the main inventory. On a tie the selected slot wins, then the rest of the
    /// hotbar, so nothing is switched or moved for nothing.
    /// </remarks>
    /// <returns>
    /// The slot of the player window, and the item in it; <c>null</c> for the
    /// item when the bare hand, or anything no better, is as good as it gets.
    /// </returns>
    public static (int Slot, Item? Tool) Best(Block block, IInventoryManager inventory)
    {
        var slots = inventory.Player.Slots;
        var selected = PlayerSlots.Hotbar(inventory.SelectedHotbarSlot);

        var hotbar = Enumerable.Range(PlayerSlots.HotbarStart, PlayerSlots.HotbarCount)
            .OrderBy(slot => slot == selected ? 0 : 1);

        var mainTools = Enumerable.Range(PlayerSlots.MainStart, PlayerSlots.MainCount)
            .Where(slot => slots[slot]?.Item.Tool() is not null);

        return hotbar.Concat(mainTools)
            .Select(slot => (Slot: slot, Tool: slots[slot]?.Item))
            .OrderBy(candidate => Mining.CanHarvest(block, candidate.Tool) ? 0 : 1)
            .ThenBy(candidate => Mining.BreakTicks(block, candidate.Tool) ?? int.MaxValue)
            .First();
    }
}
