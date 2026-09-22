namespace Vortex.Data;

/// <summary>
/// A block state property holding <c>true</c> or <c>false</c>, such as
/// <see cref="BlockProperties.Waterlogged"/>.
/// </summary>
public sealed class BoolBlockProperty(BlockProperty property)
{
    public BlockProperty Property { get; } = property;

    public override string ToString() => Property.ToString();
}

/// <summary>
/// A block state property holding a number, such as <see cref="BlockProperties.Age"/>.
/// </summary>
public sealed class IntBlockProperty(BlockProperty property)
{
    public BlockProperty Property { get; } = property;

    public override string ToString() => Property.ToString();
}

/// <summary>
/// A block state property holding one of a set of names, such as
/// <see cref="BlockProperties.Facing"/>.
/// </summary>
/// <typeparam name="TValue">The generated enum of the names.</typeparam>
public sealed class EnumBlockProperty<TValue>(BlockProperty property)
    where TValue : struct, Enum
{
    public BlockProperty Property { get; } = property;

    public override string ToString() => Property.ToString();
}

/// <summary>
/// One property of one block: the values it takes, in the order the state IDs
/// count through them.
/// </summary>
/// <param name="Values">
/// Each value as stored: 1 or 0 for a bool, the number itself, or the position
/// in the property's generated enum.
/// </param>
internal sealed record BlockPropertyDefinition(BlockProperty Property, int[] Values);
