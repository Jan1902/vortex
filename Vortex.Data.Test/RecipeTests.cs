namespace Vortex.Data.Test;

/// <summary>
/// Checks the generated recipes against the recipe files of 1.21.1.
/// </summary>
public class RecipeTests
{
    [Fact]
    public void GeneratesEveryRecipe()
    {
        Assert.Equal(1290, Recipes.All.Count);
        Assert.Equal(634, Recipes.All.OfType<ShapedRecipe>().Count());
        Assert.Equal(253, Recipes.All.OfType<ShapelessRecipe>().Count());
        Assert.Equal(250, Recipes.All.OfType<StonecuttingRecipe>().Count());
        Assert.Equal(112, Recipes.All.OfType<CookingRecipe>().Count());
        Assert.Equal(9, Recipes.All.OfType<SmithingTransformRecipe>().Count());
        Assert.Equal(18, Recipes.All.OfType<SmithingTrimRecipe>().Count());
        Assert.Equal(14, Recipes.All.OfType<SpecialRecipe>().Count());
    }

    [Fact]
    public void LaysOutShapedRecipes()
    {
        var bed = Recipes.WhiteBed;

        Assert.Equal(3, bed.Width);
        Assert.Equal(2, bed.Height);
        Assert.True(bed.At(0, 0)!.Matches(Item.WhiteWool));
        Assert.True(bed.At(2, 1)!.Matches(Item.BirchPlanks));
        Assert.False(bed.At(2, 1)!.Matches(Item.WhiteWool));
        Assert.Equal(new RecipeResult(Item.WhiteBed, 1), bed.Result);
    }

    [Fact]
    public void LeavesSpacesInAShapeEmpty()
    {
        var furnace = Recipes.Furnace;

        Assert.Null(furnace.At(1, 1));
        Assert.True(furnace.At(0, 0)!.Matches(Item.Cobblestone));
    }

    [Fact]
    public void ResolvesTagIngredients()
    {
        var planks = Recipes.OakPlanks;

        Assert.Equal(RecipeType.Crafting, planks.Type);
        Assert.Equal(new RecipeResult(Item.OakPlanks, 4), planks.Result);
        Assert.True(planks.Ingredients.Single().Matches(Item.StrippedOakWood));
    }

    [Fact]
    public void KnowsCookingRecipes()
    {
        var ingot = Recipes.IronIngotFromSmeltingIronOre;

        Assert.Equal(RecipeType.Smelting, ingot.Type);
        Assert.Equal(200, ingot.CookingTime);
        Assert.Equal(0.7, ingot.Experience, precision: 5);
        Assert.True(ingot.Ingredient.Matches(Item.IronOre));
    }

    [Fact]
    public void KnowsSmithingAndSpecialRecipes()
    {
        Assert.Equal(Item.NetheriteSword, Recipes.NetheriteSwordSmithing.Result.Item);
        Assert.Null(Recipes.FireworkRocket.Result);
        Assert.Equal(RecipeSerializer.CraftingSpecialFireworkRocket, Recipes.FireworkRocket.Serializer);
    }

    [Fact]
    public void FindsRecipesByWhatTheyMake()
    {
        var sticks = Recipes.Producing(Item.Stick);

        Assert.Contains(Recipes.Stick, sticks);
        Assert.Contains(Recipes.StickFromBambooItem, sticks);
        Assert.Empty(Recipes.Producing(Item.Bedrock));
    }
}
