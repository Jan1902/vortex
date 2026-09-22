using Vortex.Data;
using Vortex.Modules.World.ChunkData.Palettes.Abstraction;

namespace Vortex.Modules.World.ChunkData.Palettes;

/// <summary>
/// The palette of every block state in the game, which chunk sections fall back
/// to when they use too many different blocks for a palette of their own.
/// </summary>
/// <remarks>
/// The states are generated from Mojang's blocks report into Vortex.Data, so
/// there is nothing to load here.
/// </remarks>
internal class GlobalPaletteProvider : IGlobalPaletteProvider
{
    public BlockState GetStateFromId(int id)
        => BlockState.FromId(id);

    public bool TryGetStateFromId(int id, out BlockState state)
        => BlockState.TryFromId(id, out state!);
}
