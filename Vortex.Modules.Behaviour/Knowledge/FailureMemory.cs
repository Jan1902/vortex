namespace Vortex.Modules.Behaviour.Knowledge;

/// <summary>
/// Ways of getting something done that failed recently, so that the next
/// attempt takes another.
/// </summary>
/// <remarks>
/// <para>
/// This is knowledge about the world rather than progress of a task: that
/// chest was empty, that ore could not be reached. Tasks stay free of state of
/// their own and simply ask here whether an option is worth offering.
/// </para>
/// <para>
/// Entries lapse after a while, because the world changes -- the chest gets
/// refilled, a way to the ore opens up -- and because a failure is sometimes
/// only bad luck.
/// </para>
/// </remarks>
public class FailureMemory(TimeProvider time)
{
    /// <summary>How long a failure keeps an option out of consideration.</summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(2);

    private readonly Dictionary<string, DateTimeOffset> _failed = [];
    private readonly object _lock = new();

    public FailureMemory()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Remembers that an option failed.</summary>
    public void Remember(string key, TimeSpan? duration = null)
    {
        lock (_lock)
            _failed[key] = time.GetUtcNow() + (duration ?? DefaultDuration);
    }

    /// <summary>Whether an option failed recently enough to leave it be.</summary>
    public bool HasFailed(string key)
    {
        lock (_lock)
        {
            if (!_failed.TryGetValue(key, out var until))
                return false;

            if (until > time.GetUtcNow())
                return true;

            _failed.Remove(key);

            return false;
        }
    }
}
