using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Interaction;

/// <summary>
/// Uses the item in hand on a side of a block.
/// </summary>
/// <param name="CursorX">Where on the side it is clicked, 0 to 1.</param>
/// <param name="InsideBlock">Whether the player's head is inside the block, as when clicking from within scaffolding.</param>
/// <param name="Sequence">Numbers the act, so the server can confirm it.</param>
[AutoSerializedPacket(PacketIds.Play.ServerBound.UseItemOn, packetDirection: PacketDirection.ServerBound)]
public record UseItemOn(
    Hand Hand,
    Vector3i Location,
    BlockFace Face,
    float CursorX,
    float CursorY,
    float CursorZ,
    bool InsideBlock,
    int Sequence) : PacketBase;

/// <summary>Uses the item in hand on its own.</summary>
[AutoSerializedPacket(PacketIds.Play.ServerBound.UseItem, packetDirection: PacketDirection.ServerBound)]
public record UseItem(Hand Hand, int Sequence, float Yaw, float Pitch) : PacketBase;

/// <summary>Swings an arm.</summary>
[AutoSerializedPacket(PacketIds.Play.ServerBound.Swing, packetDirection: PacketDirection.ServerBound)]
public record Swing(Hand Hand) : PacketBase;

/// <summary>
/// The server has dealt with every act numbered up to this one. Sent once per
/// tick for the highest number it saw.
/// </summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.BlockChangedAck)]
public record BlockChangedAck(int Sequence) : PacketBase;
