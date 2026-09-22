namespace Vortex.Modules.Inventory.Abstraction;

/// <summary>The contents of a window changed.</summary>
public record InventoryChangedEvent(ContainerWindow Window);

/// <summary>A container opened, such as a chest someone clicked on.</summary>
public record ContainerOpenedEvent(ContainerWindow Window);

/// <summary>The open container was closed, by the server or the bot.</summary>
public record ContainerClosedEvent(int WindowId);

/// <summary>The selected hotbar slot changed.</summary>
public record HotbarSelectionChangedEvent(int Slot);
