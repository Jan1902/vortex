namespace Vortex.Modules.Networking.CodeGeneration.Serialization;

/// <summary>
/// Whether a serializer is generated for a packet or for a model nested in one.
/// </summary>
internal enum SerializerKind
{
    Packet,
    Model
}

/// <summary>
/// How a field is laid out on the wire.
/// </summary>
internal enum FieldShape
{
    /// <summary>One value.</summary>
    Single,

    /// <summary>Several values, one after the other.</summary>
    Array,

    /// <summary>A byte array, written as one block instead of byte by byte.</summary>
    Bytes,

    /// <summary>A bool array packed into bits.</summary>
    BitSet
}

/// <summary>
/// A record that gets a generated serializer, reduced to what the templates need.
/// </summary>
/// <remarks>
/// Holds only strings and numbers, never Roslyn symbols, so the incremental
/// pipeline can compare it with the previous run and skip unchanged types.
/// </remarks>
/// <param name="Type">The fully qualified name of the record.</param>
/// <param name="Fields">The record's positional parameters, in wire order.</param>
internal sealed record SerializableType(
    SerializerKind Kind,
    string Namespace,
    string Name,
    string Type,
    string Accessibility,
    EquatableArray<SerializedField> Fields)
{
    public string HintName => $"{Namespace}.{Name}_Serializer.g.cs";
}

/// <summary>
/// One field of a <see cref="SerializableType"/>.
/// </summary>
/// <param name="Name">The property on the record.</param>
/// <param name="Variable">The local the deserializer reads it into.</param>
/// <param name="ValueType">The field's type without nullability, e.g. <c>int</c> or <c>global::Foo[]</c>.</param>
/// <param name="ElementType">The type of a single element; the same as <paramref name="ValueType"/> for <see cref="FieldShape.Single"/>.</param>
/// <param name="Method">The reader and writer method suffix of an element, or <c>null</c> for a model and for shapes with a template of their own.</param>
/// <param name="Serializer">The serializer of a model element, or <c>null</c> for a primitive.</param>
/// <param name="WriteCast">A cast applied before writing, e.g. <c>(int)</c> for an enum.</param>
/// <param name="ReadCast">A cast applied after reading, e.g. <c>(global::Foo)</c> for an enum.</param>
/// <param name="FixedLength">The element count when it is not prefixed, or the length of a bit set.</param>
/// <param name="IsConditional">Whether a bool precedes the field saying if it is present.</param>
internal sealed record SerializedField(
    string Name,
    string Variable,
    FieldShape Shape,
    string ValueType,
    string ElementType,
    string? Method,
    string? Serializer,
    string WriteCast,
    string ReadCast,
    int? FixedLength,
    bool IsConditional);
