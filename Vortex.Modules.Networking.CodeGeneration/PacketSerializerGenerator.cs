using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Vortex.Modules.Networking.CodeGeneration.Serialization;

namespace Vortex.Modules.Networking.CodeGeneration;

/// <summary>
/// Generates the serializer of every record marked <c>[AutoSerializedPacket]</c>
/// or <c>[PacketModel]</c>.
/// </summary>
/// <remarks>
/// The record's positional parameters are the packet's fields in wire order.
/// <see cref="SerializableTypeReader"/> decides how each is encoded,
/// <see cref="SerializerRenderer"/> turns that into code.
/// </remarks>
[Generator]
public sealed class PacketSerializerGenerator : IIncrementalGenerator
{
    private const string PacketAttribute = SerializableTypeReader.AbstractionNamespace + ".AutoSerializedPacketAttribute";
    private const string ModelAttribute = SerializableTypeReader.AbstractionNamespace + ".PacketModelAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        Register(context, PacketAttribute, SerializerKind.Packet);
        Register(context, ModelAttribute, SerializerKind.Model);
    }

    private static void Register(IncrementalGeneratorInitializationContext context, string attribute, SerializerKind kind)
    {
        var types = context.SyntaxProvider.ForAttributeWithMetadataName(
            attribute,
            static (node, _) => node is RecordDeclarationSyntax,
            (syntaxContext, cancellationToken) => SerializableTypeReader.Read(syntaxContext, kind, cancellationToken));

        context.RegisterSourceOutput(types, static (output, type) =>
        {
            if (type is not null)
                output.AddSource(type.HintName, SerializerRenderer.Render(type));
        });
    }
}
