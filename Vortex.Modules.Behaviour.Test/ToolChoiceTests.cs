using Vortex.Data;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Inventory.Abstraction;

namespace Vortex.Modules.Behaviour.Test;

public class ToolChoiceTests
{
    private readonly FakeInventory _inventory = new();

    [Fact]
    public void TakesAToolThatHarvests()
    {
        _inventory.Hotbar(0, Item.Stone);
        _inventory.Hotbar(2, Item.WoodenPickaxe);

        Assert.Equal((PlayerSlots.Hotbar(2), Item.WoodenPickaxe), ToolChoice.Best(Block.Stone, _inventory));
    }

    [Fact]
    public void TakesTheFasterOfTwoThatHarvest()
    {
        _inventory.Hotbar(3, Item.IronPickaxe);
        _inventory.Hotbar(5, Item.DiamondPickaxe);

        Assert.Equal((PlayerSlots.Hotbar(5), Item.DiamondPickaxe), ToolChoice.Best(Block.Obsidian, _inventory));
    }

    [Fact]
    public void PrefersHarvestingOverSpeed()
    {
        // A wooden pickaxe breaks iron ore faster than a hand, but drops nothing;
        // a stone pickaxe drops the ore.
        _inventory.Hotbar(1, Item.WoodenPickaxe);
        _inventory.Hotbar(4, Item.StonePickaxe);

        Assert.Equal(PlayerSlots.Hotbar(4), ToolChoice.Best(Block.IronOre, _inventory).Slot);
    }

    [Fact]
    public void StaysOnTheSelectedSlotWhenNothingIsBetter()
    {
        _inventory.Hotbar(0, Item.Torch);
        _inventory.SelectHotbarSlotAsync(6);

        Assert.Equal(PlayerSlots.Hotbar(6), ToolChoice.Best(Block.OakPlanks, _inventory).Slot);
    }

    [Fact]
    public void FindsToolsInTheMainInventory()
    {
        _inventory.Hotbar(0, Item.Dirt);
        _inventory.Put(PlayerSlots.MainStart + 5, Item.IronPickaxe);

        Assert.Equal((PlayerSlots.MainStart + 5, Item.IronPickaxe), ToolChoice.Best(Block.Stone, _inventory));
    }
}
