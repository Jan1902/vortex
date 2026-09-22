using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks;

namespace Vortex.Modules.Behaviour.Test;

public class CraftingPlannerTests
{
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
    public void TakesTheRecipeWhoseIngredientsAreAtHand()
    {
        _carried[Item.OakPlanks] = 4;

        Assert.Equal(Recipes.CraftingTable, CraftingPlanner.Choose(Item.CraftingTable, Have, ObtainChain.Empty));
    }

    [Fact]
    public void ChoosesARecipeEvenWithNothingAtHand()
    {
        // Where the ingredients come from is not crafting's business: they are
        // asked for, and some source answers.
        Assert.Equal(Recipes.WoodenPickaxe, CraftingPlanner.Choose(Item.WoodenPickaxe, Have, ObtainChain.Empty));
    }

    [Fact]
    public void PrefersARecipeItHasEverythingFor()
    {
        // Ingots come from a block or from nuggets; with nuggets at hand, those.
        _carried[Item.IronNugget] = 9;

        var recipe = CraftingPlanner.Choose(Item.IronIngot, Have, ObtainChain.Empty);

        Assert.NotNull(recipe);
        Assert.Contains(CraftingPlanner.Needs(recipe), need => need.Ingredient.Matches(Item.IronNugget));
    }

    [Fact]
    public void DoesNotMakeAnythingOutOfWhatIsBeingMadeFurtherUp()
    {
        // Making iron blocks asks for ingots; those must not come from blocks.
        var making = ObtainChain.Empty.With([Item.IronBlock]);

        var recipe = CraftingPlanner.Choose(Item.IronIngot, Have, making);

        Assert.NotNull(recipe);
        Assert.DoesNotContain(CraftingPlanner.Needs(recipe), need => need.Ingredient.Matches(Item.IronBlock));
    }

    [Fact]
    public void FindsNoRecipeThatOnlyGoesRoundInCircles()
    {
        // Nuggets come only from ingots, which are being made further up.
        var making = ObtainChain.Empty.With([Item.IronBlock, Item.IronIngot]);

        Assert.Null(CraftingPlanner.Choose(Item.IronNugget, Have, making));
        Assert.Contains("needs what is being made", CraftingPlanner.Explain(Item.IronNugget));
    }

    [Fact]
    public void ExplainsAnItemWithoutRecipes()
        => Assert.Equal("OakLog cannot be crafted", CraftingPlanner.Explain(Item.OakLog));
}
