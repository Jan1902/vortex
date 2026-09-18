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
    Cancelled
}
