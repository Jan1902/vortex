using System.Linq;
using Microsoft.CodeAnalysis;
using Vortex.CodeGeneration;

namespace Vortex.Data.CodeGeneration;

/// <summary>
/// Generates an enum for every registry in Mojang's registries report:
/// <c>Block</c>, <c>Item</c>, <c>EntityType</c>, <c>SoundEvent</c> and so on.
/// </summary>
/// <remarks>
/// The values are the protocol IDs, so a value can be sent and received as it is
/// and the name is what the code refers to instead of a string.
/// </remarks>
[Generator]
public sealed class RegistryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => context.RegisterSourceOutput(DataFiles.File(context, "registries.json"), static (output, file) =>
        {
            foreach (var registry in SimpleJson.Parse(file.Text))
            {
                if ((registry.Value as JsonObject)?.GetObject("entries") is not { } entries)
                    continue;

                var name = Naming.ToIdentifier(registry.Key);

                output.AddSource($"Registries/{name}.g.cs", Render(registry.Key, name, entries));
            }
        });

    private static string Render(string registry, string name, JsonObject entries)
    {
        var values = entries
            .Select(entry => (Identifier: entry.Key, Id: (entry.Value as JsonObject)?.GetInt("protocol_id")))
            .Where(entry => entry.Id is not null)
            .OrderBy(entry => entry.Id)
            .Select(entry => CodeTemplate.Get("Registry", "Entry")
                .Set("identifier", entry.Identifier)
                .Set("name", Naming.ToIdentifier(entry.Identifier))
                .Set("id", entry.Id!.Value.ToString())
                .Render());

        return CodeFormatter.Format(CodeTemplate.Get("Registry", "Enum")
            .Set("registry", registry)
            .Set("name", name)
            .Set("entries", string.Join("\n", values))
            .Render());
    }
}
