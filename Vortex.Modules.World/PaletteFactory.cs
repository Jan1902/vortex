namespace Vortex.Modules.World;

internal class PaletteFactory(IGlobalPaletteProvider globalPaletteProvider)
{
    public IPalette CreatePalette(int bitsPerBlock, bool isBiome)
    {
        if (bitsPerBlock == 0)
            return new SingleValuedPalette(globalPaletteProvider);
        else if (isBiome && bitsPerBlock <= 3)
            return new IndirectPalette(bitsPerBlock, globalPaletteProvider);
        else if (bitsPerBlock <= 4)
            return new IndirectPalette(4, globalPaletteProvider);
        else if (bitsPerBlock <= 8)
            return new IndirectPalette(bitsPerBlock, globalPaletteProvider);
        else
            return new DirectPalette(globalPaletteProvider);
    }
}
