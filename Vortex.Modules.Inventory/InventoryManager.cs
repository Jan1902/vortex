using System.Collections.Immutable;
using Vortex.Data;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Inventory;

/// <summary>
/// Holds the windows and applies what the server reports about them.
/// </summary>
/// <remarks>
/// Changes come from the packet loop; readers can be on any thread and get
/// whole <see cref="ContainerWindow"/> snapshots. A lock keeps the player window
/// and an open container consistent with each other, since one update can
/// change both.
/// </remarks>
internal class InventoryManager(INetworkingManager networking, IEventBus eventBus) : IInventoryManager
{
    /// <summary>
    /// How long a click waits for the server to report what it changed. A click
    /// that changes nothing gets no answer, so this is kept short.
    /// </summary>
    private static readonly TimeSpan ClickAnswerWait = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// How long to keep listening once the first change came in: the server
    /// sends everything a click changed within the same tick.
    /// </summary>
    private static readonly TimeSpan ClickSettle = TimeSpan.FromMilliseconds(60);

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(10);

    private readonly object _lock = new();

    private ContainerWindow _player = Empty(ContainerWindow.PlayerWindowId, null, PlayerSlots.Count);
    private ContainerWindow? _container;
    private ItemStack? _cursor;
    private int _selectedHotbarSlot;

    /// <summary>Counts every change to a window, so a click can tell when its answer arrived.</summary>
    private int _version;

    public ContainerWindow Player { get { lock (_lock) return _player; } }

    public ContainerWindow? OpenContainer { get { lock (_lock) return _container; } }

    public ItemStack? Cursor { get { lock (_lock) return _cursor; } }

    public int SelectedHotbarSlot { get { lock (_lock) return _selectedHotbarSlot; } }

    public ItemStack? HeldItem
    {
        get
        {
            lock (_lock)
                return _player.Slots[PlayerSlots.Hotbar(_selectedHotbarSlot)];
        }
    }

    public int Count(Item item)
        => Find(stack => stack.Item == item).Sum(found => found.Stack.Count);

    public IReadOnlyList<(int Slot, ItemStack Stack)> Find(Func<ItemStack, bool> match)
    {
        var slots = Player.Slots;

        return PlayerSlots.Storage
            .Where(slot => slots[slot] is { } stack && match(stack))
            .Select(slot => (slot, slots[slot]!))
            .ToList();
    }

    public int SpaceFor(Item item)
    {
        var slots = Player.Slots;
        var maxStackSize = item.MaxStackSize();

        return Enumerable.Range(PlayerSlots.MainStart, PlayerSlots.MainCount + PlayerSlots.HotbarCount)
            .Sum(slot => slots[slot] switch
            {
                null => maxStackSize,
                { HasComponents: false } stack when stack.Item == item => Math.Max(0, stack.MaxStackSize - stack.Count),
                _ => 0,
            });
    }

    /// <summary>
    /// Replaces a window's contents and the cursor.
    /// </summary>
    /// <returns>The windows that changed.</returns>
    public IReadOnlyList<ContainerWindow> SetContent(int windowId, int stateId, ItemStack?[] slots, ItemStack? cursor)
    {
        lock (_lock)
        {
            _cursor = cursor;
            _version++;

            if (windowId == ContainerWindow.PlayerWindowId)
            {
                _player = _player with { StateId = stateId, Slots = [.. Pad(slots, PlayerSlots.Count)] };

                return [_player];
            }

            if (_container?.Id != windowId)
                return [];

            _container = _container with { StateId = stateId, Slots = [.. slots] };

            // The container's last slots are the player's main inventory and hotbar.
            var playerSlots = _player.Slots.ToBuilder();
            for (var i = 0; i < ContainerWindow.PlayerInventorySlots && _container.ContainerSize + i < slots.Length; i++)
                playerSlots[PlayerSlots.MainStart + i] = slots[_container.ContainerSize + i];

            _player = _player with { Slots = playerSlots.ToImmutable() };

            return [_container, _player];
        }
    }

    /// <summary>
    /// Changes one slot of a window.
    /// </summary>
    /// <returns>The windows that changed.</returns>
    public IReadOnlyList<ContainerWindow> SetSlot(int windowId, int stateId, int slot, ItemStack? stack)
    {
        lock (_lock)
        {
            _version++;

            if (windowId == ContainerWindow.PlayerWindowId)
            {
                if (slot < 0 || slot >= _player.Slots.Length)
                    return [];

                _player = _player with { StateId = stateId, Slots = _player.Slots.SetItem(slot, stack) };

                return [_player];
            }

            if (_container?.Id != windowId || slot < 0 || slot >= _container.Slots.Length)
                return [];

            _container = _container with { StateId = stateId, Slots = _container.Slots.SetItem(slot, stack) };

            if (slot < _container.ContainerSize)
                return [_container];

            _player = _player with { Slots = _player.Slots.SetItem(PlayerSlots.MainStart + slot - _container.ContainerSize, stack) };

            return [_container, _player];
        }
    }

    /// <summary>
    /// Changes a slot of the player's inventory given by inventory index: the
    /// hotbar is 0 to 8, the main inventory 9 to 35, armour 36 to 39 from the
    /// feet up, the offhand 40.
    /// </summary>
    /// <returns>The player window, or <c>null</c> for an index outside the inventory.</returns>
    public ContainerWindow? SetInventorySlot(int index, ItemStack? stack)
    {
        int? slot = index switch
        {
            >= 0 and < 9 => PlayerSlots.Hotbar(index),
            >= 9 and < 36 => index,
            >= 36 and < 40 => PlayerSlots.Feet - (index - 36),
            40 => PlayerSlots.Offhand,
            _ => null,
        };

        if (slot is null)
            return null;

        lock (_lock)
        {
            _version++;
            _player = _player with { Slots = _player.Slots.SetItem(slot.Value, stack) };

            return _player;
        }
    }

    public void SetCursor(ItemStack? stack)
    {
        lock (_lock)
        {
            _version++;
            _cursor = stack;
        }
    }

    /// <returns>The window whose property changed, or <c>null</c> if it is not open.</returns>
    public ContainerWindow? SetProperty(int windowId, int property, int value)
    {
        lock (_lock)
        {
            if (windowId == ContainerWindow.PlayerWindowId)
                return _player = _player with { Properties = _player.Properties.SetItem(property, value) };

            if (_container?.Id != windowId)
                return null;

            return _container = _container with { Properties = _container.Properties.SetItem(property, value) };
        }
    }

    /// <summary>
    /// A container opened. It starts empty; the server sends its contents next.
    /// </summary>
    public ContainerWindow Open(int windowId, Menu type)
    {
        lock (_lock)
            return _container = Empty(windowId, type, 0);
    }

    /// <returns>Whether the window was the open container.</returns>
    public bool Close(int windowId)
    {
        lock (_lock)
        {
            if (_container?.Id != windowId)
                return false;

            _container = null;

            return true;
        }
    }

    public ContainerWindow ActiveWindow { get { lock (_lock) return _container ?? _player; } }

    public int? ToActiveWindowSlot(int playerSlot)
    {
        lock (_lock)
        {
            if (_container is null)
                return playerSlot;

            if (playerSlot < PlayerSlots.MainStart || playerSlot >= PlayerSlots.Offhand)
                return null;

            return _container.ContainerSize + playerSlot - PlayerSlots.MainStart;
        }
    }

    public async Task ClickAsync(int slot, int button, ClickMode mode)
    {
        var window = ActiveWindow;
        var version = Volatile.Read(ref _version);

        // No expectations sent: the server then reports every slot the click
        // changed, so nothing of the game's click rules has to be copied here.
        await networking.SendPacket(new ContainerClick((byte)window.Id, window.StateId, (short)slot, (byte)button, mode, [], Carried: null));

        var deadline = DateTime.UtcNow + ClickAnswerWait;

        while (Volatile.Read(ref _version) == version && DateTime.UtcNow < deadline)
            await Task.Delay(Poll);

        if (Volatile.Read(ref _version) != version)
            await Task.Delay(ClickSettle);
    }

    public Task PickUpAsync(int slot)
        => ClickAsync(slot, 0, ClickMode.PickUp);

    public Task QuickMoveAsync(int slot)
        => ClickAsync(slot, 0, ClickMode.QuickMove);

    public Task SwapWithHotbarAsync(int slot, int hotbarSlot)
        => ClickAsync(slot, hotbarSlot, ClickMode.Swap);

    public Task DropAsync(int slot, bool wholeStack = true)
        => ClickAsync(slot, wholeStack ? 1 : 0, ClickMode.Throw);

    public async Task CloseContainerAsync()
    {
        if (OpenContainer is not { } container)
            return;

        await networking.SendPacket(new ServerBoundContainerClose((byte)container.Id));

        if (Close(container.Id))
            await eventBus.PublishAsync(new ContainerClosedEvent(container.Id));
    }

    public async Task SelectHotbarSlotAsync(int slot)
    {
        if (slot is < 0 or >= PlayerSlots.HotbarCount)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "The hotbar has nine slots, 0 to 8.");

        // The server takes this as said and does not answer, so it is set here too.
        SelectHotbarSlot(slot);

        await networking.SendPacket(new ServerBoundSetCarriedItem((short)slot));
    }

    public void SelectHotbarSlot(int slot)
    {
        lock (_lock)
            _selectedHotbarSlot = Math.Clamp(slot, 0, PlayerSlots.HotbarCount - 1);
    }

    private static ContainerWindow Empty(int id, Menu? type, int size)
        => new(id, type, 0, [.. new ItemStack?[size]], ImmutableDictionary<int, int>.Empty);

    private static ItemStack?[] Pad(ItemStack?[] slots, int size)
    {
        if (slots.Length >= size)
            return slots;

        var padded = new ItemStack?[size];
        slots.CopyTo(padded, 0);

        return padded;
    }
}
