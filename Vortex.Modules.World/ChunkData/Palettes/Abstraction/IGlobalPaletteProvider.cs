using Vortex.Shared;

namespace Vortex.Modules.World.ChunkData.Palettes.Abstraction;

internal interface IGlobalPaletteProvider
{
    BlockState GetStateFromId(int id);
}