﻿using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.World.ChunkData.Palettes.Abstraction;

internal interface IPalette
{
    BlockState? GetStateForId(int id);
    int GetBitsPerBlock();
    void Read(IMinecraftBinaryReader reader);
}
