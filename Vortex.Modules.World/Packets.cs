using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.World;

[AutoSerializedPacket(PacketIds.Play.ClientBound.ChunkBatchStart)]
public record ChunkBatchStart : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.LevelChunkWithLight)]
public record ChunkDataAndUpdateLight([OverwriteType(OverwriteType.Int)] int ChunkX, [OverwriteType(OverwriteType.Int)] int ChunkZ, NbtTag Heightmaps, byte[] Data/*, BlockEntity[] BlockEntities*/) : PacketBase;

[PacketModel]
public record BlockEntity(byte PackedXZ, short Y, int Type, string Data);

/// <summary>
/// One block changed. Sent whenever anything edits the world after the chunk it
/// sits in was handed out.
/// </summary>
[AutoSerializedPacket(PacketIds.Play.ClientBound.BlockUpdate)]
public record BlockUpdate(Vector3i Location, int BlockStateId) : PacketBase;

/// <summary>
/// Several blocks in one chunk section changed in the same tick, as a piston,
/// an explosion or a growing tree does.
/// </summary>
/// <param name="SectionPosition">
/// The section, packed as 22 bits of chunk X, 20 bits of section Y and 22 bits
/// of chunk Z.
/// </param>
/// <param name="Blocks">
/// One entry per changed block, each packing the new block state id together
/// with the block's position inside the section.
/// </param>
[AutoSerializedPacket(PacketIds.Play.ClientBound.SectionBlocksUpdate)]
public record SectionBlocksUpdate(long SectionPosition, [OverwriteType(OverwriteType.VarLong)] long[] Blocks) : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ClientBound.ChunkBatchFinished)]
public record ChunkBatchFinished : PacketBase;

[AutoSerializedPacket(PacketIds.Play.ServerBound.ChunkBatchReceived, packetDirection: PacketDirection.ServerBound)]
public record ChunkBatchReceived(float ChunksPerTick) : PacketBase;