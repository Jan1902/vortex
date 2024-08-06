namespace Vortex.Modules.Networking.Abstraction;

public enum ProtocolState
{
    Handshake = 0,
    Status = 1,
    Login = 2,
    Configuration = 3,
    Play = 4
}