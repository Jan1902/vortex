using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.World.ChunkData.Palettes.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.World.ChunkData.Palettes;

internal class IndirectPalette(int bitsPerBlock, IGlobalPaletteProvider globalPaletteProvider) : IPalette
{
    private readonly Dictionary<int, BlockState> _idToState = [];

    public BlockState? GetStateForId(int id)
        => _idToState.TryGetValue(id, out BlockState? value) ? value : null;

    public int GetBitsPerBlock()
        => bitsPerBlock;

    public void Read(IMinecraftBinaryReader reader)
    {
        var length = reader.ReadVarInt();

        for (var id = 0; id < length; id++)
        {
            var stateId = reader.ReadVarInt();
            var state = globalPaletteProvider.GetStateFromId(stateId);

            _idToState[id] = state;
        }
    }
}
