using System.Buffers;
using System.Text;
using Vortex.Modules.Networking.PacketHandling;

namespace Vortex.Modules.Networking.Test;

public class PacketFramingTests
{
    private const int Threshold = 256;

    [Fact]
    public void RoundTripsUncompressedFrame()
    {
        var data = Encoding.UTF8.GetBytes("Hello world!");

        AssertRoundTrip(opCode: 0x06, data, PacketFraming.CompressionDisabled);
    }

    [Fact]
    public void RoundTripsSmallPacketBelowTheThreshold()
    {
        // Below the threshold the body travels uncompressed, announced by a zero
        // data length, even though compression is enabled.
        var data = Encoding.UTF8.GetBytes("short");

        AssertRoundTrip(opCode: 0x06, data, Threshold);
    }

    [Fact]
    public void RoundTripsLargePacketAboveTheThreshold()
    {
        var data = new byte[4096];
        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)(i % 11);

        AssertRoundTrip(opCode: 0x27, data, Threshold);
    }

    [Fact]
    public void RoundTripsPacketExactlyAtTheThreshold()
    {
        // The body is the op code plus the data, so this lands the body exactly on
        // the threshold, where the "compress or not" decision flips.
        var data = new byte[Threshold - 1];

        AssertRoundTrip(opCode: 0x27, data, Threshold);
    }

    [Fact]
    public void RoundTripsEmptyPacket()
        => AssertRoundTrip(opCode: 0x03, [], PacketFraming.CompressionDisabled);

    [Fact]
    public void ReadsTwoFramesFromOneBuffer()
    {
        var first = PacketFraming.BuildFrame(0x01, Encoding.UTF8.GetBytes("one"), PacketFraming.CompressionDisabled);
        var second = PacketFraming.BuildFrame(0x02, Encoding.UTF8.GetBytes("two"), PacketFraming.CompressionDisabled);

        var buffer = new ReadOnlySequence<byte>([.. first, .. second]);

        Assert.True(PacketFraming.TryReadFrame(ref buffer, out var firstPayload));
        Assert.True(PacketFraming.TryUnwrapPayload(firstPayload.ToArray(), PacketFraming.CompressionDisabled, out var firstOpCode, out var firstBody));
        Assert.Equal(0x01, firstOpCode);
        Assert.Equal("one", Encoding.UTF8.GetString(firstBody));

        Assert.True(PacketFraming.TryReadFrame(ref buffer, out var secondPayload));
        Assert.True(PacketFraming.TryUnwrapPayload(secondPayload.ToArray(), PacketFraming.CompressionDisabled, out var secondOpCode, out var secondBody));
        Assert.Equal(0x02, secondOpCode);
        Assert.Equal("two", Encoding.UTF8.GetString(secondBody));

        Assert.False(PacketFraming.TryReadFrame(ref buffer, out _));
        Assert.Equal(0, buffer.Length);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void WaitsForIncompleteFrames(int availableBytes)
    {
        var frame = PacketFraming.BuildFrame(0x27, new byte[1024], PacketFraming.CompressionDisabled);
        var buffer = new ReadOnlySequence<byte>(frame[..availableBytes]);

        Assert.False(PacketFraming.TryReadFrame(ref buffer, out _));

        // Nothing may be consumed while the frame is incomplete.
        Assert.Equal(availableBytes, buffer.Length);
    }

    [Fact]
    public void ReadsFrameSplitAcrossSegments()
    {
        // A packet arriving in several socket reads is the normal case for anything
        // larger than the receive buffer, including the length prefix being split.
        var frame = PacketFraming.BuildFrame(0x27, new byte[8192], Threshold);
        var buffer = CreateSegmented(frame, segmentSize: 1);

        Assert.True(PacketFraming.TryReadFrame(ref buffer, out var payload));
        Assert.True(PacketFraming.TryUnwrapPayload(payload.ToArray(), Threshold, out var opCode, out var body));

        Assert.Equal(0x27, opCode);
        Assert.Equal(8192, body.Length);
    }

    [Fact]
    public void RejectsNegativeFrameLength()
    {
        // 0xff 0xff 0xff 0xff 0x0f decodes to -1.
        var buffer = new ReadOnlySequence<byte>([0xff, 0xff, 0xff, 0xff, 0x0f, 0x00]);

        Assert.Throws<InvalidDataException>(() =>
        {
            var local = buffer;
            PacketFraming.TryReadFrame(ref local, out _);
        });
    }

    private static void AssertRoundTrip(int opCode, byte[] data, int compressionThreshold)
    {
        var frame = PacketFraming.BuildFrame(opCode, data, compressionThreshold);
        var buffer = new ReadOnlySequence<byte>(frame);

        Assert.True(PacketFraming.TryReadFrame(ref buffer, out var payload));
        Assert.Equal(0, buffer.Length);

        Assert.True(PacketFraming.TryUnwrapPayload(payload.ToArray(), compressionThreshold, out var readOpCode, out var body));
        Assert.Equal(opCode, readOpCode);
        Assert.Equal(data, body);
    }

    /// <summary>
    /// Builds a sequence made of several segments, mimicking data that arrived in
    /// separate socket reads.
    /// </summary>
    private static ReadOnlySequence<byte> CreateSegmented(byte[] data, int segmentSize)
    {
        Segment? first = null;
        Segment? current = null;

        for (var offset = 0; offset < data.Length; offset += segmentSize)
        {
            var length = Math.Min(segmentSize, data.Length - offset);
            var memory = new ReadOnlyMemory<byte>(data, offset, length);

            current = first is null
                ? first = new Segment(memory, 0)
                : current!.Append(memory);
        }

        return new ReadOnlySequence<byte>(first!, 0, current!, current!.Memory.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory, long runningIndex)
        {
            Memory = memory;
            RunningIndex = runningIndex;
        }

        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var segment = new Segment(memory, RunningIndex + Memory.Length);
            Next = segment;

            return segment;
        }
    }
}
