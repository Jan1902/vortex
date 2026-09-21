namespace Vortex.Modules.Player.Abstraction;

/// <summary>
/// How the player moves, which decides its speed and whether it is protected
/// from walking off edges.
/// </summary>
public enum MovementMode
{
    /// <summary>Ordinary walking.</summary>
    Walk,

    /// <summary>Sprinting, which is faster and consumes hunger in game.</summary>
    Sprint,

    /// <summary>Sneaking, which is slower and refuses to step off a ledge.</summary>
    Sneak
}

/// <summary>
/// Why a movement ended.
/// </summary>
public enum MovementResult
{
    /// <summary>The requested distance was covered.</summary>
    Arrived,

    /// <summary>Something is in the way and the player stopped making progress.</summary>
    Blocked,

    /// <summary>The movement was replaced by another one or cancelled.</summary>
    Cancelled,

    /// <summary>
    /// The server put the player somewhere else while the movement was running.
    /// Nothing was achieved and nothing went wrong: where the movement was
    /// headed was worked out from a position that no longer holds, so the
    /// caller should decide again rather than treat this as failure.
    /// </summary>
    Desynced
}
