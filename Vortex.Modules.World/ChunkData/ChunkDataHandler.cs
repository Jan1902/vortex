using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.World.ChunkData.Palettes;
using Vortex.Shared;

namespace Vortex.Modules.World.ChunkData;

internal class ChunkDataHandler(PaletteFactory paletteFactory, IMinecraftBinaryReaderFactory binaryReaderFactory)
{
    public Chunk HandleChunkData(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = binaryReaderFactory.GetReader(stream);

        var sections = new ChunkSection[24];

        // Read all chunk sections in a chunk column
        for (var i = 0; i < 24; i++)
            sections[i] = HandleChunkSection(reader);

        return new Chunk(sections);
    }

    private ChunkSection HandleChunkSection(IMinecraftBinaryReader reader)
    {
        _ = reader.ReadShort(); // Block count

        // Start reading block states
        var bitsPerEntry = reader.ReadByte();

        var palette = paletteFactory.CreatePalette(bitsPerEntry, false);
        palette.Read(reader);

        // Overwrite bits per block with the actual value
        bitsPerEntry = (byte)palette.GetBitsPerBlock();

        var states = new BlockState?[16, 16, 16];

        // If bits per entry is 0, the palette is a single value palette
        if (bitsPerEntry == 0)
        {
            // Get the default state
            var state = palette.GetStateForId(0);

            // Length of empty data array, always 0
            _ = reader.ReadVarInt();

            // Fill chunk section with the default state
            for (var y = 0; y < 16; y++)
            {
                for (var z = 0; z < 16; z++)
                {
                    for (var x = 0; x < 16; x++)
                    {
                        states[x, y, z] = state;
                    }
                }
            }

            DiscardBiomeData();

            return new ChunkSection(states);
        }

        // Read the data array
        var dataLength = reader.ReadVarInt();
        var data = new ulong[dataLength];

        for (var i = 0; i < data.Length; i++)
            data[i] = reader.ReadULong();

        // Compated array as helper for reading the data array
        var compactedArray = new CompactedDataArray(data, bitsPerEntry);

        // Fill chunk with block states
        for (var y = 0; y < 16; y++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var blockNumber = (y * 16 + z) * 16 + x;
                    var state = palette.GetStateForId((int)compactedArray[blockNumber]);

                    states[x, y, z] = state;
                }
            }
        }

        // Do the same for biomes but discard the data
        DiscardBiomeData();

        return new ChunkSection(states);

        void DiscardBiomeData()
        {
            var bitsPerEntry = reader.ReadByte();
            var palette = paletteFactory.CreatePalette(bitsPerEntry, true);
            palette.Read(reader);

            var length = reader.ReadVarInt();

            for (var i = 0; i < length; i++)
                reader.ReadLong();
        }
    }
}
