using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Vortex.CodeGeneration;

namespace Vortex.Modules.Networking.CodeGeneration;

/// <summary>
/// Generates the packet IDs of the protocol from Mojang's own packet report,
/// so they never have to be looked up and typed by hand.
/// </summary>
/// <remarks>
/// The report is produced by the vanilla server's data generator, see
/// scripts/update-protocol-data.sh, and handed to the project that owns the IDs
/// as <c>Resources/packets.json</c>.
/// </remarks>
[Generator]
public sealed class PacketIdGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => context.RegisterSourceOutput(DataFiles.File(context, "packets.json"), static (output, report) =>
            output.AddSource("PacketIds.g.cs", Render(SimpleJson.Parse(report.Text))));

    private static string Render(JsonObject report)
    {
        var states = report
            .OrderBy(state => state.Key, StringComparer.Ordinal)
            .Where(state => state.Value is JsonObject)
            .Select(state => Group(Naming.ToIdentifier(state.Key), ((JsonObject)state.Value!)
                .OrderBy(direction => direction.Key, StringComparer.Ordinal)
                .Where(direction => direction.Value is JsonObject)
                .Select(direction => Group(ToDirectionName(direction.Key), ((JsonObject)direction.Value!)
                    .OrderBy(packet => packet.Key, StringComparer.Ordinal)
                    .Where(packet => (packet.Value as JsonObject)?.GetInt("protocol_id") is not null)
                    .Select(packet => CodeTemplate.Get("PacketIds", "Packet")
                        .Set("identifier", packet.Key)
                        .Set("name", Naming.ToIdentifier(packet.Key))
                        .Set("id", ((JsonObject)packet.Value!).GetInt("protocol_id")!.Value.ToString("X2"))
                        .Render())))));

        var code = CodeTemplate.Get("PacketIds", "File")
            .Set("states", string.Join("\n", states))
            .Render();

        return CodeFormatter.Format(code);
    }

    private static string Group(string name, IEnumerable<string> members)
        => CodeTemplate.Get("PacketIds", "Group")
            .Set("name", name)
            .Set("members", string.Join("\n", members))
            .Render();

    /// <summary>
    /// Matches the spelling of <c>PacketDirection</c> instead of the report's lowercase form.
    /// </summary>
    private static string ToDirectionName(string direction)
        => direction switch
        {
            "clientbound" => "ClientBound",
            "serverbound" => "ServerBound",
            _ => Naming.ToIdentifier(direction)
        };
}
