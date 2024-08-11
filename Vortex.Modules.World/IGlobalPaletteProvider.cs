using Vortex.Shared;

namespace Vortex.Modules.World;

internal interface IGlobalPaletteProvider
{
    BlockState GetStateFromId(int id);
}