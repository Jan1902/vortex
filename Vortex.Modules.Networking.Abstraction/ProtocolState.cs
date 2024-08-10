namespace Vortex.Modules.Networking.Abstraction;

/// <summary>
/// Represents the state of the protocol.
/// </summary>
public enum ProtocolState
{
    /// <summary>
    /// Handshake state.
    /// </summary>
    Handshake = 0,

    /// <summary>
    /// Status state.
    /// </summary>
    Status = 1,

    /// <summary>
    /// Login state.
    /// </summary>
    Login = 2,

    /// <summary>
    /// Configuration state.
    /// </summary>
    Configuration = 3,

    /// <summary>
    /// Play state.
    /// </summary>
    Play = 4
}
