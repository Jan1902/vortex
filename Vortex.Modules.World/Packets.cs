using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.World;

[AutoSerializedPacket(PacketIds.Play.ClientBound.ChunkBatchStart)]
public record ChunkBatchStart : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.LevelChunkWithLight)]
public record ChunkDataAndUpdateLight([OverwriteType(OverwriteType.Int)] int ChunkX, [OverwriteType(OverwriteType.Int)] int ChunkZ, NbtTag Heightmaps, byte[] Data/*, BlockEntity[] BlockEntities*/) : PacketBase;

[PacketModel]
public record BlockEntity(byte PackedXZ, short Y, int Type, string Data);

[AutoSerializedPacket(PacketIds.Play.ClientBound.ChunkBatchFinished)]
public record ChunkBatchFinished : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ServerBound.ChunkBatchReceived, packetDirection: PacketDirection.ServerBound)]
public record ChunkBatchReceived(float ChunksPerTick) : PacketBase;