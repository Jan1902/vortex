namespace Vortex.Framework.Abstraction;

/// <summary>
/// Represents a Vortex client.
/// </summary>
public interface IVortexClient
{
    /// <summary>
    /// Starts the Vortex client asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAsync();
}
