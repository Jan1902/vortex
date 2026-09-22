using System.Linq;
using Vortex.CodeGeneration;

namespace Vortex.Modules.Networking.CodeGeneration.Serialization;

/// <summary>
/// Renders the serializer of a <see cref="SerializableType"/> from the templates
/// in <c>Serializer.ct</c>, <c>Writing.ct</c> and <c>Reading.ct</c>.
/// </summary>
/// <remarks>
/// All code lives in the templates. What is left here is choosing the snippet for
/// each field and nesting them: an element goes into an array loop, which goes
/// into a presence check.
/// </remarks>
internal static class SerializerRenderer
{
    public static string Render(SerializableType type)
    {
        var construct = Reading("Construct")
            .Set("type", type.Type)
            .Set("arguments", string.Join(", ", type.Fields.Select(f => f.Variable)))
            .Render();

        var code = CodeTemplate.Get("Serializer", type.Kind.ToString())
            .Set("namespace", type.Namespace)
            .Set("accessibility", type.Accessibility)
            .Set("name", type.Name)
            .Set("type", type.Type)
            .Set("serialize", string.Join("\n", type.Fields.Select(RenderWrite)))
            .Set("deserialize", string.Join("\n", type.Fields.Select(RenderRead).Append(construct)))
            .Render();

        return CodeFormatter.Format(code);
    }

    private static string RenderWrite(SerializedField field)
    {
        var access = $"subject.{field.Name}";

        // Inside a presence check the value is the non-null local the check produced.
        var value = field.IsConditional ? $"{field.Variable}Value" : access;

        var content = field.Shape switch
        {
            FieldShape.BitSet => Writing("WriteBitSet")
                .Set("value", value),
            FieldShape.Bytes => Writing("WriteBytes")
                .Set("lengthPrefix", WriteLengthPrefix(field, value))
                .Set("value", value),
            FieldShape.Array => Writing("WriteArray")
                .Set("lengthPrefix", WriteLengthPrefix(field, value))
                .Set("value", value)
                .Set("content", WriteElement(field, "item")),
            _ => WriteElementTemplate(field, value),
        };

        if (!field.IsConditional)
            return content.Render();

        return Writing("WriteConditional")
            .Set("value", access)
            .Set("local", value)
            .Set("content", content.Render())
            .Render();
    }

    private static string WriteElement(SerializedField field, string value)
        => WriteElementTemplate(field, value).Render();

    private static CodeTemplate WriteElementTemplate(SerializedField field, string value)
        => field.Method is null
            ? Writing("WriteModel").Set("serializer", field.Serializer!).Set("value", value)
            : Writing("Write").Set("method", field.Method).Set("cast", field.WriteCast).Set("value", value);

    private static string WriteLengthPrefix(SerializedField field, string value)
        => field.FixedLength is null
            ? Writing("WriteLengthPrefix").Set("value", value).Render()
            : "";

    private static string RenderRead(SerializedField field)
    {
        // Inside a presence check the variable is already declared, as nullable.
        var target = field.IsConditional ? field.Variable : $"var {field.Variable}";

        var content = field.Shape switch
        {
            FieldShape.BitSet => Reading("ReadBitSet")
                .Set("target", target)
                .Set("length", Length(field)),
            FieldShape.Bytes => Reading("ReadBytes")
                .Set("lengthPrefix", ReadLengthPrefix(field))
                .Set("target", target)
                .Set("length", Length(field)),
            FieldShape.Array => Reading("ReadArray")
                .Set("lengthPrefix", ReadLengthPrefix(field))
                .Set("target", target)
                .Set("variable", field.Variable)
                .Set("elementType", field.ElementType)
                .Set("length", Length(field))
                .Set("value", ReadElement(field)),
            _ => Reading("Assign")
                .Set("target", target)
                .Set("value", ReadElement(field)),
        };

        if (!field.IsConditional)
            return content.Render();

        return Reading("ReadConditional")
            .Set("type", field.ValueType)
            .Set("variable", field.Variable)
            .Set("content", content.Render())
            .Render();
    }

    private static string ReadElement(SerializedField field)
        => field.Method is null
            ? Reading("ReadModel").Set("serializer", field.Serializer!).Render()
            : Reading("Read").Set("method", field.Method).Set("cast", field.ReadCast).Render();

    private static string ReadLengthPrefix(SerializedField field)
        => field.FixedLength is null
            ? Reading("ReadLengthPrefix").Set("variable", field.Variable).Render()
            : "";

    /// <summary>
    /// The element count: fixed by an attribute, or read from the prefix before it.
    /// </summary>
    private static string Length(SerializedField field)
        => field.FixedLength?.ToString() ?? $"{field.Variable}Length";

    private static CodeTemplate Writing(string section)
        => CodeTemplate.Get("Writing", section);

    private static CodeTemplate Reading(string section)
        => CodeTemplate.Get("Reading", section);
}
