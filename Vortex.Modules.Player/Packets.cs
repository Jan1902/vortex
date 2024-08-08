using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Player;

[AutoSerializedPacket(0x40)]
public record SynchronizePlayerPosition(double X, double Y, double Z, float Yaw, float Pitch, [BitField] PositionFlags Flags, int TeleportId) : PacketBase;

[Flags]
public enum PositionFlags
{
    X = 0x01,
    Y = 0x02,
    Z = 0x04,
    Y_ROT = 0x08,
    X_ROT = 0x10
}