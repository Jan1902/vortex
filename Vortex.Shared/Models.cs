namespace Vortex.Shared;

public record Vector3i(int X, int Y, int Z);
public record Vector2i(int X, int Z);
public record Vector3f(float X, float Y, float Z);

public record BlockState(int Id, string BlockName);
public record Chunk(ChunkSection[] Sections);
public record ChunkSection(BlockState?[,,] States);