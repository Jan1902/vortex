using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Vortex.CodeGeneration;

namespace Vortex.Modules.Networking.CodeGeneration.Serialization;

/// <summary>
/// Turns a packet or model record into a <see cref="SerializableType"/>.
/// </summary>
/// <remarks>
/// Works on the semantic model rather than on syntax, so a type is known for what
/// it is: an enum is recognised as one, <c>int?</c> and <c>string?</c> are both
/// understood as nullable, and an alias or a <c>using</c> cannot confuse it.
/// </remarks>
internal static class SerializableTypeReader
{
    public const string AbstractionNamespace = "Vortex.Modules.Networking.Abstraction";

    private const string FallbackNamespace = "Vortex.Generated";

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>
    /// Reader and writer methods for the types the protocol has a direct encoding for.
    /// </summary>
    private static readonly Dictionary<string, string> PrimitiveMethods = new()
    {
        ["byte"] = "Byte",
        ["short"] = "Short",
        ["ushort"] = "UShort",
        ["int"] = "VarInt",
        ["uint"] = "UInt",
        ["long"] = "Long",
        ["ulong"] = "ULong",
        ["float"] = "Float",
        ["double"] = "Double",
        ["bool"] = "Bool",
        ["string"] = "StringWithVarIntPrefix",
        ["global::System.Guid"] = "UUID",
        ["global::Vortex.Shared.Vector3i"] = "Position",
        ["global::Vortex.Shared.NbtTag"] = "NbtTag",
        ["global::Vortex.Data.ItemStack"] = "Slot",
    };

    /// <summary>
    /// The value each reader and writer method works with, which an enum has to be
    /// cast to and from.
    /// </summary>
    private static readonly Dictionary<string, string> MethodValueTypes = new()
    {
        ["Byte"] = "byte",
        ["Short"] = "short",
        ["UShort"] = "ushort",
        ["Int"] = "int",
        ["VarInt"] = "int",
        ["UInt"] = "uint",
        ["Long"] = "long",
        ["VarLong"] = "long",
        ["ULong"] = "ulong",
    };

    /// <summary>
    /// Names the generated code already uses, which a field must not take as its local.
    /// </summary>
    private static readonly HashSet<string> ReservedNames = ["reader", "writer", "subject", "item", "i"];

    public static SerializableType? Read(GeneratorAttributeSyntaxContext context, SerializerKind kind, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type || context.TargetNode is not RecordDeclarationSyntax record)
            return null;

        var fields = new List<SerializedField>();

        foreach (var parameterSyntax in record.ParameterList?.Parameters ?? default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.SemanticModel.GetDeclaredSymbol(parameterSyntax, cancellationToken) is IParameterSymbol parameter)
                fields.Add(ReadField(parameter));
        }

        return new SerializableType(
            kind,
            type.ContainingNamespace.IsGlobalNamespace ? FallbackNamespace : type.ContainingNamespace.ToDisplayString(),
            type.Name,
            type.ToDisplayString(TypeFormat),
            type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal",
            new EquatableArray<SerializedField>([.. fields]));
    }

    private static SerializedField ReadField(IParameterSymbol parameter)
    {
        var attributes = parameter.GetAttributes();
        var type = WithoutNullability(parameter.Type);

        var name = parameter.Name;
        var variable = ToVariableName(name);
        var valueType = type.ToDisplayString(TypeFormat);
        var isConditional = Find(attributes, "ConditionalAttribute") is not null;

        if (Find(attributes, "BitSetAttribute") is { } bitSet)
            return new SerializedField(name, variable, FieldShape.BitSet, valueType, "bool", null, null, "", "", IntArgument(bitSet), isConditional);

        var fixedLength = Find(attributes, "LengthAttribute") is { } length ? IntArgument(length) : null;

        var shape = FieldShape.Single;
        var element = type;

        if (type is IArrayTypeSymbol array)
        {
            element = WithoutNullability(array.ElementType);
            shape = element.SpecialType == SpecialType.System_Byte ? FieldShape.Bytes : FieldShape.Array;
        }

        var elementType = element.ToDisplayString(TypeFormat);

        if (shape == FieldShape.Bytes)
            return new SerializedField(name, variable, shape, valueType, elementType, null, null, "", "", fixedLength, isConditional);

        var isEnum = element.TypeKind == TypeKind.Enum;

        var method = OverwrittenMethod(attributes)
            ?? (Find(attributes, "BitFieldAttribute") is not null ? "Byte" : null)
            ?? (isEnum ? "VarInt" : null)
            ?? (PrimitiveMethods.TryGetValue(elementType, out var primitive) ? primitive : null);

        // Anything the protocol has no encoding for is taken to be a model with a
        // generated serializer of its own.
        var serializer = method is null ? SerializerOf(element) : null;

        // An enum travels as the number behind it.
        var castsEnum = isEnum && method is not null && MethodValueTypes.ContainsKey(method);
        var writeCast = castsEnum ? $"({MethodValueTypes[method!]})" : "";
        var readCast = castsEnum ? $"({elementType})" : "";

        return new SerializedField(name, variable, shape, valueType, elementType, method, serializer, writeCast, readCast, fixedLength, isConditional);
    }

    private static ITypeSymbol WithoutNullability(ITypeSymbol type)
        => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

    private static AttributeData? Find(IEnumerable<AttributeData> attributes, string name)
        => attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == $"{AbstractionNamespace}.{name}");

    /// <summary>
    /// Reads an <c>int</c> passed to an attribute. Constructors taking an enum
    /// instead, like <c>Length(LengthType)</c>, yield <c>null</c>.
    /// </summary>
    private static int? IntArgument(AttributeData attribute)
        => attribute.ConstructorArguments.FirstOrDefault() is { Kind: TypedConstantKind.Primitive, Value: int value } ? value : null;

    /// <summary>
    /// The method named by <c>[OverwriteType(OverwriteType.X)]</c>, which is the name of the enum member.
    /// </summary>
    private static string? OverwrittenMethod(IEnumerable<AttributeData> attributes)
    {
        if (Find(attributes, "OverwriteTypeAttribute")?.ConstructorArguments.FirstOrDefault() is not { Kind: TypedConstantKind.Enum } argument)
            return null;

        return argument.Type?.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, argument.Value))
            ?.Name;
    }

    private static string SerializerOf(ITypeSymbol model)
        => model.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? $"global::{ns.ToDisplayString()}.{model.Name}Serializer"
            : $"global::{FallbackNamespace}.{model.Name}Serializer";

    private static string ToVariableName(string name)
    {
        var variable = char.ToLowerInvariant(name[0]) + name.Substring(1);

        if (SyntaxFacts.GetKeywordKind(variable) != SyntaxKind.None)
            return "@" + variable;

        return ReservedNames.Contains(variable) ? "_" + variable : variable;
    }
}
