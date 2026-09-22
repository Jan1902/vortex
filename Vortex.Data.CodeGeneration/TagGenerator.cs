using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Vortex.CodeGeneration;

namespace Vortex.Data.CodeGeneration;

/// <summary>
/// Generates <c>BlockTags</c>, <c>ItemTags</c> and <c>EntityTypeTags</c> from the
/// tag files of Mojang's data generator.
/// </summary>
/// <remarks>
/// A tag may list other tags (<c>#minecraft:logs</c>); those are resolved here, so
/// every generated set holds its actual members and can be asked directly.
/// </remarks>
[Generator]
public sealed class TagGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor UnknownTag = new(
        "VXD001",
        "Unknown tag",
        "The tag '{0}' refers to '{1}', which does not exist",
        "Vortex.Data",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        Register(context, "tags/block", "block", "BlockTags", "Block");
        Register(context, "tags/item", "item", "ItemTags", "Item");
        Register(context, "tags/entity_type", "entity type", "EntityTypeTags", "EntityType");
    }

    private static void Register(IncrementalGeneratorInitializationContext context, string folder, string kind, string className, string type)
        => context.RegisterSourceOutput(DataFiles.Folder(context, folder), (output, files) =>
        {
            if (files.Count == 0)
                return;

            var tags = files.ToDictionary(file => "minecraft:" + file.Name, file => ReadValues(file.Text));
            var resolved = new Dictionary<string, SortedSet<string>>();

            var rendered = tags.Keys.OrderBy(tag => tag, StringComparer.Ordinal).Select(tag =>
            {
                var values = Resolve(tag, tags, resolved, output);

                return CodeTemplate.Get("Tags", "Tag")
                    .Set("identifier", tag)
                    .Set("name", Naming.ToIdentifier(tag))
                    .Set("type", type)
                    .Set("values", string.Join(", ", values.Select(value => CodeTemplate.Get("Tags", "Value")
                        .Set("type", type)
                        .Set("name", Naming.ToIdentifier(value))
                        .Render())))
                    .Render();
            });

            var code = CodeTemplate.Get("Tags", "File")
                .Set("kind", kind)
                .Set("className", className)
                .Set("tags", string.Join("\n", rendered))
                .Render();

            output.AddSource($"{className}.g.cs", CodeFormatter.Format(code));
        });

    /// <summary>
    /// The entries of a tag file. An entry is either a name or, for an optional
    /// one, an object carrying it as <c>id</c>.
    /// </summary>
    private static List<string> ReadValues(string text)
        => (SimpleJson.Parse(text).GetArray("values") ?? new List<object?>())
            .Select(value => value as string ?? (value as JsonObject)?.GetString("id"))
            .Where(value => value is not null)
            .Select(value => value!)
            .ToList();

    private static SortedSet<string> Resolve(string tag, Dictionary<string, List<string>> tags, Dictionary<string, SortedSet<string>> resolved, SourceProductionContext output)
    {
        if (resolved.TryGetValue(tag, out var done))
            return done;

        // Registered before resolving, so a tag that includes itself ends instead of recursing forever.
        var values = new SortedSet<string>(StringComparer.Ordinal);
        resolved[tag] = values;

        foreach (var value in tags[tag])
        {
            if (!value.StartsWith("#", StringComparison.Ordinal))
            {
                values.Add(value);
                continue;
            }

            var included = value.Substring(1);
            if (!tags.ContainsKey(included))
            {
                output.ReportDiagnostic(Diagnostic.Create(UnknownTag, Location.None, tag, value));
                continue;
            }

            values.UnionWith(Resolve(included, tags, resolved, output));
        }

        return values;
    }
}
