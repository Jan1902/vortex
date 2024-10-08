﻿using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.World.ChunkData.Palettes.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.World.ChunkData.Palettes;

internal class SingleValuedPalette(IGlobalPaletteProvider globalPaletteProvider) : IPalette
{
    private BlockState? _defaultState;

    public int GetBitsPerBlock() => 0;

    public BlockState? GetStateForId(int id) => _defaultState;

    public void Read(IMinecraftBinaryReader reader)
        => _defaultState = globalPaletteProvider.GetStateFromId(reader.ReadVarInt());
}