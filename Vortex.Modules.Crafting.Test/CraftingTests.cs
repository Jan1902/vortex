using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using Vortex.Data;
using Vortex.Modules.Crafting.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Crafting.Test;

public class CraftingTests
{
    private readonly FakeInventory _inventory = new();
    private readonly CraftingManager _crafting;

    public CraftingTests()
        => _crafting = new CraftingManager(new NoNetworking(), _inventory, NullLogger<CraftingManager>.Instance);

    [Fact]
    public void CraftsInTheInventoryWhenNothingIsOpen()
        => Assert.Equal(new CraftingGrid(0, 2), _crafting.ActiveGrid);

    [Fact]
    public void CraftsAtAnOpenCraftingTable()
    {
        _inventory.Open = Window(4, Menu.Crafting, 10 + 36);

        Assert.Equal(new CraftingGrid(4, 3), _crafting.ActiveGrid);
    }

    [Fact]
    public void HasNoGridWhileAChestIsOpen()
    {
        _inventory.Open = Window(4, Menu.Generic9x3, 27 + 36);

        Assert.Null(_crafting.ActiveGrid);
    }

    [Fact]
    public void KnowsWhichRecipesFitWhichGrid()
    {
        var small = new CraftingGrid(0, 2);
        var large = new CraftingGrid(4, 3);

        Assert.True(_crafting.Fits(Recipes.Stick, small));
        Assert.True(_crafting.Fits(Recipes.CraftingTable, small));
        Assert.False(_crafting.Fits(Recipes.WoodenPickaxe, small));
        Assert.True(_crafting.Fits(Recipes.WoodenPickaxe, large));
        Assert.False(_crafting.Fits(Recipes.IronIngotFromSmeltingIronOre, large));
    }

    [Fact]
    public void KeepsTheRecipeBook()
    {
        _crafting.UpdateRecipeBook(RecipeBookAction.Init, ["minecraft:stick", "minecraft:torch"]);
        _crafting.UpdateRecipeBook(RecipeBookAction.Add, ["minecraft:crafting_table"]);
        _crafting.UpdateRecipeBook(RecipeBookAction.Remove, ["minecraft:torch"]);

        Assert.Equal(new HashSet<string> { "minecraft:stick", "minecraft:crafting_table" }, _crafting.UnlockedRecipes);
    }

    [Fact]
    public async Task DoesNotCraftWhatDoesNotFit()
        => Assert.False(await _crafting.CraftAsync(Recipes.WoodenPickaxe));

    private static ContainerWindow Window(int id, Menu type, int size)
        => new(id, type, 0, [.. new ItemStack?[size]], ImmutableDictionary<int, int>.Empty);

    private class FakeInventory : IInventoryManager
    {
        public ContainerWindow? Open { get; set; }

        public ContainerWindow Player => new(0, null, 0, [.. new ItemStack?[PlayerSlots.Count]], ImmutableDictionary<int, int>.Empty);
        public ContainerWindow? OpenContainer => Open;
        public ItemStack? Cursor => null;
        public int SelectedHotbarSlot => 0;
        public ItemStack? HeldItem => null;
        public ContainerWindow ActiveWindow => Open ?? Player;

        public int Count(Item item) => 0;
        public IReadOnlyList<(int Slot, ItemStack Stack)> Find(Func<ItemStack, bool> match) => [];
        public int SpaceFor(Item item) => 64;
        public int? ToActiveWindowSlot(int playerSlot) => playerSlot;
        public Task SelectHotbarSlotAsync(int slot) => Task.CompletedTask;
        public Task ClickAsync(int slot, int button, ClickMode mode) => Task.CompletedTask;
        public Task PickUpAsync(int slot) => Task.CompletedTask;
        public Task QuickMoveAsync(int slot) => Task.CompletedTask;
        public Task SwapWithHotbarAsync(int slot, int hotbarSlot) => Task.CompletedTask;
        public Task DropAsync(int slot, bool wholeStack = true) => Task.CompletedTask;
        public Task CloseContainerAsync() => Task.CompletedTask;

        public int FirstEmptySlot() => 1;
    }

    private class NoNetworking : INetworkingManager
    {
        public Task Connect() => Task.CompletedTask;
        public Task ConnectAndWaitForPlay() => Task.CompletedTask;
        public Task Disconnect() => Task.CompletedTask;
        public Task SendPacket(PacketBase packet) => Task.CompletedTask;
    }
}
