namespace Vortex.Shared;

public abstract class NbtTag
{
    public string? Name { get; set; }

    protected NbtTag(string? name) => Name = name;
}

public class CompoundTag : NbtTag
{
    public IEnumerable<NbtTag> Children { get; set; }

    public CompoundTag(IEnumerable<NbtTag> children, string? name = null) : base(name)
        => Children = children;

    public CompoundTag(string? name = null) : base(name)
        => Children = new List<NbtTag>();
}

public class IntTag : NbtTag
{
    public int? Value { get; set; }

    public IntTag(int value, string? name = null) : base(name)
        => Value = value;

    public IntTag(string? name = null) : base(name) { }
}

public class ByteTag : NbtTag
{
    public byte? Value { get; set; }

    public ByteTag(byte value, string? name = null) : base(name)
        => Value = value;

    public ByteTag(string? name = null) : base(name) { }
}

public class ShortTag : NbtTag
{
    public short? Value { get; set; }

    public ShortTag(short value, string? name = null) : base(name)
        => Value = value;

    public ShortTag(string? name = null) : base(name) { }
}

public class LongTag : NbtTag
{
    public long? Value { get; set; }

    public LongTag(long value, string? name = null) : base(name)
        => Value = value;

    public LongTag(string? name = null) : base(name) { }
}

public class FloatTag : NbtTag
{
    public float? Value { get; set; }

    public FloatTag(float value, string? name = null) : base(name)
        => Value = value;

    public FloatTag(string? name = null) : base(name) { }
}

public class DoubleTag : NbtTag
{
    public double? Value { get; set; }

    public DoubleTag(double value, string? name = null) : base(name)
        => Value = value;

    public DoubleTag(string? name = null) : base(name) { }
}

public class ByteArrayTag : NbtTag
{
    public byte[]? Value { get; set; }

    public ByteArrayTag(byte[] value, string? name = null) : base(name)
        => Value = value;

    public ByteArrayTag(string? name = null) : base(name) { }
}

public class StringTag : NbtTag
{
    public string? Value { get; set; }

    public StringTag(string value, string? name = null) : base(name)
        => Value = value;

    public StringTag(string? name = null) : base(name) { }
}

public class ListTag : NbtTag
{
    public Type? TagType { get; set; }
    public IEnumerable<NbtTag> Items { get; set; }

    public ListTag(Type tagType, IEnumerable<NbtTag> items, string? name = null) : base(name)
    {
        TagType = tagType;
        Items = items;
    }

    public ListTag(Type tagType, string? name = null) : base(name)
    {
        TagType = tagType;
        Items = new List<NbtTag>();
    }

    public ListTag(string? name = null) : base(name)
        => Items = new List<NbtTag>();
}

public class IntArrayTag : NbtTag
{
    public int[] Items { get; set; }

    public IntArrayTag(int[] items, string? name = null) : base(name)
        => Items = items;

    public IntArrayTag(string? name = null) : base(name)
        => Items = Array.Empty<int>();
}

public class LongArrayTag : NbtTag
{
    public long[] Items { get; set; }

    public LongArrayTag(long[] items, string? name = null) : base(name)
        => Items = items;

    public LongArrayTag(string? name = null) : base(name)
        => Items = Array.Empty<long>();
}

public class EndTag : NbtTag
{
    public EndTag() : base(null) { }
}