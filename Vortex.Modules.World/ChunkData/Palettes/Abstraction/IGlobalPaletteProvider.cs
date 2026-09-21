using Vortex.Shared;

namespace Vortex.Modules.World.ChunkData.Palettes.Abstraction;

internal interface IGlobalPaletteProvider
{
    BlockState GetStateFromId(int id);

    /// <summary>
    /// Looks up a block state that came from the network, where an id the
    /// palette does not know is possible rather than a programming error.
    /// </summary>
    bool TryGetStateFromId(int id, out BlockState state);
}