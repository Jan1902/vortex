using Microsoft.Extensions.Logging;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Inventory;

internal class InventoryPacketHandler(
    ILogger<InventoryPacketHandler> logger,
    IEventBus eventBus,
    InventoryManager inventory)
    : IPacketHandler<SetContainerContent>,
    IPacketHandler<SetContainerSlot>,
    IPacketHandler<SetContainerData>,
    IPacketHandler<OpenScreen>,
    IPacketHandler<ClientBoundContainerClose>,
    IPacketHandler<ClientBoundSetCarriedItem>
{
    public Task HandleAsync(SetContainerContent packet)
        => Changed(inventory.SetContent(packet.WindowId, packet.StateId, packet.Slots, packet.Carried));

    public Task HandleAsync(SetContainerSlot packet)
    {
        switch (packet.WindowId)
        {
            case SetContainerSlot.CursorWindow:
                inventory.SetCursor(packet.Item);
                return Task.CompletedTask;

            case SetContainerSlot.PlayerInventoryWindow:
                return inventory.SetInventorySlot(packet.Slot, packet.Item) is { } player
                    ? Changed([player])
                    : Task.CompletedTask;

            default:
                return Changed(inventory.SetSlot(packet.WindowId, packet.StateId, packet.Slot, packet.Item));
        }
    }

    public async Task HandleAsync(SetContainerData packet)
    {
        if (inventory.SetProperty(packet.WindowId, packet.Property, packet.Value) is { } window)
            await eventBus.PublishAsync(new InventoryChangedEvent(window));
    }

    public async Task HandleAsync(OpenScreen packet)
    {
        var window = inventory.Open(packet.WindowId, packet.Type);

        logger.LogDebug("Opened a {Type} as window {WindowId}", packet.Type, packet.WindowId);

        await eventBus.PublishAsync(new ContainerOpenedEvent(window));
    }

    public async Task HandleAsync(ClientBoundContainerClose packet)
    {
        if (inventory.Close(packet.WindowId))
            await eventBus.PublishAsync(new ContainerClosedEvent(packet.WindowId));
    }

    public async Task HandleAsync(ClientBoundSetCarriedItem packet)
    {
        inventory.SelectHotbarSlot(packet.Slot);

        await eventBus.PublishAsync(new HotbarSelectionChangedEvent(packet.Slot));
    }

    private async Task Changed(IReadOnlyList<ContainerWindow> windows)
    {
        foreach (var window in windows)
            await eventBus.PublishAsync(new InventoryChangedEvent(window));
    }
}
