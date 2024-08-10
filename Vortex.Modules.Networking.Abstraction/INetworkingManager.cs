namespace Vortex.Modules.Networking.Abstraction;

/// <summary>
/// Represents a networking manager.
/// </summary>
public interface INetworkingManager
{
    /// <summary>
    /// Connects to the server.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Connect();

    /// <summary>
    /// Connects to the server and waits the protocol to enter play state.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConnectAndWaitForPlay();

    /// <summary>
    /// Sends a packet to the server.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendPacket(PacketBase packet);
}
