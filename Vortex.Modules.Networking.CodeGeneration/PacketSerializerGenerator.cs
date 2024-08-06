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
            var parameterName = parameter.Identifier.Text;
            var parameterType = parameter.Type?.ToString();

            if (parameterType is null)
                continue;

            var isArray = false;
            if (parameterType.EndsWith("[]"))
            {
                isArray = true;
                parameterType = parameterType.Substring(0, parameterType.Length - 2);
            }

            var writerMethod = MapTypeToReaderWriterMethod(parameterType);
            if (isArray)
            {
                builder.AppendLine($"for (int i = 0; i < subject.{parameterName}.Length; i++)");
                builder.AppendLine("{");

                if (writerMethod is null)
                    builder.AppendLine($"new {parameterType}Serializer().SerializeModel(subject.{parameterName}[i], writer);");
                else
                    builder.AppendLine($"writer.Write{writerMethod}(subject.{parameterName}[i]);");

                builder.AppendLine("}");

                continue;
            }

            if (writerMethod is null)
            {
                builder.AppendLine($"new {parameterType}Serializer().SerializeModel(subject.{parameterName}, writer);");

                continue;
            }

            builder.AppendLine($"writer.Write{writerMethod}(subject.{parameterName});");
        }

        return builder.ToString();
    }

    private string BuildDeserializeMethod(RecordDeclarationSyntax packet)
    {
        var builder = new StringBuilder();

        var parameters = new List<string>();

        foreach (var parameter in packet.ParameterList?.Parameters ?? [])
        {
            var parameterName = parameter.Identifier.Text;
            parameterName = parameterName.Substring(0, 1).ToLower() + parameterName.Substring(1, parameterName.Length - 1);

            var parameterType = parameter.Type?.ToString();

            if (parameterName is null || parameterType is null)
                continue;

            parameters.Add(parameterName);

            var isArray = false;
            if (parameterType.EndsWith("[]"))
            {
                isArray = true;
                parameterType = parameterType.Substring(0, parameterType.Length - 2);
            }

            var readerMethod = MapTypeToReaderWriterMethod(parameterType);
            if (isArray)
            {
                builder.AppendLine($"var {parameterName}Length = reader.ReadVarInt();");
                builder.AppendLine($"var {parameterName} = new {parameterType}[{parameterName}Length];");

                builder.AppendLine($"for (int i = 0; i < {parameterName}Length; i++)");
                builder.AppendLine("{");

                if (readerMethod is null)
                    builder.AppendLine($"{parameterName}[i] = new {parameterType}Serializer().DeserializeModel(reader);");
                else
                    builder.AppendLine($"{parameterName}[i] = reader.Read{readerMethod}();");

                builder.AppendLine("}");

                continue;
            }

            if (readerMethod is null)
            {
                builder.AppendLine($"var {parameterName} = new {parameterType}Serializer().DeserializeModel(reader);");

                continue;
            }

            builder.AppendLine($"var {parameterName} = reader.Read{readerMethod}();");
        }

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
            _ => null
        };
    }
}
