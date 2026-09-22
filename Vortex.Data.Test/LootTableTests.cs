namespace Vortex.Data.Test;

/// <summary>
/// Checks what the generated block loot tables of 1.21.1 say can drop.
/// </summary>
public class LootTableTests
{
    private static readonly Dictionary<Enchantment, int> _silkTouch = new() { [Enchantment.SilkTouch] = 1 };

    [Fact]
    public void GeneratesATableForEveryLootTableFile()
        => Assert.Equal(982, Enum.GetValues<Block>().Count(block => LootTables.For(block) is not null));

    [Fact]
    public void StoneDropsCobblestoneUnlessMinedWithSilkTouch()
    {
        var stone = BlockState.Default(Block.Stone);

        Assert.Equal([Item.Cobblestone], LootTables.PossibleDrops(stone, Item.WoodenPickaxe));
        Assert.Equal([Item.Stone], LootTables.PossibleDrops(stone, Item.WoodenPickaxe, _silkTouch));
    }

    [Fact]
    public void OresDropTheirResource()
    {
        var ore = BlockState.Default(Block.DiamondOre);

        Assert.Equal([Item.Diamond], LootTables.PossibleDrops(ore, Item.IronPickaxe));
        Assert.Equal([Item.DiamondOre], LootTables.PossibleDrops(ore, Item.IronPickaxe, _silkTouch));
    }

    [Fact]
    public void ChanceDropsCountAsPossibleAndDoNotEndTheAlternatives()
    {
        // Flint only drops by chance, so gravel is still possible after it.
        var drops = LootTables.PossibleDrops(BlockState.Default(Block.Gravel));

        Assert.Equal(new HashSet<Item> { Item.Flint, Item.Gravel }, drops);
    }

    [Fact]
    public void CropsDropTheirHarvestOnlyWhenGrown()
    {
        var seedling = BlockState.Default(Block.Wheat);
        var grown = BlockState.FromId(4285);

        Assert.Equal(0, seedling.Get(BlockProperties.Age));
        Assert.Equal([Item.WheatSeeds], LootTables.PossibleDrops(seedling));
        Assert.Equal(new HashSet<Item> { Item.Wheat, Item.WheatSeeds }, LootTables.PossibleDrops(grown));
    }

    [Fact]
    public void ShearsTakeTheBlockItselfAndNothingElse()
    {
        var leaves = BlockState.Default(Block.OakLeaves);

        Assert.Equal([Item.OakLeaves], LootTables.PossibleDrops(leaves, Item.Shears));
        Assert.Equal(new HashSet<Item> { Item.OakSapling, Item.Stick, Item.Apple }, LootTables.PossibleDrops(leaves));
    }

    [Fact]
    public void BlocksThatNeedATool()
    {
        // Stone's loot table does not ask for a pickaxe; the game does, before it
        // looks at the table.
        var stone = BlockState.Default(Block.Stone);

        Assert.Empty(LootTables.PossibleDrops(stone));
        Assert.Empty(LootTables.PossibleDrops(BlockState.Default(Block.IronOre), Item.WoodenPickaxe));
        Assert.Equal([Item.RawIron], LootTables.PossibleDrops(BlockState.Default(Block.IronOre), Item.StonePickaxe));
    }

    [Fact]
    public void AirDropsNothing()
        => Assert.Empty(LootTables.PossibleDrops(BlockState.Default(Block.Air)));
}
