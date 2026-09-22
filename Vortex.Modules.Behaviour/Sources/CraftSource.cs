using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks;
using Vortex.Modules.Inventory.Abstraction;

namespace Vortex.Modules.Behaviour.Sources;

/// <summary>
/// Gets items by crafting them.
/// </summary>
/// <remarks>
/// Where several kinds would do, the ones whose ingredients are already at hand
/// come first -- planks of the wood that is carried -- and the rest follow in
/// the game's own order. Each kind is its own offer, so that one whose
/// ingredients turn out not to be had anywhere gives way to the next.
/// </remarks>
internal class CraftSource(
    IInventoryManager inventory,
    Func<Item, int, ObtainChain, CraftTask> craft) : IItemSource
{
    public ItemSourceKind Kind => ItemSourceKind.Craft;

    public IEnumerable<ItemSourceOption> Options(ItemRequest request, ObtainChain chain)
    {
        var missing = request.Count - request.CountIn(inventory.Count);

        if (missing <= 0)
            yield break;

        var craftable = request.Items
            .Where(item => !chain.Contains(item))
            .Select(item => (Item: item, Recipe: CraftingPlanner.Choose(item, inventory.Count, chain)))
            .Where(candidate => candidate.Recipe is not null)
            .OrderBy(candidate => AllAtHand(candidate.Recipe!) ? 0 : 1)
            .ThenBy(candidate => candidate.Item);

        foreach (var (item, _) in craftable)
            yield return new ItemSourceOption($"craft {item}", craft(item, inventory.Count(item) + missing, chain));
    }

    private bool AllAtHand(Recipe recipe)
        => CraftingPlanner.Needs(recipe).All(need => CraftingPlanner.Available(need.Ingredient, inventory.Count) >= need.Count);
}
