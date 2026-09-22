using Vortex.Data;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Entities;

/// <summary>
/// Starts the play state. Carries the entity ID the server gave the player,
/// which is how packets about the player itself are told apart from packets
/// about everyone else.
/// </summary>
/// <remarks>
/// Only the fields up to the one needed are declared; the packet is framed, so
/// the rest is simply not read.
/// </remarks>
[AutoSerializedPacket(PacketIds.Play.ClientBound.Login)]
public record LoginPlay([OverwriteType(OverwriteType.Int)] int EntityId, bool IsHardcore) : PacketBase;

/// <summary>
/// Moves the player into a new world, after death or through a portal. Everything
/// the client knew about the old one, entities included, is gone.
/// </summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.Respawn)]
public record Respawn : PacketBase;

/// <summary>
/// An entity came into tracking range.
/// </summary>
/// <param name="Data">Type specific, such as the direction an item frame faces.</param>
/// <param name="VelocityX">In 1/8000 of a block per tick.</param>
[AutoSerializedPacket(PacketIds.Play.ClientBound.AddEntity)]
public record AddEntity(
    int EntityId,
    Guid Uuid,
    EntityType Type,
    double X,
    double Y,
    double Z,
    [OverwriteType(OverwriteType.Angle)] float Pitch,
    [OverwriteType(OverwriteType.Angle)] float Yaw,
    [OverwriteType(OverwriteType.Angle)] float HeadYaw,
    int Data,
    short VelocityX,
    short VelocityY,
    short VelocityZ) : PacketBase;

/// <summary>
/// An experience orb came into tracking range. Orbs have a packet of their own
/// and no UUID.
/// </summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.AddExperienceOrb)]
public record AddExperienceOrb(int EntityId, double X, double Y, double Z, short Count) : PacketBase;

/// <summary>
/// An entity moved a short way. The deltas are in 1/4096 of a block.
/// </summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.MoveEntityPos)]
public record MoveEntityPosition(int EntityId, short DeltaX, short DeltaY, short DeltaZ, bool OnGround) : PacketBase;

/// <summary>
/// An entity moved a short way and turned. The deltas are in 1/4096 of a block.
/// </summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.MoveEntityPosRot)]
public record MoveEntityPositionAndRotation(
    int EntityId,
    short DeltaX,
    short DeltaY,
    short DeltaZ,
    [OverwriteType(OverwriteType.Angle)] float Yaw,
    [OverwriteType(OverwriteType.Angle)] float Pitch,
    bool OnGround) : PacketBase;

/// <summary>An entity turned without moving.</summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.MoveEntityRot)]
public record MoveEntityRotation(
    int EntityId,
    [OverwriteType(OverwriteType.Angle)] float Yaw,
    [OverwriteType(OverwriteType.Angle)] float Pitch,
    bool OnGround) : PacketBase;

/// <summary>
/// An entity is at an absolute position. Sent when it moved too far for a
/// delta, and now and then to correct the drift of adding deltas up.
/// </summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.TeleportEntity)]
public record TeleportEntity(
    int EntityId,
    double X,
    double Y,
    double Z,
    [OverwriteType(OverwriteType.Angle)] float Yaw,
    [OverwriteType(OverwriteType.Angle)] float Pitch,
    bool OnGround) : PacketBase;

/// <summary>An entity's velocity changed, in 1/8000 of a block per tick.</summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.SetEntityMotion)]
public record SetEntityMotion(int EntityId, short VelocityX, short VelocityY, short VelocityZ) : PacketBase;

/// <summary>An entity turned its head.</summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.RotateHead)]
public record RotateHead(int EntityId, [OverwriteType(OverwriteType.Angle)] float HeadYaw) : PacketBase;

/// <summary>
/// An entity picked up an item, arrow or experience orb. Sent just before the
/// picked up entity is removed, or with a smaller count when only part of the
/// stack fit.
/// </summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.TakeItemEntity)]
public record TakeItemEntity(int CollectedEntityId, int CollectorEntityId, int Count) : PacketBase;

/// <summary>Entities are gone, or out of tracking range.</summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.RemoveEntities)]
public record RemoveEntities(int[] EntityIds) : PacketBase;
