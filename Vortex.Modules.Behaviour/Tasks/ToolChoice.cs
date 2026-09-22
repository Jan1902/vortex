using Vortex.Data;
using Vortex.Modules.Inventory.Abstraction;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Picks what to break a block with from the hotbar.
/// </summary>
internal static class ToolChoice
{
    /// <summary>
    /// The best hotbar slot for breaking a block: one whose item makes the block
    /// drop its loot if any does, and among those the fastest.
    /// </summary>
    /// <returns>
    /// The hotbar position, and the item in it; <c>null</c> for the item when
    /// the bare hand -- or anything that is no better -- is as good as it gets.
    /// </returns>
    public static (int Slot, Item? Tool) Best(Block block, IInventoryManager inventory)
    {
        var slots = inventory.Player.Slots;
        var selected = inventory.SelectedHotbarSlot;

        var candidates = Enumerable.Range(0, PlayerSlots.HotbarCount)
            .Select(slot => (Slot: slot, Tool: slots[PlayerSlots.Hotbar(slot)]?.Item))
            // Staying on the selected slot wins a tie, so nothing is switched for nothing.
            .OrderBy(candidate => candidate.Slot == selected ? 0 : 1);

        return candidates
            .OrderBy(candidate => Mining.CanHarvest(block, candidate.Tool) ? 0 : 1)
            .ThenBy(candidate => Mining.BreakTicks(block, candidate.Tool) ?? int.MaxValue)
            .First();
    }
}
