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
    /// <summary>Items the inventory has no room for.</summary>
    public HashSet<Item> Full { get; } = [];

    public ContainerWindow Player => throw new NotSupportedException();
    public ContainerWindow? OpenContainer => null;
    public ItemStack? Cursor => null;
    public int SelectedHotbarSlot => 0;
    public ItemStack? HeldItem => null;

    public int Count(Item item) => 0;
    public IReadOnlyList<(int Slot, ItemStack Stack)> Find(Func<ItemStack, bool> match) => [];
    public int SpaceFor(Item item) => Full.Contains(item) ? 0 : 64;
}
