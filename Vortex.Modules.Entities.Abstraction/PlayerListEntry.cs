namespace Vortex.Modules.Entities.Abstraction;

/// <summary>
/// A player on the server, as the tab list knows it. Players far away are on
/// the list without being among the tracked entities.
/// </summary>
/// <param name="Uuid">The account's UUID, which is also the UUID of the player's entity.</param>
/// <param name="Latency">The round trip to the server, in milliseconds.</param>
/// <param name="Listed">Whether the tab list shows the player.</param>
public sealed record PlayerListEntry(Guid Uuid, string Name, GameMode GameMode, int Latency, bool Listed);

/// <summary>
/// How a player plays.
/// </summary>
public enum GameMode
{
    Survival = 0,
    Creative = 1,
    Adventure = 2,
    Spectator = 3
}
