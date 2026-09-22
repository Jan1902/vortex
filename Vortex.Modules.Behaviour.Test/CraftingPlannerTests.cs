using Vortex.Data;
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

        Assert.Equal(Recipes.CraftingTable, CraftingPlanner.Choose(Item.CraftingTable, Have));
    }

    [Fact]
    public void LooksThroughIngredientsThatCanBeCraftedFirst()
    {
        _carried[Item.BirchLog] = 1;

        Assert.Equal(Recipes.Stick, CraftingPlanner.Choose(Item.Stick, Have));
        Assert.Equal(Item.BirchPlanks, CraftingPlanner.ItemToCraft(Recipes.Stick.Key['#'], Have));
    }

    [Fact]
    public void GoesSeveralStepsDeep()
    {
        // Logs to planks to sticks, and planks for the head.
        _carried[Item.OakLog] = 3;

        Assert.Equal(Recipes.WoodenPickaxe, CraftingPlanner.Choose(Item.WoodenPickaxe, Have));
    }

    [Fact]
    public void FindsNothingWithoutMaterials()
    {
        Assert.Null(CraftingPlanner.Choose(Item.IronPickaxe, Have));
        Assert.Contains("IronIngot", CraftingPlanner.Explain(Item.IronPickaxe, Have));
    }

    [Fact]
    public void DoesNotGoRoundInCircles()
    {
        // Iron ingots come from iron blocks and iron nuggets, which come from ingots.
        Assert.Null(CraftingPlanner.Choose(Item.IronBlock, Have));
    }
}
