using Vortex.Data;
using Vortex.Modules.Behaviour.Tasks.Helper;

namespace Vortex.Modules.Behaviour.Test;

/// <summary>
/// Which recipe is taken for an item, from what the bot carries and what is
/// already being fetched for it.
/// </summary>
public class CraftingPlannerTests
{
    private static readonly HashSet<Item> NothingElse = [];

    private readonly Dictionary<Item, int> _carried = [];

    private int Have(Item item) => _carried.GetValueOrDefault(item);

    [Fact]
    public void CountsWhatARecipeUses()
    {
        var needs = CraftingPlanner.Needs(Recipes.Stick);

        var planks = Assert.Single(needs);
        Assert.Equal(2, planks.Count);
        Assert.True(planks.Ingredient.Matches(Item.SprucePlanks));
    }

    [Fact]
    public void ChoosesARecipeEvenWithNothingAtHand()
    {
        // Where the ingredients come from is not crafting's business.
        Assert.Equal(Recipes.WoodenPickaxe, CraftingPlanner.Choose(Item.WoodenPickaxe, Have, NothingElse));
    }

    [Fact]
    public void PrefersARecipeItHasEverythingFor()
    {
        // Ingots come from a block or from nuggets; with nuggets at hand, those.
        _carried[Item.IronNugget] = 9;

        var recipe = CraftingPlanner.Choose(Item.IronIngot, Have, NothingElse);

        Assert.NotNull(recipe);
        Assert.Contains(CraftingPlanner.Needs(recipe), need => need.Ingredient.Matches(Item.IronNugget));
    }

    [Fact]
    public void DoesNotMakeAnythingOutOfWhatIsBeingFetchedForIt()
    {
        // Making iron blocks asks for ingots; those must not come from blocks.
        var making = new HashSet<Item> { Item.IronBlock };

        var recipe = CraftingPlanner.Choose(Item.IronIngot, Have, making);

        Assert.NotNull(recipe);
        Assert.DoesNotContain(CraftingPlanner.Needs(recipe), need => need.Ingredient.Matches(Item.IronBlock));
    }

    [Fact]
    public void FindsNoRecipeThatOnlyGoesRoundInCircles()
    {
        // Nuggets come only from ingots, which are being made further up.
        var making = new HashSet<Item> { Item.IronBlock, Item.IronIngot };

        Assert.Null(CraftingPlanner.Choose(Item.IronNugget, Have, making));
    }

    [Fact]
    public void FindsNoRecipeForSomethingThatIsNotCrafted()
        => Assert.Null(CraftingPlanner.Choose(Item.OakLog, Have, NothingElse));
}
