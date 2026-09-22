using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Vortex.CodeGeneration;

namespace Vortex.Data.CodeGeneration;

/// <summary>
/// Generates the block state data from Mojang's blocks report: which block each
/// state ID belongs to and what its properties are.
/// </summary>
/// <remarks>
/// <para>
/// The game numbers states block by block, in the order of the block registry,
/// and within a block walks every combination of its properties sorted by name,
/// the last one changing fastest. Knowing that, a block's first state ID and its
/// property values are enough to decode any of its states, so the table holds
/// that instead of all 26 000 states.
/// </para>
/// <para>
/// That layout is checked against every state in the report. Should a future
/// version number them differently, the build fails here instead of the bot
/// misreading the world.
/// </para>
/// </remarks>
[Generator]
public sealed class BlockGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor UnexpectedLayout = new(
        "VXD002",
        "Unexpected block state layout",
        "The states of '{0}' are not numbered the way BlockState decodes them: {1}",
        "Vortex.Data",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var input = DataFiles.File(context, "blocks.json")
            .Combine(DataFiles.File(context, "registries.json").Collect());

        context.RegisterSourceOutput(input, static (output, files) =>
        {
            if (files.Right.IsEmpty)
                return;

            var registry = SimpleJson.Parse(files.Right[0].Text).GetObject("minecraft:block")?.GetObject("entries");
            if (registry is null)
                return;

            var blocks = SimpleJson.Parse(files.Left.Text)
                .Select(block => ReadBlock(block.Key, (JsonObject)block.Value!, registry))
                .OrderBy(block => block.Id)
                .ToList();

            var properties = ClassifyProperties(blocks);

            foreach (var block in blocks)
                if (Validate(block, properties) is { } problem)
                    output.ReportDiagnostic(Diagnostic.Create(UnexpectedLayout, Location.None, block.Name, problem));

            output.AddSource("BlockProperties.g.cs", CodeFormatter.Format(RenderProperties(properties)));
            output.AddSource("BlockStateTable.g.cs", CodeFormatter.Format(RenderTable(blocks, properties)));
        });
    }

    private static BlockData ReadBlock(string name, JsonObject block, JsonObject registry)
    {
        var properties = (block.GetObject("properties") ?? new JsonObject())
            .Select(property => new PropertyData(property.Key, ((List<object?>)property.Value!).Cast<string>().ToList()))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToList();

        var states = (block.GetArray("states") ?? new List<object?>())
            .Cast<JsonObject>()
            .Select(state => new StateData(
                state.GetInt("id")!.Value,
                (state.GetObject("properties") ?? new JsonObject()).ToDictionary(p => p.Key, p => (string)p.Value!),
                state.GetBool("default") ?? false))
            .OrderBy(state => state.Id)
            .ToList();

        var id = (registry.GetObject(name) ?? throw new InvalidOperationException($"The block '{name}' is missing from the registry.")).GetInt("protocol_id")!.Value;

        return new BlockData(name, id, properties, states);
    }

    /// <summary>
    /// Works out for every property name whether it holds a bool, a number or
    /// one of a set of names. The same name can have different values on
    /// different blocks, so the set of names is the union of all of them.
    /// </summary>
    private static SortedDictionary<string, PropertyKind> ClassifyProperties(IEnumerable<BlockData> blocks)
    {
        var values = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var property in blocks.SelectMany(block => block.Properties))
        {
            if (!values.TryGetValue(property.Name, out var set))
                values[property.Name] = set = new SortedSet<string>(StringComparer.Ordinal);

            set.UnionWith(property.Values);
        }

        var kinds = new SortedDictionary<string, PropertyKind>(StringComparer.Ordinal);

        foreach (var property in values)
        {
            if (property.Value.All(value => value is "true" or "false"))
                kinds[property.Key] = new PropertyKind(PropertyType.Bool, []);
            else if (property.Value.All(value => value.All(char.IsDigit)))
                kinds[property.Key] = new PropertyKind(PropertyType.Int, []);
            else
                kinds[property.Key] = new PropertyKind(PropertyType.Enum, [.. property.Value]);
        }

        return kinds;
    }

    /// <summary>
    /// Checks that decoding each state of a block the way <c>BlockState</c> does
    /// yields exactly the properties the report lists for it.
    /// </summary>
    private static string? Validate(BlockData block, SortedDictionary<string, PropertyKind> kinds)
    {
        var expectedCount = block.Properties.Aggregate(1, (count, property) => count * property.Values.Count);
        if (block.States.Count != expectedCount)
            return $"expected {expectedCount} states, found {block.States.Count}";

        if (block.States.Count(state => state.IsDefault) != 1)
            return "expected exactly one default state";

        var first = block.States[0].Id;

        for (var index = 0; index < block.States.Count; index++)
        {
            var state = block.States[index];

            if (state.Id != first + index)
                return $"state {state.Id} is not consecutive";

            var offset = index;
            for (var p = block.Properties.Count - 1; p >= 0; p--)
            {
                var property = block.Properties[p];
                var value = property.Values[offset % property.Values.Count];
                offset /= property.Values.Count;

                if (!state.Properties.TryGetValue(property.Name, out var actual) || actual != value)
                    return $"state {state.Id} has {property.Name}={actual}, decoding gives {value}";
            }
        }

        return null;
    }

    private static string RenderProperties(SortedDictionary<string, PropertyKind> kinds)
    {
        var names = kinds.Keys.Select(name => CodeTemplate.Get("Blocks", "Name")
            .Set("identifier", name)
            .Set("name", Naming.ToIdentifier(name))
            .Render());

        var descriptors = kinds.Select(property => CodeTemplate.Get("Blocks", property.Value.Type + "Descriptor")
            .Set("identifier", property.Key)
            .Set("name", Naming.ToIdentifier(property.Key))
            .Render());

        var valueEnums = kinds.Where(property => property.Value.Type == PropertyType.Enum).Select(property => CodeTemplate.Get("Blocks", "ValueEnum")
            .Set("identifier", property.Key)
            .Set("name", Naming.ToIdentifier(property.Key))
            .Set("values", string.Join("\n", property.Value.EnumValues.Select((value, index) => CodeTemplate.Get("Blocks", "Value")
                .Set("identifier", value)
                .Set("name", Naming.ToIdentifier(value))
                .Set("index", index.ToString())
                .Render())))
            .Render());

        return CodeTemplate.Get("Blocks", "Properties")
            .Set("names", string.Join("\n", names))
            .Set("descriptors", string.Join("\n", descriptors))
            .Set("valueEnums", string.Join("\n", valueEnums))
            .Render();
    }

    private static string RenderTable(List<BlockData> blocks, SortedDictionary<string, PropertyKind> kinds)
    {
        var definitions = blocks.Select(block => CodeTemplate.Get("Blocks", "BlockDefinitions")
            .Set("block", Naming.ToIdentifier(block.Name))
            .Set("definitions", string.Join(", ", block.Properties.Select(property => CodeTemplate.Get("Blocks", "Definition")
                .Set("property", Naming.ToIdentifier(property.Name))
                .Set("values", string.Join(", ", property.Values.Select(value => kinds[property.Name].Encode(value))))
                .Render())))
            .Render());

        return CodeTemplate.Get("Blocks", "Table")
            .Set("stateCount", blocks.Sum(block => block.States.Count).ToString())
            .Set("firstStateIds", string.Join(", ", blocks.Select(block => block.States[0].Id)))
            .Set("defaultStateIds", string.Join(", ", blocks.Select(block => block.States.First(state => state.IsDefault).Id)))
            .Set("properties", string.Join("\n", definitions))
            .Render();
    }

    private sealed record BlockData(string Name, int Id, List<PropertyData> Properties, List<StateData> States);

    private sealed record PropertyData(string Name, List<string> Values);

    private sealed record StateData(int Id, Dictionary<string, string> Properties, bool IsDefault);

    private enum PropertyType
    {
        Bool,
        Int,
        Enum
    }

    /// <param name="EnumValues">The names an enum property can take, in the order of the generated enum.</param>
    private sealed record PropertyKind(PropertyType Type, List<string> EnumValues)
    {
        /// <summary>
        /// The number a value is stored as: 1 or 0 for a bool, the number itself,
        /// or the value's position in the generated enum.
        /// </summary>
        public string Encode(string value)
            => Type switch
            {
                PropertyType.Bool => value == "true" ? "1" : "0",
                PropertyType.Int => value,
                _ => EnumValues.IndexOf(value).ToString(),
            };
    }
}
