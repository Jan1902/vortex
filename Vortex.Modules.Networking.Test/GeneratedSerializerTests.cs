using Vortex.Data;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.Networking.Data;
using Vortex.Modules.Networking.Packets;
using Vortex.Shared;

namespace Vortex.Modules.Networking.Test;

public enum SampleMode
{
    Idle,
    Walking,
    Running = 300
}

[Flags]
public enum SampleFlags : byte
{
    None = 0,
    First = 0x01,
    Second = 0x02
}

/// <summary>
/// Exercises every way the generator can lay out a field. Declared as models
/// rather than packets so they stay out of the packet registry.
/// </summary>
[PacketModel]
public record AllShapes(
    int Number,
    long Big,
    string Text,
    Guid Id,
    Vector3i Location,
    bool Toggle,
    SampleMode Mode,
    [BitField] SampleFlags Flags,
    [OverwriteType(OverwriteType.Int)] int FixedWidth,
    [OverwriteType(OverwriteType.VarLong)] long Packed,
    [OverwriteType(OverwriteType.Angle)] float Heading,
    SampleMode[] Modes,
    string[] Names,
    byte[] Payload,
    [Length(4)] byte[] FixedPayload,
    [BitSet(10)] bool[] Bits,
    [Conditional] int? OptionalNumber,
    [Conditional] string? OptionalText,
    [Conditional] SampleNested[]? OptionalNested,
    SampleNested Nested,
    ItemStack? Held,
    ItemStack? Empty,
    int Reader,
    int Event);

[PacketModel]
public record SampleNested(string Name, [Conditional] byte[]? Data);

public class GeneratedSerializerTests
{
    [Fact]
    public void RoundTripsEveryFieldShape()
    {
        var original = new AllShapes(
            Number: 300,
            Big: long.MaxValue,
            Text: "hello",
            Id: Guid.NewGuid(),
            Location: new Vector3i(10, -64, -20),
            Toggle: true,
            Mode: SampleMode.Running,
            Flags: SampleFlags.First | SampleFlags.Second,
            FixedWidth: -5,
            Packed: 1L << 40,
            Heading: 90f,
            Modes: [SampleMode.Idle, SampleMode.Running],
            Names: ["a", "bc"],
            Payload: [1, 2, 3],
            FixedPayload: [9, 8, 7, 6],
            Bits: [true, false, false, true, false, false, false, false, false, true],
            OptionalNumber: 42,
            OptionalText: "present",
            OptionalNested: [new SampleNested("inner", [5]), new SampleNested("empty", null)],
            Nested: new SampleNested("outer", null),
            Held: new ItemStack(Item.Torch, 5),
            Empty: null,
            Reader: 7,
            Event: 8);

        var copy = RoundTrip(original);

        Assert.Equal(original.Number, copy.Number);
        Assert.Equal(original.Big, copy.Big);
        Assert.Equal(original.Text, copy.Text);
        Assert.Equal(original.Id, copy.Id);
        Assert.Equal(original.Location, copy.Location);
        Assert.Equal(original.Toggle, copy.Toggle);
        Assert.Equal(original.Mode, copy.Mode);
        Assert.Equal(original.Flags, copy.Flags);
        Assert.Equal(original.FixedWidth, copy.FixedWidth);
        Assert.Equal(original.Packed, copy.Packed);
        Assert.Equal(original.Heading, copy.Heading);
        Assert.Equal(original.Modes, copy.Modes);
        Assert.Equal(original.Names, copy.Names);
        Assert.Equal(original.Payload, copy.Payload);
        Assert.Equal(original.FixedPayload, copy.FixedPayload);
        Assert.Equal(original.Bits, copy.Bits);
        Assert.Equal(original.OptionalNumber, copy.OptionalNumber);
        Assert.Equal(original.OptionalText, copy.OptionalText);
        Assert.NotNull(copy.OptionalNested);
        Assert.Equal(2, copy.OptionalNested.Length);
        Assert.Equal("inner", copy.OptionalNested[0].Name);
        Assert.Equal([5], copy.OptionalNested[0].Data!);
        Assert.Null(copy.OptionalNested[1].Data);
        Assert.Equal("outer", copy.Nested.Name);
        Assert.Equal(original.Held, copy.Held);
        Assert.Null(copy.Empty);
        Assert.Equal(original.Reader, copy.Reader);
        Assert.Equal(original.Event, copy.Event);
    }

    [Fact]
    public void RoundTripsAbsentConditionals()
    {
        var original = new AllShapes(
            0, 0, "", Guid.Empty, Vector3i.Zero, false, SampleMode.Idle, SampleFlags.None, 0, 0, 0f,
            [], [], [], [0, 0, 0, 0], new bool[10],
            OptionalNumber: null, OptionalText: null, OptionalNested: null,
            new SampleNested("", null), null, null, 0, 0);

        var copy = RoundTrip(original);

        Assert.Null(copy.OptionalNumber);
        Assert.Null(copy.OptionalText);
        Assert.Null(copy.OptionalNested);
    }

    [Fact]
    public void WritesPresenceBeforeTheLengthOfAnOptionalByteArray()
    {
        var bytes = Write(new SampleNested("x", [1, 2]), (s, w) => new SampleNestedSerializer().SerializeModel(s, w));

        // Name, then present, then length, then the bytes.
        Assert.Equal(new byte[] { 1, (byte)'x', 1, 2, 1, 2 }, bytes);
    }

    [Fact]
    public void ReadsPresenceBeforeTheLengthOfAnOptionalByteArray()
    {
        using var stream = new MemoryStream([1, (byte)'x', 1, 2, 1, 2]);

        var nested = new SampleNestedSerializer().DeserializeModel(new MinecraftBinaryReader(stream));

        Assert.Equal([1, 2], nested.Data!);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void WritesOnlyPresenceForAnAbsentConditional()
    {
        var bytes = Write(new SampleNested("x", null), (s, w) => new SampleNestedSerializer().SerializeModel(s, w));

        Assert.Equal(new byte[] { 1, (byte)'x', 0 }, bytes);
    }

    [Fact]
    public void RoundTripsAPacketWithAnArrayOfModels()
    {
        var original = new LoginSuccessPacket(Guid.NewGuid(), "Vortex", [new Property("textures", "abc", true, "sig")], StrictErrorHandling: true);
        var serializer = new LoginSuccessPacketSerializer();

        using var stream = new MemoryStream(Write(original, serializer.SerializePacket));
        var copy = serializer.DeserializePacket(new MinecraftBinaryReader(stream));

        Assert.Equal(original.Uuid, copy.Uuid);
        Assert.Equal(original.Username, copy.Username);
        Assert.Equal(original.Properties, copy.Properties);
        Assert.True(copy.StrictErrorHandling);
    }

    private static AllShapes RoundTrip(AllShapes original)
    {
        var serializer = new AllShapesSerializer();

        using var stream = new MemoryStream(Write(original, serializer.SerializeModel));
        var copy = serializer.DeserializeModel(new MinecraftBinaryReader(stream));

        Assert.Equal(stream.Length, stream.Position);

        return copy;
    }

    private static byte[] Write<T>(T subject, Action<T, IMinecraftBinaryWriter> serialize)
    {
        using var stream = new MemoryStream();
        serialize(subject, new MinecraftBinaryWriter(stream));

        return stream.ToArray();
    }
}
