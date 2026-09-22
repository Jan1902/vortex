using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Inventory.Test;

public class InventoryTrackingTests
{
    private readonly InventoryManager _inventory;
    private readonly RecordingEventBus _events = new();
    private readonly InventoryPacketHandler _handler;

    public InventoryTrackingTests()
    {
        _inventory = new InventoryManager(new NoNetworking(), _events);
        _handler = new InventoryPacketHandler(NullLogger<InventoryPacketHandler>.Instance, _events, _inventory);
    }

    [Fact]
    public async Task TakesInTheWholeInventory()
    {
        var slots = new ItemStack?[PlayerSlots.Count];
        slots[PlayerSlots.Hotbar(0)] = new ItemStack(Item.StonePickaxe, 1);
        slots[PlayerSlots.MainStart] = new ItemStack(Item.Cobblestone, 40);
        slots[PlayerSlots.MainStart + 1] = new ItemStack(Item.Cobblestone, 20);

        await _handler.HandleAsync(new SetContainerContent(0, 5, slots, Carried: null));

        Assert.Equal(60, _inventory.Count(Item.Cobblestone));
        Assert.Equal(Item.StonePickaxe, _inventory.HeldItem?.Item);
        Assert.Equal(5, _inventory.Player.StateId);
        Assert.IsType<InventoryChangedEvent>(Assert.Single(_events.Events));
    }

    [Fact]
    public async Task ChangesSingleSlots()
    {
        await _handler.HandleAsync(new SetContainerSlot(0, 6, (short)PlayerSlots.Hotbar(2), new ItemStack(Item.Torch, 16)));
        await _handler.HandleAsync(new ClientBoundSetCarriedItem(2));

        Assert.Equal(new ItemStack(Item.Torch, 16), _inventory.HeldItem);
        Assert.Equal(2, _inventory.SelectedHotbarSlot);
    }

    [Fact]
    public async Task SetsTheCursor()
    {
        await _handler.HandleAsync(new SetContainerSlot(SetContainerSlot.CursorWindow, 0, -1, new ItemStack(Item.Dirt, 3)));

        Assert.Equal(new ItemStack(Item.Dirt, 3), _inventory.Cursor);
    }

    [Theory]
    [InlineData(0, PlayerSlots.HotbarStart)]
    [InlineData(20, 20)]
    [InlineData(36, PlayerSlots.Feet)]
    [InlineData(39, PlayerSlots.Head)]
    [InlineData(40, PlayerSlots.Offhand)]
    public async Task MapsInventoryIndicesToWindowSlots(short index, int slot)
    {
        await _handler.HandleAsync(new SetContainerSlot(SetContainerSlot.PlayerInventoryWindow, 0, index, new ItemStack(Item.Apple, 1)));

        Assert.Equal(Item.Apple, _inventory.Player.Slots[slot]?.Item);
    }

    [Fact]
    public async Task KeepsThePlayerInventoryCurrentWhileAChestIsOpen()
    {
        await _handler.HandleAsync(new OpenScreen(3, Menu.Generic9x3, new StringTag()));

        // 27 chest slots, then the player's 27 main slots and 9 hotbar slots.
        var slots = new ItemStack?[27 + 36];
        slots[0] = new ItemStack(Item.Diamond, 5);
        slots[27] = new ItemStack(Item.Stick, 8);
        await _handler.HandleAsync(new SetContainerContent(3, 1, slots, Carried: null));
        await _handler.HandleAsync(new SetContainerSlot(3, 2, 27 + 36 - 1, new ItemStack(Item.Bread, 2)));

        Assert.Equal(27, _inventory.OpenContainer!.ContainerSize);
        Assert.Equal(new ItemStack(Item.Diamond, 5), _inventory.OpenContainer.Slots[0]);
        Assert.Equal(8, _inventory.Count(Item.Stick));
        Assert.Equal(Item.Bread, _inventory.Player.Slots[PlayerSlots.Hotbar(8)]?.Item);
        Assert.Equal(0, _inventory.Count(Item.Diamond));
    }

    [Fact]
    public async Task ForgetsAClosedContainer()
    {
        await _handler.HandleAsync(new OpenScreen(3, Menu.Generic9x3, new StringTag()));
        await _handler.HandleAsync(new ClientBoundContainerClose(3));

        Assert.Null(_inventory.OpenContainer);
        Assert.Contains(_events.Events, e => e is ContainerClosedEvent { WindowId: 3 });
    }

    [Fact]
    public async Task KnowsHowMuchMoreFits()
    {
        var slots = new ItemStack?[PlayerSlots.Count];
        for (var slot = PlayerSlots.MainStart; slot < PlayerSlots.Offhand; slot++)
            slots[slot] = new ItemStack(Item.Stone, 64);
        slots[PlayerSlots.MainStart] = new ItemStack(Item.Cobblestone, 60);

        await _handler.HandleAsync(new SetContainerContent(0, 1, slots, Carried: null));

        Assert.Equal(4, _inventory.SpaceFor(Item.Cobblestone));
        Assert.Equal(0, _inventory.SpaceFor(Item.Dirt));
    }
}

internal class NoNetworking : Vortex.Modules.Networking.Abstraction.INetworkingManager
{
    public List<Vortex.Modules.Networking.Abstraction.PacketBase> Sent { get; } = [];

    public Task Connect() => Task.CompletedTask;

    public Task ConnectAndWaitForPlay() => Task.CompletedTask;
    public Task Disconnect() => Task.CompletedTask;

    public Task SendPacket(Vortex.Modules.Networking.Abstraction.PacketBase packet)
    {
        Sent.Add(packet);
        return Task.CompletedTask;
    }
}
