using System.Collections.Concurrent;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Entities;

/// <summary>
/// Holds the tracked entities and applies what the server reports about them.
/// </summary>
/// <remarks>
/// Changes come from the packet loop only, one at a time. Readers can be on any
/// thread; they get whole <see cref="Entity"/> snapshots, which are swapped
/// rather than modified.
/// </remarks>
internal class EntityManager : IEntityManager
{
    /// <summary>Movement deltas are sent in 1/4096 of a block.</summary>
    private const double DeltaScale = 4096;

    /// <summary>Velocities are sent in 1/8000 of a block per tick.</summary>
    private const double VelocityScale = 8000;

    private readonly ConcurrentDictionary<int, Entity> _entities = new();
    private readonly ConcurrentDictionary<Guid, int> _idsByUuid = new();
    private readonly ConcurrentDictionary<Guid, PlayerListEntry> _players = new();

    public int? SelfId { get; private set; }

    public IReadOnlyCollection<Entity> Entities => [.. _entities.Values];

    public Entity? Get(int id)
        => _entities.TryGetValue(id, out var entity) ? entity : null;

    public Entity? Get(Guid uuid)
        => _idsByUuid.TryGetValue(uuid, out var id) ? Get(id) : null;

    public Entity? Nearest(Vector3d from, Func<Entity, bool>? filter = null)
        => _entities.Values
            .Where(entity => filter?.Invoke(entity) ?? true)
            .MinBy(entity => entity.Position.DistanceTo(from));

    public IReadOnlyCollection<PlayerListEntry> Players => [.. _players.Values];

    public PlayerListEntry? GetPlayer(Guid uuid)
        => _players.TryGetValue(uuid, out var player) ? player : null;

    public Entity? FindPlayer(string name)
        => _players.Values.FirstOrDefault(player => string.Equals(player.Name, name, StringComparison.OrdinalIgnoreCase)) is { } player
            ? Get(player.Uuid)
            : null;

    /// <summary>
    /// Adds a player to the tab list or changes its entry. Parts the update does
    /// not carry keep their value.
    /// </summary>
    public void UpdatePlayer(Guid uuid, string? name, GameMode? gameMode, bool? listed, int? latency)
    {
        var player = _players.TryGetValue(uuid, out var known)
            ? known
            : new PlayerListEntry(uuid, name ?? "", GameMode.Survival, 0, Listed: false);

        _players[uuid] = player with
        {
            Name = name ?? player.Name,
            GameMode = gameMode ?? player.GameMode,
            Listed = listed ?? player.Listed,
            Latency = latency ?? player.Latency,
        };
    }

    public void RemovePlayer(Guid uuid)
        => _players.TryRemove(uuid, out _);

    /// <summary>
    /// Forgets the players of a previous session. Unlike the entities, the tab
    /// list survives respawning and changing worlds.
    /// </summary>
    public void ClearPlayers()
        => _players.Clear();

    /// <summary>
    /// Starts over in a new world, forgetting every entity of the old one.
    /// </summary>
    /// <param name="selfId">The bot's own ID, if the server just gave it one.</param>
    public void Reset(int? selfId = null)
    {
        if (selfId is not null)
            SelfId = selfId;

        _entities.Clear();
        _idsByUuid.Clear();
    }

    /// <returns>The entity, or <c>null</c> if it is the bot itself.</returns>
    public Entity? Add(Entity entity)
    {
        if (entity.Id == SelfId)
            return null;

        // An ID is only ever reused after the entity it belonged to was removed,
        // but a spawn for a known ID replaces it all the same, UUID included.
        if (_entities.TryGetValue(entity.Id, out var previous))
            _idsByUuid.TryRemove(previous.Uuid, out _);

        _entities[entity.Id] = entity;

        if (entity.Uuid != Guid.Empty)
            _idsByUuid[entity.Uuid] = entity.Id;

        return entity;
    }

    public void MoveBy(int id, short deltaX, short deltaY, short deltaZ, bool onGround)
        => Update(id, entity => entity with
        {
            Position = entity.Position + new Vector3d(deltaX / DeltaScale, deltaY / DeltaScale, deltaZ / DeltaScale),
            OnGround = onGround,
        });

    public void Turn(int id, float yaw, float pitch, bool onGround)
        => Update(id, entity => entity with { Yaw = yaw, Pitch = pitch, OnGround = onGround });

    public void MoveTo(int id, Vector3d position, float yaw, float pitch, bool onGround)
        => Update(id, entity => entity with { Position = position, Yaw = yaw, Pitch = pitch, OnGround = onGround });

    public void SetVelocity(int id, short x, short y, short z)
        => Update(id, entity => entity with { Velocity = ToVelocity(x, y, z) });

    /// <summary>
    /// Merges metadata values into what is known; values the update does not
    /// carry keep theirs.
    /// </summary>
    public void SetData(int id, IEnumerable<EntityDataValue> values)
        => Update(id, entity => entity with
        {
            Metadata = entity.Metadata.SetItems(values.Select(value => new KeyValuePair<int, object?>(value.Index, value.Value))),
        });

    public void TurnHead(int id, float headYaw)
        => Update(id, entity => entity with { HeadYaw = headYaw });

    /// <returns>The entity as it was last known, or <c>null</c> if it was not tracked.</returns>
    public Entity? Remove(int id)
    {
        if (!_entities.TryRemove(id, out var entity))
            return null;

        _idsByUuid.TryRemove(entity.Uuid, out _);

        return entity;
    }

    public static Vector3d ToVelocity(short x, short y, short z)
        => new(x / VelocityScale, y / VelocityScale, z / VelocityScale);

    /// <summary>
    /// Replaces an entity's snapshot. Updates for entities that are not tracked
    /// are dropped: they happen when a packet about the bot itself arrives, or
    /// one that crossed a removal on the way.
    /// </summary>
    private void Update(int id, Func<Entity, Entity> change)
    {
        if (_entities.TryGetValue(id, out var entity))
            _entities[id] = change(entity);
    }
}
