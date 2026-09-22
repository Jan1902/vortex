using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Inventory.Test;

public class InventoryActionTests
{
    private readonly NoNetworking _networking = new();
    private readonly RecordingEventBus _events = new();
    private readonly InventoryManager _inventory;
    private readonly InventoryPacketHandler _handler;

    public InventoryActionTests()
    {
        _inventory = new InventoryManager(_networking, _events);
        _handler = new InventoryPacketHandler(NullLogger<InventoryPacketHandler>.Instance, _events, _inventory);
    }

    [Fact]
    public async Task ClicksWithoutExpectationsSoTheServerReportsTheResult()
    {
        await _handler.HandleAsync(new SetContainerContent(0, 7, new ItemStack?[PlayerSlots.Count], Carried: null));

        await _inventory.QuickMoveAsync(PlayerSlots.MainStart);

        var click = Assert.IsType<ContainerClick>(Assert.Single(_networking.Sent));
        Assert.Equal(0, click.WindowId);
        Assert.Equal(7, click.StateId);
        Assert.Equal(PlayerSlots.MainStart, click.Slot);
        Assert.Equal(ClickMode.QuickMove, click.Mode);
        Assert.Empty(click.ChangedSlots);
        Assert.Null(click.Carried);
    }

    [Fact]
    public async Task SwapsWithTheHotbarByKey()
    {
        await _inventory.SwapWithHotbarAsync(PlayerSlots.MainStart + 3, 4);

        var click = Assert.IsType<ContainerClick>(Assert.Single(_networking.Sent));
        Assert.Equal(ClickMode.Swap, click.Mode);
        Assert.Equal(4, click.Button);
    }

    [Fact]
    public async Task ClicksGoToTheOpenContainer()
    {
        await _handler.HandleAsync(new OpenScreen(5, Menu.Generic9x3, new StringTag()));
        await _handler.HandleAsync(new SetContainerContent(5, 2, new ItemStack?[27 + 36], Carried: null));

        Assert.Equal(27, _inventory.ToActiveWindowSlot(PlayerSlots.MainStart));
        Assert.Equal(27 + 27, _inventory.ToActiveWindowSlot(PlayerSlots.Hotbar(0)));
        Assert.Null(_inventory.ToActiveWindowSlot(PlayerSlots.Head));

        await _inventory.PickUpAsync(0);

        Assert.Equal(5, Assert.IsType<ContainerClick>(Assert.Single(_networking.Sent)).WindowId);
    }

    [Fact]
    public async Task ClosesTheContainer()
    {
        await _handler.HandleAsync(new OpenScreen(5, Menu.Generic9x3, new StringTag()));

        await _inventory.CloseContainerAsync();

        Assert.Equal(5, Assert.IsType<ServerBoundContainerClose>(Assert.Single(_networking.Sent)).WindowId);
        Assert.Null(_inventory.OpenContainer);
        Assert.Contains(_events.Events, e => e is ContainerClosedEvent { WindowId: 5 });
    }

    [Fact]
    public async Task SelectsHotbarSlots()
    {
        await _inventory.SelectHotbarSlotAsync(3);

        Assert.Equal(3, _inventory.SelectedHotbarSlot);
        Assert.Equal(3, Assert.IsType<ServerBoundSetCarriedItem>(Assert.Single(_networking.Sent)).Slot);
    }
}
