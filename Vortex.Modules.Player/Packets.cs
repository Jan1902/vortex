using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Player;

[AutoSerializedPacket(PacketIds.Play.ClientBound.PlayerPosition)]
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

[AutoSerializedPacket(PacketIds.Play.ServerBound.AcceptTeleportation, packetDirection: PacketDirection.ServerBound)]
public record ConfirmTeleportation(int TeleportId) : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.PlayerAbilities)]
public record PlayerAbilities : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.PlayerInfoUpdate)]
public record PlayerInfoUpdate : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.SetDefaultSpawnPosition)]
public record SetDefaultSpawnPosition : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.TickingState)]
public record SetTickingRate : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.TickingStep)]
public record StepTick : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.SetHealth)]
public record SetHealth(float Health, int Food, float FoodSaturation) : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.SetExperience)]
public record SetExperience(float ExperienceBar, int Level, int TotalExperience) : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ServerBound.MovePlayerPos, packetDirection: PacketDirection.ServerBound)]
public record SetPlayerPosition(double X, double FeetY, double Z, bool OnGround) : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ServerBound.MovePlayerPosRot, packetDirection: PacketDirection.ServerBound)]
public record SetPlayerPositionAndRotation(double X, double FeetY, double Z, float Yaw, float Pitch, bool OnGround) : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ServerBound.MovePlayerStatusOnly, packetDirection: PacketDirection.ServerBound)]
public record SetPlayerOnGround(bool OnGround) : PacketBase;


/// <summary>
/// Action ids for <see cref="ClientCommand"/>.
/// </summary>
public static class ClientCommandAction
{
    /// <summary>Leaves the death screen. A dead player receives no chunks until this is sent.</summary>
    public const int PerformRespawn = 0;

    /// <summary>Asks the server for the player's statistics.</summary>
    public const int RequestStatistics = 1;
}

[AutoSerializedPacket(PacketIds.Play.ServerBound.ClientCommand, packetDirection: PacketDirection.ServerBound)]
public record ClientCommand(int ActionId) : PacketBase;
