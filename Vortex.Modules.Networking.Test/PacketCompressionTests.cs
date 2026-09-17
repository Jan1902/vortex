using System.Text;
using Vortex.Modules.Networking.PacketHandling;

namespace Vortex.Modules.Networking.Test;

public class PacketCompressionTests
{
    [Fact]
    public void RoundTripsData()
    {
        var original = Encoding.UTF8.GetBytes("minecraft:overworld");

        var compressed = PacketCompression.Compress(original);
        var result = PacketCompression.Decompress(compressed, original.Length);

        Assert.Equal(original, result);
    }

    [Fact]
    public void RoundTripsLargeRepetitiveData()
    {
        // Chunk payloads are large and highly repetitive, which is the case
        // compression actually exists for.
        var original = new byte[128 * 1024];
        for (var i = 0; i < original.Length; i++)
            original[i] = (byte)(i % 7);

        var compressed = PacketCompression.Compress(original);

        Assert.True(compressed.Length < original.Length, "repetitive data should shrink");
        Assert.Equal(original, PacketCompression.Decompress(compressed, original.Length));
    }

    [Fact]
    public void RoundTripsEmptyData()
    {
        var compressed = PacketCompression.Compress([]);

        Assert.Equal([], PacketCompression.Decompress(compressed, 0));
    }

    [Fact]
    public void DecompressThrowsWhenPayloadIsShorterThanAnnounced()
    {
        var compressed = PacketCompression.Compress(Encoding.UTF8.GetBytes("short"));

        // The server announced more bytes than the stream actually contains.
        Assert.Throws<InvalidDataException>(() => PacketCompression.Decompress(compressed, 4096));
    }

    [Fact]
    public void DecompressThrowsOnCorruptData()
        => Assert.ThrowsAny<Exception>(() => PacketCompression.Decompress([0xde, 0xad, 0xbe, 0xef], 16));
}
