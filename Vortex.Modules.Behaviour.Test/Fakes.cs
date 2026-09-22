using System.Collections.Immutable;
using Vortex.Data;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Test;

internal class FakePlayer : IPlayerManager
{
    public Vector3d Position { get; set; } = Vector3d.Zero;
    public Vector3d Velocity => Vector3d.Zero;
    public bool IsOnGround => true;
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public float Health => 20;
    public bool IsAlive => true;
    public bool IsPositionSynchronized => true;
    public bool IsSpawned => true;

    public void Look(float yaw, float pitch) => (Yaw, Pitch) = (yaw, pitch);

    public void LookAt(Vector3d target) { }
}

internal class FakeEntities : IEntityManager
{
    public List<Entity> Tracked { get; } = [];

    public int? SelfId => 1;
    public IReadOnlyCollection<Entity> Entities => Tracked;
    public IReadOnlyCollection<PlayerListEntry> Players => [];

    public Entity? Get(int id) => Tracked.FirstOrDefault(entity => entity.Id == id);
    public Entity? Get(Guid uuid) => Tracked.FirstOrDefault(entity => entity.Uuid == uuid);
    public Entity? Nearest(Vector3d from, Func<Entity, bool>? filter = null) => null;
    public PlayerListEntry? GetPlayer(Guid uuid) => null;
    public Entity? FindPlayer(string name) => null;

    /// <summary>Puts an item on the ground.</summary>
    public Entity Drop(int id, Item item, int count, Vector3d position)
    {
        var entity = new Entity(id, Guid.NewGuid(), EntityType.Item, position, 0, 0, 0, Vector3d.Zero, OnGround: true)
        {
            Metadata = ImmutableDictionary<int, object?>.Empty.Add(EntityDataKeys.IndexOf(EntityType.Item, EntityDataKey.Item), new ItemStack(item, count)),
        };

        Tracked.Add(entity);

        return entity;
    }
}

internal class FakeInventory : IInventoryManager
{
    private readonly ItemStack?[] _slots = new ItemStack?[PlayerSlots.Count];

    /// <summary>Items the inventory has no room for.</summary>
    public HashSet<Item> Full { get; } = [];

    public ContainerWindow Player
        => new(ContainerWindow.PlayerWindowId, null, 0, [.. _slots], ImmutableDictionary<int, int>.Empty);

    public ContainerWindow? OpenContainer => null;
    public ItemStack? Cursor => null;
    public int SelectedHotbarSlot { get; private set; }
    public ItemStack? HeldItem => _slots[PlayerSlots.Hotbar(SelectedHotbarSlot)];

    /// <summary>Puts something into a hotbar slot.</summary>
    public void Hotbar(int slot, Item item) => _slots[PlayerSlots.Hotbar(slot)] = new ItemStack(item, 1);

    /// <summary>Puts something into a slot of the player window.</summary>
    public void Put(int slot, Item item, int count = 1) => _slots[slot] = new ItemStack(item, count);

    /// <summary>Every click made, as slot and mode.</summary>
    public List<(int Slot, int Button, ClickMode Mode)> Clicks { get; } = [];

    public ContainerWindow ActiveWindow => Player;

    public int? ToActiveWindowSlot(int playerSlot) => playerSlot;

    public Task ClickAsync(int slot, int button, ClickMode mode)
    {
        Clicks.Add((slot, button, mode));

        if (mode == ClickMode.Swap)
            (_slots[slot], _slots[PlayerSlots.Hotbar(button)]) = (_slots[PlayerSlots.Hotbar(button)], _slots[slot]);

        return Task.CompletedTask;
    }

    public Task PickUpAsync(int slot) => ClickAsync(slot, 0, ClickMode.PickUp);
    public Task QuickMoveAsync(int slot) => ClickAsync(slot, 0, ClickMode.QuickMove);
    public Task SwapWithHotbarAsync(int slot, int hotbarSlot) => ClickAsync(slot, hotbarSlot, ClickMode.Swap);
    public Task DropAsync(int slot, bool wholeStack = true) => ClickAsync(slot, wholeStack ? 1 : 0, ClickMode.Throw);
    public Task CloseContainerAsync() => Task.CompletedTask;

    public int Count(Item item) => Find(stack => stack.Item == item).Sum(found => found.Stack.Count);

    public IReadOnlyList<(int Slot, ItemStack Stack)> Find(Func<ItemStack, bool> match)
        => PlayerSlots.Storage
            .Where(slot => _slots[slot] is { } stack && match(stack))
            .Select(slot => (slot, _slots[slot]!))
            .ToList();
    public int SpaceFor(Item item) => Full.Contains(item) ? 0 : 64;

    public Task SelectHotbarSlotAsync(int slot)
    {
        SelectedHotbarSlot = slot;
        return Task.CompletedTask;
    }
}
