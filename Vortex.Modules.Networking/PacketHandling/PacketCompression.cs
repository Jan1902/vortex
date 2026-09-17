using System.IO.Compression;

namespace Vortex.Modules.Networking.PacketHandling;

/// <summary>
/// Zlib compression for the packet format that is used once the server has sent
/// a Set Compression packet.
/// </summary>
internal static class PacketCompression
{
    /// <summary>
    /// Compresses the uncompressed body of a packet.
    /// </summary>
    /// <param name="data">The uncompressed data.</param>
    /// <returns>The zlib compressed data.</returns>
    public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        using var output = new MemoryStream();

        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data);

        return output.ToArray();
    }

    /// <summary>
    /// Decompresses the body of a compressed packet.
    /// </summary>
    /// <param name="data">The zlib compressed data.</param>
    /// <param name="uncompressedLength">The uncompressed length announced by the server.</param>
    /// <returns>The uncompressed data.</returns>
    /// <exception cref="InvalidDataException">The data did not decompress to the announced length.</exception>
    public static byte[] Decompress(ReadOnlySpan<byte> data, int uncompressedLength)
    {
        using var input = new MemoryStream(data.ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);

        var result = new byte[uncompressedLength];
        var totalRead = 0;

        while (totalRead < uncompressedLength)
        {
            var read = zlib.Read(result, totalRead, uncompressedLength - totalRead);

            // A zero-byte read means the stream ended early, so the announced
            // length and the actual payload disagree.
            if (read == 0)
                throw new InvalidDataException(
                    $"Compressed packet ended after {totalRead} bytes but announced {uncompressedLength} bytes");

            totalRead += read;
        }

        return result;
    }
}
