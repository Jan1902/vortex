using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.World;

[AutoSerializedPacket(0x0d)]
public record ChunkBatchStart : PacketBase;

[AutoSerializedPacket(0x27)]
public record ChunkDataAndUpdateLight([OverwriteType(OverwriteType.Int)] int ChunkX, [OverwriteType(OverwriteType.Int)] int ChunkZ, NbtTag Heightmaps, byte[] Data/*, BlockEntity[] BlockEntities*/) : PacketBase;

[PacketModel]
public record BlockEntity(byte PackedXZ, short Y, int Type, string Data);

[AutoSerializedPacket(0x0c)]
public record ChunkBatchFinished : PacketBase;

[AutoSerializedPacket(0x08, packetDirection: PacketDirection.ServerBound)]
public record ChunkBatchReceived(float ChunksPerTick) : PacketBase;