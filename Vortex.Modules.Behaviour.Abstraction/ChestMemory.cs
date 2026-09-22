using Vortex.Data;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Abstraction;

/// <summary>
/// What was in the containers the bot has opened, as of the last look.
/// </summary>
/// <remarks>
/// Written by whoever opens a container, since only they know which block the
/// window belongs to. May be out of date; the bot finds out when it opens the
/// container again.
/// </remarks>
public sealed class ChestMemory
{
    private readonly Dictionary<Vector3i, List<ItemStack>> _contents = [];
    private readonly object _lock = new();

    /// <summary>Where the container the bot opened last stands; only meaningful while one is open.</summary>
    public Vector3i? OpenAt { get; set; }

    /// <summary>Notes what is in the container at a position.</summary>
    public void Remember(Vector3i position, IEnumerable<ItemStack?> contents)
    {
        lock (_lock)
            _contents[position] = contents.OfType<ItemStack>().ToList();
    }

    public void Forget(Vector3i position)
    {
        lock (_lock)
            _contents.Remove(position);
    }

    /// <summary>The containers that held any of the items, nearest to a point first.</summary>
    public IReadOnlyList<Vector3i> Holding(IReadOnlySet<Item> items, Vector3d near)
    {
        lock (_lock)
            return _contents
                .Where(entry => entry.Value.Any(stack => items.Contains(stack.Item)))
                .Select(entry => entry.Key)
                .OrderBy(position => new Vector3d(position.X + 0.5, position.Y, position.Z + 0.5).DistanceTo(near))
                .ToList();
    }

    /// <summary>What the container at a position held, empty if it was never looked into.</summary>
    public IReadOnlyList<ItemStack> ContentsOf(Vector3i position)
    {
        lock (_lock)
            return _contents.TryGetValue(position, out var contents) ? contents.ToList() : [];
    }
}
