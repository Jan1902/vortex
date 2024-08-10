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

[AutoSerializedPacket(0x00, packetDirection: PacketDirection.ServerBound)]
public record ConfirmTeleportation(int TeleportId) : PacketBase;

[AutoSerializedPacket(0x38)]
public record PlayerAbilities : PacketBase;

[AutoSerializedPacket(0x3e)]
public record PlayerInfoUpdate : PacketBase;

[AutoSerializedPacket(0x56)]
public record SetDefaultSpawnPosition : PacketBase;

[AutoSerializedPacket(0x71)]
public record SetTickingRate : PacketBase;

[AutoSerializedPacket(0x72)]
public record StepTick : PacketBase;

[AutoSerializedPacket(0x5d)]
public record SetHealth(float Health, int Food, float FoodSaturation) : PacketBase;

[AutoSerializedPacket(0x5c)]
public record SetExperience(float ExperienceBar, int Level, int TotalExperience) : PacketBase;