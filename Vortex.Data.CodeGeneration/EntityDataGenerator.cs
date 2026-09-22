using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Vortex.CodeGeneration;

namespace Vortex.Data.CodeGeneration;

/// <summary>
/// Generates <c>EntityDataKey</c> and <c>EntityDataKeys</c>: what each index of
/// an entity's metadata means.
/// </summary>
/// <remarks>
/// That is decided by the entity classes in the game's code and is not in any
/// report Mojang's data generator writes, so the names come from PrismarineJS's
/// <c>entities.json</c>. The entity types themselves still come from Mojang's
/// registry, which the table is laid out by.
/// </remarks>
[Generator]
public sealed class EntityDataGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MissingEntity = new(
        "VXD005",
        "Entity without metadata names",
        "PrismarineJS has no metadata names for '{0}', so none of its metadata can be read by name",
        "Vortex.Data",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var input = DataFiles.File(context, "prismarine/entities.json")
            .Combine(DataFiles.File(context, "registries.json").Collect());

        context.RegisterSourceOutput(input, static (output, files) =>
        {
            if (files.Right.IsEmpty)
                return;

            var registry = SimpleJson.Parse(files.Right[0].Text).GetObject("minecraft:entity_type")?.GetObject("entries");
            if (registry is null)
                return;

            var keysByEntity = SimpleJson.ParseArray(files.Left.Text)
                .OfType<JsonObject>()
                .Where(entity => entity.GetString("name") is not null)
                .ToDictionary(
                    entity => "minecraft:" + entity.GetString("name"),
                    entity => (entity.GetArray("metadataKeys") ?? new List<object?>()).Cast<string>().ToList());

            var types = registry
                .OrderBy(entry => (entry.Value as JsonObject)?.GetInt("protocol_id"))
                .Select(entry =>
                {
                    if (!keysByEntity.TryGetValue(entry.Key, out var keys))
                    {
                        output.ReportDiagnostic(Diagnostic.Create(MissingEntity, Location.None, entry.Key));
                        keys = [];
                    }

                    return (Type: Naming.ToIdentifier(entry.Key), Keys: keys);
                })
                .ToList();

            var allKeys = new SortedSet<string>(types.SelectMany(type => type.Keys), StringComparer.Ordinal);

            var code = Template("File")
                .Set("keys", string.Join("\n", allKeys.Select(key => Template("Key")
                    .Set("identifier", key)
                    .Set("name", Naming.ToIdentifier(key))
                    .Render())))
                .Set("types", string.Join("\n", types.Select(type => Template("Type")
                    .Set("type", type.Type)
                    .Set("keys", string.Join(", ", type.Keys.Select(key => Template("TypeKey").Set("name", Naming.ToIdentifier(key)).Render())))
                    .Render())))
                .Render();

            output.AddSource("EntityDataKeys.g.cs", CodeFormatter.Format(code));
        });
    }

    private static CodeTemplate Template(string section)
        => CodeTemplate.Get("EntityData", section);
}
