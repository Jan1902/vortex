using Microsoft.IO;
using System.Buffers;
using Vortex.Modules.Networking.CustomTypes;

namespace Vortex.Modules.Networking.PacketHandling;

/// <summary>
/// Wraps and unwraps the packet frames that are sent over the wire.
/// </summary>
/// <remarks>
/// A frame is a length prefix followed by a payload. Before the server enables
/// compression the payload is the packet body directly. Afterwards the payload is
/// prefixed with the uncompressed length, which is zero when the body was below the
/// threshold and therefore left uncompressed.
/// </remarks>
internal static class PacketFraming
{
    /// <summary>
    /// Value of the compression threshold while the server has not enabled compression.
    /// </summary>
    public const int CompressionDisabled = -1;

    private static readonly RecyclableMemoryStreamManager _streamManager = new();

    /// <summary>
    /// Takes one complete frame off the front of the buffer.
    /// </summary>
    /// <param name="buffer">The received data. Advanced past the frame on success.</param>
    /// <param name="payload">The frame without its length prefix.</param>
    /// <returns><c>false</c> if the buffer does not hold a complete frame yet.</returns>
    public static bool TryReadFrame(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> payload)
    {
        payload = default;

        var reader = new SequenceReader<byte>(buffer);

        // The length prefix itself can be split across segments.
        if (!VarInt.TryReadVarInt(ref reader, out var length))
            return false;

        if (length < 0)
            throw new InvalidDataException($"Packet frame announced a negative length of {length}");

        if (reader.Remaining < length)
            return false;

        payload = buffer.Slice(reader.Position, length);
        buffer = buffer.Slice(payload.End);

        return true;
    }

    /// <summary>
    /// Unwraps a frame payload into the op code and the packet body.
    /// </summary>
    /// <param name="payload">The frame without its length prefix.</param>
    /// <param name="compressionThreshold">The active threshold, or <see cref="CompressionDisabled"/>.</param>
    /// <param name="opCode">The op code of the packet.</param>
    /// <param name="body">The packet body without the op code.</param>
    /// <returns><c>false</c> if the payload was malformed.</returns>
    public static bool TryUnwrapPayload(ReadOnlySpan<byte> payload, int compressionThreshold, out int opCode, out byte[] body)
    {
        opCode = 0;
        body = [];

        var decompressed = payload;

        if (compressionThreshold >= 0)
        {
            if (!VarInt.TryReadVarInt(payload, out var uncompressedLength, out var prefixSize))
                return false;

            var remainder = payload[prefixSize..];

            // A length of zero means the server left this packet uncompressed
            // because the body was below the threshold.
            decompressed = uncompressedLength == 0
                ? remainder
                : PacketCompression.Decompress(remainder, uncompressedLength);
        }

        if (!VarInt.TryReadVarInt(decompressed, out opCode, out var opCodeSize))
            return false;

        body = decompressed[opCodeSize..].ToArray();

        return true;
    }

    /// <summary>
    /// Builds a complete frame, ready to be written to the socket.
    /// </summary>
    /// <param name="opCode">The op code of the packet.</param>
    /// <param name="data">The serialized packet fields.</param>
    /// <param name="compressionThreshold">The active threshold, or <see cref="CompressionDisabled"/>.</param>
    public static byte[] BuildFrame(int opCode, ReadOnlySpan<byte> data, int compressionThreshold)
    {
        using var bodyStream = _streamManager.GetStream();
        bodyStream.WriteVarInt(opCode);
        bodyStream.Write(data);
        var body = bodyStream.ToArray();

        using var payloadStream = _streamManager.GetStream();

        if (compressionThreshold >= 0)
        {
            if (body.Length >= compressionThreshold)
            {
                payloadStream.WriteVarInt(body.Length);
                payloadStream.Write(PacketCompression.Compress(body));
            }
            else
            {
                payloadStream.WriteVarInt(0);
                payloadStream.Write(body);
            }
        }
        else
        {
            payloadStream.Write(body);
        }

        var payload = payloadStream.ToArray();

        using var frameStream = _streamManager.GetStream();
        frameStream.WriteVarInt(payload.Length);
        frameStream.Write(payload);

        return frameStream.ToArray();
    }
}
