using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vortex.Modules.Networking.CodeGeneration;

public class SyntaxReceiver : ISyntaxReceiver
{
    private const string AutoSerializedPacketAttributeName = "AutoSerializedPacket";
    private const string PacketModelAttributeName = "PacketModel";

    public List<RecordDeclarationSyntax> PacketDeclerations { get; } = [];
    public List<RecordDeclarationSyntax> ModelDeclerations { get; } = [];

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is RecordDeclarationSyntax packetDecleration && packetDecleration.AttributeLists.Any(l => l.Attributes.Any(a => a.Name.ToString() == AutoSerializedPacketAttributeName)))
            PacketDeclerations.Add(packetDecleration);

        if (syntaxNode is RecordDeclarationSyntax modelDecleration && modelDecleration.AttributeLists.Any(l => l.Attributes.Any(a => a.Name.ToString() == PacketModelAttributeName)))
            ModelDeclerations.Add(modelDecleration);
    }
}

[Generator]
public class PacketSerializerGenerator : ISourceGenerator
{
    private const string PacketSerializerTemplateName = "PacketSerializerTemplate";
    private const string ModelSerializerTemplateName = "ModelSerializerTemplate";
    private const string ConditionalAttributeName = "Conditional";
    private const string BitFieldAttributeName = "BitField";
    private const string BitSetAttributeName = "BitSet";
    private const string OverwriteTypeAttributeName = "OverwriteType";

    public void Initialize(GeneratorInitializationContext context)
        => context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not SyntaxReceiver receiver)
            return;

        foreach (var packet in receiver.PacketDeclerations)
            context.AddSource($"{packet.Identifier.Text}_Serializer.g.cs", BuildGeneratorClass(packet, PacketSerializerTemplateName));

        foreach (var model in receiver.ModelDeclerations)
            context.AddSource($"{model.Identifier.Text}_Serializer.g.cs", BuildGeneratorClass(model, ModelSerializerTemplateName));
    }

    private string BuildGeneratorClass(RecordDeclarationSyntax packet, string templateName)
    {
        var typeName = packet.Identifier.Text;
        var typeNamespace = packet.FirstAncestorOrSelf<CompilationUnitSyntax>()?.Members.OfType<FileScopedNamespaceDeclarationSyntax>()?.FirstOrDefault().Name.ToString() ?? "Vortex.Generated";

        var template = new CodeTemplate(templateName);

        template.Set("type", typeName);
        template.Set("namespace", typeNamespace);
        template.Set("serializeContent", BuildSerializeMethod(packet));
        template.Set("deserializeContent", BuildDeserializeMethod(packet));

        return template.Render();
    }

    private string BuildSerializeMethod(RecordDeclarationSyntax packet)
    {
        var builder = new StringBuilder();

        foreach (var parameter in packet.ParameterList?.Parameters ?? [])
        {
            // Parameter name and type
            var parameterName = parameter.Identifier.Text;
            var parameterType = parameter.Type?.ToString();
            parameterType = parameterType?.Replace("?", "");

            if (parameterType is null)
                continue;

            // Arrays
            var isArray = false;
            if (parameterType.EndsWith("[]"))
            {
                isArray = true;
                parameterType = parameterType.Substring(0, parameterType.Length - 2);
            }

            // Conditional values with previous bool
            var conditional = false;
            if (parameter.AttributeLists.Any(l => l.Attributes.Any(a => a.Name.ToString() == ConditionalAttributeName)))
            {
                conditional = true;

                builder.AppendLine($"writer.WriteBool(subject.{parameterName} != default);");
                builder.AppendLine($"if (subject.{parameterName} != default)");
                builder.AppendLine("{");
            }

            // Binary writer method
            var writerMethod = MapTypeToReaderWriterMethod(parameterType);

            // Type overwrite
            var overwriteTypeAttribute = parameter.AttributeLists.SelectMany(l => l.Attributes).FirstOrDefault(a => a.Name.ToString() == OverwriteTypeAttributeName);
            if (overwriteTypeAttribute is not null)
            {
                var memberAccess = (MemberAccessExpressionSyntax?)overwriteTypeAttribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression;

                if (memberAccess is not null)
                    writerMethod = memberAccess.Name.Identifier.ValueText;
            }

            // Bit sets
            var bitSetAttribute = parameter.AttributeLists.SelectMany(l => l.Attributes).FirstOrDefault(a => a.Name.ToString() == BitSetAttributeName);
            if (bitSetAttribute is not null)
            {
                isArray = false;
                writerMethod = "BitSet";
            }

            // Bit fields
            var isBitField = false;
            if (parameter.AttributeLists.Any(l => l.Attributes.Any(a => a.Name.ToString() == BitFieldAttributeName)))
            {
                writerMethod = "Byte";
                isBitField = true;
            }

            // Byte arrays
            if (writerMethod == "Byte" && isArray)
            {
                writerMethod = "Bytes";
                isArray = false;
                parameterType = "byte[]";

                // Length prefix
                builder.AppendLine($"writer.WriteVarInt(subject.{parameterName}.Length);");
            }

            // Array
            if (isArray)
            {
                // Length prefix
                builder.AppendLine($"writer.WriteVarInt(subject.{parameterName}.Length);");

                // For loop
                builder.AppendLine($"for (int i = 0; i < subject.{parameterName}.Length; i++)");
                builder.AppendLine("{");

                // Custom model
                if (writerMethod is null)
                    builder.AppendLine($"new {parameterType}Serializer().SerializeModel(subject.{parameterName}[i], writer);");
                else
                    builder.AppendLine($"writer.Write{writerMethod}({(isBitField ? "(byte)" : "")}subject.{parameterName}[i]);");

                // Closing loop bracket
                builder.AppendLine("}");

                // Closing if bracket
                if (conditional)
                    builder.AppendLine("}");

                continue;
            }

            // Custom model
            if (writerMethod is null)
            {
                builder.AppendLine($"new {parameterType}Serializer().SerializeModel(subject.{parameterName}, writer);");

                // Closing if bracket
                if (conditional)
                    builder.AppendLine("}");

                continue;
            }

            // Default case
            builder.AppendLine($"writer.Write{writerMethod}({(isBitField ? "(byte)" : "")}subject.{parameterName});");

            // Closing if bracket
            if (conditional)
                builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private string BuildDeserializeMethod(RecordDeclarationSyntax packet)
    {
        var builder = new StringBuilder();

        var parameters = new List<string>();

        foreach (var parameter in packet.ParameterList?.Parameters ?? [])
        {
            // Parameter name
            var parameterName = parameter.Identifier.Text;
            parameterName = parameterName.Substring(0, 1).ToLower() + parameterName.Substring(1, parameterName.Length - 1);

            // Parameter type
            var parameterType = parameter.Type?.ToString();
            parameterType = parameterType?.Replace("?", "");

            if (parameterName is null || parameterType is null)
                continue;

            // List of parameters for constructor
            parameters.Add(parameterName);

            // Arrays
            var isArray = false;
            if (parameterType.EndsWith("[]"))
            {
                isArray = true;
                parameterType = parameterType.Substring(0, parameterType.Length - 2);
            }

            // Conditional values with previous bool
            var conditional = false;
            if (parameter.AttributeLists.Any(l => l.Attributes.Any(a => a.Name.ToString() == ConditionalAttributeName)))
                conditional = true;

            // Binary reader method
            var readerMethod = MapTypeToReaderWriterMethod(parameterType);
            var readerMethodParameters = new List<string>();

            // Type overwrite
            var overwriteTypeAttribute = parameter.AttributeLists.SelectMany(l => l.Attributes).FirstOrDefault(a => a.Name.ToString() == OverwriteTypeAttributeName);
            if (overwriteTypeAttribute is not null)
            {
                var memberAccess = (MemberAccessExpressionSyntax?)overwriteTypeAttribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression;

                if (memberAccess is not null)
                    readerMethod = memberAccess.Name.Identifier.ValueText;
            }

            // Bit sets
            var bitSetAttribute = parameter.AttributeLists.SelectMany(l => l.Attributes).FirstOrDefault(a => a.Name.ToString() == BitSetAttributeName);
            if (bitSetAttribute is not null)
            {
                isArray = false;
                readerMethod = "BitSet";
                readerMethodParameters.Add(bitSetAttribute.ArgumentList?.Arguments.FirstOrDefault()?.ToString() ?? "0");
            }

            // Bit fields
            var isBitField = false;
            if (parameter.AttributeLists.Any(l => l.Attributes.Any(a => a.Name.ToString() == BitFieldAttributeName)))
            {
                readerMethod = "Byte";
                isBitField = true;
            }

            // Byte arrays
            if (readerMethod == "Byte" && isArray)
            {
                readerMethod = "Bytes";
                isArray = false;
                parameterType = "byte[]";

                // Read array length
                builder.AppendLine($"var {parameterName}Length = reader.ReadVarInt();");
                readerMethodParameters.Add($"{parameterName}Length");
            }

            // Array
            if (isArray)
            {
                if (conditional)
                {
                    // Declare array
                    builder.AppendLine($"{parameterType}[]? {parameterName} = null;");

                    // Check if array exists
                    builder.AppendLine($"var {parameterName}Exists = reader.ReadBool();");

                    // Open if statement
                    builder.AppendLine($"if ({parameterName}Exists)");
                    builder.AppendLine("{");
                }

                // Read array length
                builder.AppendLine($"var {parameterName}Length = reader.ReadVarInt();");

                // Initialize array
                builder.AppendLine($"{(conditional ? "" : "var ")}{parameterName} = new {parameterType}[{parameterName}Length];");

                // Open loop
                builder.AppendLine($"for (int i = 0; i < {parameterName}Length; i++)");
                builder.AppendLine("{");

                if (readerMethod is null)
                    builder.AppendLine($"{parameterName}[i] = new {parameterType}Serializer().DeserializeModel(reader);");
                else
                    builder.AppendLine($"{parameterName}[i] = {(isBitField ? $"({parameterType})" : "")}reader.Read{readerMethod}({string.Join(", ", readerMethodParameters)});");

                // Close loop
                builder.AppendLine("}");

                // Close if statement
                if (conditional)
                    builder.AppendLine("}");

                continue;
            }

            // Conditional values with previous bool
            if (conditional)
            {
                // Declare variable
                builder.AppendLine($"{parameterType}? {parameterName} = null;");

                // Check if variable exists
                builder.AppendLine($"var {parameterName}Exists = reader.ReadBool();");

                // Open if statement
                builder.AppendLine($"if ({parameterName}Exists)");
                builder.AppendLine("{");
            }

            // Custom model
            if (readerMethod is null)
            {
                builder.AppendLine($"{(conditional ? "" : "var ")}{parameterName} = new {parameterType}Serializer().DeserializeModel(reader);");

                // Close if statement
                if (conditional)
                    builder.AppendLine("}");

                continue;
            }

            // Default case
            builder.AppendLine($"{(conditional ? "" : "var ")}{parameterName} = {(isBitField ? $"({parameterType})" : "")}reader.Read{readerMethod}({string.Join(", ", readerMethodParameters)});");

            // Close if statement
            if (conditional)
                builder.AppendLine("}");
        }

        // Return packet with constructor parameters
        builder.AppendLine($"return new {packet.Identifier.Text}({string.Join(", ", parameters)});");

        return builder.ToString();
    }

    private string? MapTypeToReaderWriterMethod(string type)
    {
        return type switch
        {
            "byte" => "Byte",
            "short" => "Short",
            "ushort" => "UShort",
            "int" => "VarInt",
            "uint" => "UInt",
            "long" => "Long",
            "ulong" => "ULong",
            "float" => "Float",
            "double" => "Double",
            "bool" => "Bool",
            "string" => "StringWithVarIntPrefix",
            "Guid" => "UUID",
            "NbtTag" => "NbtTag",
            _ => null
        };
    }
}
