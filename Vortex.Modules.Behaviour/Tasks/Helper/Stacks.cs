using Vortex.Data;
using Vortex.Modules.Inventory.Abstraction;

namespace Vortex.Modules.Behaviour.Tasks.Helper;

/// <summary>For tasks where any of several items will do: planks of any wood, any pickaxe.</summary>
internal static class Stacks
{
    /// <summary>How many of the items the bot carries, all together.</summary>
    public static int CountIn(IReadOnlySet<Item> items, IInventoryManager inventory)
        => items.Sum(inventory.Count);

    /// <summary>"OakPlanks", or "OakPlanks (or similar)" where others would do too.</summary>
    public static string Describe(IReadOnlySet<Item> items)
        => items.Count == 1 ? items.First().ToString() : $"{items.Order().First()} (or similar)";
}
