using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Vortex.Data;

/// <summary>
/// One state of a block: the block together with the values of its properties,
/// identified by the state ID the server sends.
/// </summary>
/// <remarks>
/// There is one instance per state ID, so states can be compared by reference
/// and holding many of them, as a chunk does, costs nothing extra.
/// </remarks>
public sealed class BlockState
{
    private static readonly BlockState?[] _states = new BlockState?[BlockStateTable.StateCount];

    private BlockState(int id, Block block)
    {
        Id = id;
        Block = block;
    }

    /// <summary>The state ID, as used on the wire and in chunk palettes.</summary>
    public int Id { get; }

    /// <summary>The block this is a state of.</summary>
    public Block Block { get; }

    /// <summary>Whether this is the state the block is placed in when nothing says otherwise.</summary>
    public bool IsDefault => BlockStateTable.DefaultStateIds[(int)Block] == Id;

    /// <summary>
    /// Gets the state with the given ID.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The ID belongs to no state.</exception>
    public static BlockState FromId(int id)
        => TryFromId(id, out var state) ? state : throw new ArgumentOutOfRangeException(nameof(id), id, "No block state has this ID.");

    /// <summary>
    /// Gets the state with the given ID, if there is one.
    /// </summary>
    public static bool TryFromId(int id, [NotNullWhen(true)] out BlockState? state)
    {
        if (id < 0 || id >= BlockStateTable.StateCount)
        {
            state = null;
            return false;
        }

        // A race here only builds the same state twice; either copy is correct.
        state = _states[id] ??= new BlockState(id, BlockOf(id));
        return true;
    }

    /// <summary>
    /// Gets the state a block is placed in when nothing says otherwise.
    /// </summary>
    public static BlockState Default(Block block)
        => FromId(BlockStateTable.DefaultStateIds[(int)block]);

    /// <summary>Reads a property that holds <c>true</c> or <c>false</c>.</summary>
    /// <returns>The value, or <c>null</c> if this block does not have the property.</returns>
    public bool? Get(BoolBlockProperty property)
        => RawValue(property.Property) is { } value ? value != 0 : null;

    /// <summary>Reads a property that holds a number.</summary>
    /// <returns>The value, or <c>null</c> if this block does not have the property.</returns>
    public int? Get(IntBlockProperty property)
        => RawValue(property.Property);

    /// <summary>Reads a property that holds one of a set of names.</summary>
    /// <returns>The value, or <c>null</c> if this block does not have the property.</returns>
    public TValue? Get<TValue>(EnumBlockProperty<TValue> property)
        where TValue : struct, Enum
        => RawValue(property.Property) is { } value ? Unsafe.As<int, TValue>(ref value) : null;

    /// <summary>
    /// Whether this block has the property at all.
    /// </summary>
    public bool Has(BlockProperty property)
        => BlockStateTable.Properties[(int)Block].Any(definition => definition.Property == property);

    public override string ToString()
    {
        var properties = BlockStateTable.Properties[(int)Block];

        return properties.Length == 0
            ? Block.ToString()
            : $"{Block}[{string.Join(", ", properties.Select(p => $"{p.Property}={RawValue(p.Property)}"))}]";
    }

    /// <summary>
    /// Decodes one property from the state ID. The IDs of a block count through
    /// every combination of its properties with the last one changing fastest,
    /// like the digits of a number whose places have different bases.
    /// </summary>
    private int? RawValue(BlockProperty property)
    {
        var definitions = BlockStateTable.Properties[(int)Block];
        var offset = Id - BlockStateTable.FirstStateIds[(int)Block];

        for (var i = definitions.Length - 1; i >= 0; i--)
        {
            var values = definitions[i].Values;

            if (definitions[i].Property == property)
                return values[offset % values.Length];

            offset /= values.Length;
        }

        return null;
    }

    private static Block BlockOf(int id)
    {
        var index = Array.BinarySearch(BlockStateTable.FirstStateIds, id);

        // Not a first state: the block is the one whose range started just before.
        return (Block)(index >= 0 ? index : ~index - 1);
    }
}
