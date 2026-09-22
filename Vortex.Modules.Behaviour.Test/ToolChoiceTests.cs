using Vortex.Data;
using Vortex.Modules.Behaviour.Tasks;

namespace Vortex.Modules.Behaviour.Test;

public class ToolChoiceTests
{
    private readonly FakeInventory _inventory = new();

    [Fact]
    public void TakesAToolThatHarvests()
    {
        _inventory.Hotbar(0, Item.Stone);
        _inventory.Hotbar(2, Item.WoodenPickaxe);

        Assert.Equal((2, Item.WoodenPickaxe), ToolChoice.Best(Block.Stone, _inventory));
    }

    [Fact]
    public void TakesTheFasterOfTwoThatHarvest()
    {
        _inventory.Hotbar(3, Item.IronPickaxe);
        _inventory.Hotbar(5, Item.DiamondPickaxe);

        Assert.Equal((5, Item.DiamondPickaxe), ToolChoice.Best(Block.Obsidian, _inventory));
    }

    [Fact]
    public void PrefersHarvestingOverSpeed()
    {
        // A wooden pickaxe breaks iron ore faster than a hand, but drops nothing;
        // a stone pickaxe drops the ore.
        _inventory.Hotbar(1, Item.WoodenPickaxe);
        _inventory.Hotbar(4, Item.StonePickaxe);

        Assert.Equal(4, ToolChoice.Best(Block.IronOre, _inventory).Slot);
    }

    [Fact]
    public void StaysOnTheSelectedSlotWhenNothingIsBetter()
    {
        _inventory.Hotbar(0, Item.Torch);
        _inventory.SelectHotbarSlotAsync(6);

        Assert.Equal(6, ToolChoice.Best(Block.OakPlanks, _inventory).Slot);
    }
}
