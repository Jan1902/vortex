using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.World;

internal class DirectPalette(IGlobalPaletteProvider globalPaletteProvider) : IPalette
{
    public int GetBitsPerBlock()
        => (int)Math.Ceiling(Math.Log2(15));

    public BlockState? GetStateForId(int id)
        => globalPaletteProvider.GetStateFromId(id);

    public void Read(IMinecraftBinaryReader reader) { }
}