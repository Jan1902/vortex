using System.Collections.Immutable;
using Vortex.Data;
using Vortex.Modules.Behaviour.Knowledge;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Test;

public class ContainerMemoryTests
{
    private static readonly Vector3i _chest = new(10, 64, 3);

    private readonly ContainerMemory _memory = new();

    [Fact]
    public async Task RemembersWhatWasInAContainerByWhereItStands()
    {
        _memory.ExpectOpening(_chest);

        await _memory.HandleAsync(new ContainerOpenedEvent(Window(4, Menu.Generic9x3)));
        await _memory.HandleAsync(new InventoryChangedEvent(Window(4, Menu.Generic9x3, (0, Item.OakLog, 5), (3, Item.OakLog, 2))));

        Assert.Equal(_chest, _memory.OpenAt);
        Assert.Equal(7, _memory.CountAt(_chest, new HashSet<Item> { Item.OakLog }));
    }

    [Fact]
    public async Task KeepsUpWithWhatIsTakenOut()
    {
        _memory.ExpectOpening(_chest);

        await _memory.HandleAsync(new ContainerOpenedEvent(Window(4, Menu.Generic9x3, (0, Item.OakLog, 5))));
        await _memory.HandleAsync(new InventoryChangedEvent(Window(4, Menu.Generic9x3)));

        Assert.Equal(0, _memory.CountAt(_chest, new HashSet<Item> { Item.OakLog }));
    }

    [Fact]
    public async Task LeavesTheInventoryAlone()
    {
        _memory.ExpectOpening(_chest);

        await _memory.HandleAsync(new ContainerOpenedEvent(Window(4, Menu.Generic9x3)));
        await _memory.HandleAsync(new InventoryChangedEvent(Window(ContainerWindow.PlayerWindowId, null, (0, Item.Diamond, 1))));

        Assert.Equal(0, _memory.CountAt(_chest, new HashSet<Item> { Item.Diamond }));
    }

    [Fact]
    public async Task DoesNotTakeACraftingTableForStorage()
    {
        _memory.ExpectOpening(_chest);

        await _memory.HandleAsync(new ContainerOpenedEvent(Window(4, Menu.Crafting, (1, Item.OakPlanks, 1))));

        // Open all the same, which is what crafting asks about.
        Assert.Equal(_chest, _memory.OpenAt);
        Assert.Empty(_memory.Known);
    }

    [Fact]
    public async Task ForgetsWhereTheOpenOneIsOnceItCloses()
    {
        _memory.ExpectOpening(_chest);

        await _memory.HandleAsync(new ContainerOpenedEvent(Window(4, Menu.Generic9x3)));
        await _memory.HandleAsync(new ContainerClosedEvent(4));

        Assert.Null(_memory.OpenAt);
        Assert.Single(_memory.Known);
    }

    [Fact]
    public async Task IgnoresAContainerNobodySaidWhereItIs()
    {
        await _memory.HandleAsync(new ContainerOpenedEvent(Window(4, Menu.Generic9x3, (0, Item.OakLog, 5))));

        Assert.Null(_memory.OpenAt);
        Assert.Empty(_memory.Known);
    }

    private static ContainerWindow Window(int id, Menu? type, params (int Slot, Item Item, int Count)[] stacks)
    {
        var size = id == ContainerWindow.PlayerWindowId ? PlayerSlots.Count : type == Menu.Crafting ? 10 + 36 : 27 + 36;
        var slots = new ItemStack?[size];

        foreach (var (slot, item, count) in stacks)
            slots[slot] = new ItemStack(item, count);

        return new ContainerWindow(id, type, 0, [.. slots], ImmutableDictionary<int, int>.Empty);
    }
}
