using Vortex.Data;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Knowledge;

/// <summary>
/// What was in every container the bot has looked into, as of when it last
/// looked.
/// </summary>
/// <remarks>
/// <para>
/// The game says which window opened, not which block it belongs to, so whoever
/// opens a container says where beforehand through <see cref="ExpectOpening"/>.
/// From then on the window's contents are kept against that position, and kept
/// up to date for as long as it stays open -- including what the bot takes out.
/// </para>
/// <para>
/// Only ever as fresh as the last look. Someone else may have emptied a chest
/// since; the bot then finds out by opening it, which updates what is known.
/// </para>
/// </remarks>
public class ContainerMemory :
    IEventHandler<ContainerOpenedEvent>,
    IEventHandler<InventoryChangedEvent>,
    IEventHandler<ContainerClosedEvent>
{
    private readonly Dictionary<Vector3i, IReadOnlyList<ItemStack>> _contents = [];
    private readonly object _lock = new();

    private Vector3i? _expected;
    private (int WindowId, Vector3i Position)? _open;

    /// <summary>Where the container that is open right now stands, if the bot opened one.</summary>
    public Vector3i? OpenAt
    {
        get { lock (_lock) return _open?.Position; }
    }

    /// <summary>Every container looked into, with what was in it.</summary>
    public IReadOnlyList<(Vector3i Position, IReadOnlyList<ItemStack> Contents)> Known
    {
        get { lock (_lock) return _contents.Select(entry => (entry.Key, entry.Value)).ToList(); }
    }

    /// <summary>Says that the next container to open is the one at a position.</summary>
    public void ExpectOpening(Vector3i position)
    {
        lock (_lock)
            _expected = position;
    }

    /// <summary>How many of the items were in the container at a position when last seen.</summary>
    public int CountAt(Vector3i position, IReadOnlySet<Item> items)
    {
        lock (_lock)
            return _contents.TryGetValue(position, out var contents)
                ? contents.Where(stack => items.Contains(stack.Item)).Sum(stack => stack.Count)
                : 0;
    }

    /// <summary>Forgets a container, once it turns out to be gone.</summary>
    public void Forget(Vector3i position)
    {
        lock (_lock)
            _contents.Remove(position);
    }

    public Task HandleAsync(ContainerOpenedEvent @event)
    {
        lock (_lock)
        {
            _open = _expected is { } position ? (@event.Window.Id, position) : null;
            _expected = null;

            if (_open is not null)
                Record(@event.Window);
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(InventoryChangedEvent @event)
    {
        lock (_lock)
            if (_open is { } open && open.WindowId == @event.Window.Id)
                Record(@event.Window);

        return Task.CompletedTask;
    }

    public Task HandleAsync(ContainerClosedEvent @event)
    {
        lock (_lock)
            if (_open is { } open && open.WindowId == @event.WindowId)
                _open = null;

        return Task.CompletedTask;
    }

    /// <remarks>
    /// Only for containers that store things. A crafting table or a furnace
    /// opens the same way, but what lies in its slots is work in progress,
    /// not something to come back for.
    /// </remarks>
    private void Record(ContainerWindow window)
    {
        if (window.Type is not (Menu.Generic9x1 or Menu.Generic9x2 or Menu.Generic9x3 or Menu.Generic9x4
            or Menu.Generic9x5 or Menu.Generic9x6 or Menu.Generic3x3 or Menu.Hopper or Menu.ShulkerBox))
            return;

        _contents[_open!.Value.Position] = window.Slots
            .Take(window.ContainerSize)
            .OfType<ItemStack>()
            .ToList();
    }
}
