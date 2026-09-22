using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Vortex.CodeGeneration;

namespace Vortex.Data.CodeGeneration;

/// <summary>
/// Generates <c>LootTables</c> from the block loot tables of Mojang's data
/// generator: what each block drops, and under which conditions.
/// </summary>
/// <remarks>
/// Every kind of entry, condition and function becomes its own typed record, and
/// conditions on block state properties become typed comparisons such as
/// <c>state.Get(BlockProperties.Age) == 7</c>. Anything the generator does not
/// know is a build error rather than a table that quietly drops less.
/// </remarks>
[Generator]
public sealed class LootTableGenerator : IIncrementalGenerator
{
    private const string Folder = "loot_table/blocks";

    private static readonly DiagnosticDescriptor UnsupportedLoot = new(
        "VXD004",
        "Unsupported loot table content",
        "The loot table '{0}' uses something the loot table generator does not know: {1}",
        "Vortex.Data",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var input = DataFiles.Folder(context, Folder)
            .Combine(DataFiles.File(context, "blocks.json").Collect());

        context.RegisterSourceOutput(input, static (output, files) =>
        {
            if (files.Left.Count == 0 || files.Right.IsEmpty)
                return;

            var renderer = new Renderer(BlockPropertyKinds.Classify(SimpleJson.Parse(files.Right[0].Text)));
            var tables = new List<(string Block, string Code)>();

            foreach (var file in files.Left)
            {
                var identifier = $"minecraft:blocks/{file.Name}";
                var block = Naming.ToIdentifier(file.Name);

                try
                {
                    tables.Add((block, renderer.Table(identifier, block, SimpleJson.Parse(file.Text))));
                }
                catch (UnsupportedLootException exception)
                {
                    output.ReportDiagnostic(Diagnostic.Create(UnsupportedLoot, Location.None, identifier, exception.Message));
                }
            }

            var code = CodeTemplate.Get("LootTables", "File")
                .Set("cases", string.Join("\n", tables.Select(table => CodeTemplate.Get("LootTables", "Case").Set("block", table.Block).Render())))
                .Set("tables", string.Join("\n\n", tables.Select(table => table.Code)))
                .Render();

            output.AddSource("LootTables.g.cs", CodeFormatter.Format(code));
        });
    }

    private sealed class UnsupportedLootException(string message) : Exception(message);

    /// <summary>
    /// Renders the pieces of a loot table. Knows the block state properties, so
    /// it can type the comparisons in state conditions.
    /// </summary>
    private sealed class Renderer(IReadOnlyDictionary<string, PropertyKind> properties)
    {
        public string Table(string identifier, string block, JsonObject table)
            => Template("Table")
                .Set("identifier", identifier)
                .Set("block", block)
                .Set("pools", Join(",\n", table.GetArray("pools"), pool => Pool((JsonObject)pool!)))
                .Set("functions", Functions(table))
                .Render();

        private string Pool(JsonObject pool)
            => Template("Pool")
                .Set("rolls", Number(pool["rolls"]))
                .Set("bonusRolls", Number(pool.TryGetValue("bonus_rolls", out var bonus) ? bonus : 0d))
                .Set("entries", Join(",\n", pool.GetArray("entries"), entry => Entry((JsonObject)entry!)))
                .Set("conditions", Conditions(pool))
                .Set("functions", Functions(pool))
                .Render();

        private string Entry(JsonObject entry)
            => Type(entry, "type") switch
            {
                "item" => Template("ItemEntry")
                    .Set("item", Naming.ToIdentifier(entry.GetString("name")!))
                    .Set("conditions", Conditions(entry))
                    .Set("functions", Functions(entry))
                    .Render(),
                "alternatives" => Template("AlternativesEntry")
                    .Set("children", Join(",\n", entry.GetArray("children"), child => Entry((JsonObject)child!)))
                    .Set("conditions", Conditions(entry))
                    .Render(),
                "dynamic" => Template("DynamicEntry")
                    .Set("contents", Naming.ToIdentifier(entry.GetString("name")!))
                    .Set("conditions", Conditions(entry))
                    .Render(),
                var other => throw new UnsupportedLootException($"the entry type '{other}'"),
            };

        private string Conditions(JsonObject owner)
            => Join(", ", owner.GetArray("conditions"), condition => Condition((JsonObject)condition!));

        private string Condition(JsonObject condition)
            => Type(condition, "condition") switch
            {
                "survives_explosion" => Template("SurvivesExplosion").Render(),
                "block_state_property" => Template("BlockStateProperty")
                    .Set("block", Naming.ToIdentifier(condition.GetString("block")!))
                    .Set("matches", StateMatches(condition.GetObject("properties")!))
                    .Render(),
                "match_tool" => MatchTool(condition.GetObject("predicate") ?? new JsonObject()),
                "table_bonus" => Template("TableBonus")
                    .Set("enchantment", Naming.ToIdentifier(condition.GetString("enchantment")!))
                    .Set("chances", Join(", ", condition.GetArray("chances"), Double))
                    .Render(),
                "random_chance" => Template("RandomChance").Set("chance", Double(condition["chance"])).Render(),
                "any_of" => Template("AnyOf")
                    .Set("terms", Join(", ", condition.GetArray("terms"), term => Condition((JsonObject)term!)))
                    .Render(),
                "inverted" => Template("Inverted").Set("term", Condition(condition.GetObject("term")!)).Render(),
                "location_check" => LocationCheck(condition),
                "entity_properties" when condition.GetObject("predicate") is { Count: 0 } => Template("EntityProperties").Render(),
                var other => throw new UnsupportedLootException($"the condition '{other}' in {Describe(condition)}"),
            };

        private string MatchTool(JsonObject predicate)
        {
            var items = predicate.TryGetValue("items", out var value) ? Set("Item", value) : "null";
            string enchantment = "null", minimumLevel = "0";

            if (predicate.GetObject("predicates") is { } components)
            {
                var enchantments = components.GetArray("minecraft:enchantments");
                if (components.Count != 1 || enchantments is not { Count: 1 } || enchantments[0] is not JsonObject required)
                    throw new UnsupportedLootException($"the tool predicate {Describe(predicate)}");

                enchantment = "Enchantment." + Naming.ToIdentifier(required.GetString("enchantments")!);
                minimumLevel = (required.GetObject("levels")?.GetInt("min") ?? 1).ToString(CultureInfo.InvariantCulture);
            }

            if (predicate.Keys.Any(key => key is not ("items" or "predicates")))
                throw new UnsupportedLootException($"the tool predicate {Describe(predicate)}");

            return Template("MatchTool")
                .Set("items", items)
                .Set("enchantment", enchantment)
                .Set("minimumLevel", minimumLevel)
                .Render();
        }

        private string LocationCheck(JsonObject condition)
        {
            var block = condition.GetObject("predicate")?.GetObject("block")
                ?? throw new UnsupportedLootException($"the location predicate {Describe(condition)}");

            return Template("LocationCheck")
                .Set("x", (condition.GetInt("offsetX") ?? 0).ToString(CultureInfo.InvariantCulture))
                .Set("y", (condition.GetInt("offsetY") ?? 0).ToString(CultureInfo.InvariantCulture))
                .Set("z", (condition.GetInt("offsetZ") ?? 0).ToString(CultureInfo.InvariantCulture))
                .Set("blocks", Set("Block", block["blocks"]))
                .Set("matches", block.GetObject("state") is { } state
                    ? Template("StatePredicate").Set("matches", StateMatches(state)).Render()
                    : "null")
                .Render();
        }

        /// <summary>
        /// Turns required property values into a typed comparison, such as
        /// <c>state.Get(BlockProperties.Half) == HalfValue.Upper</c>.
        /// </summary>
        private string StateMatches(JsonObject required)
            => string.Join(" && ", required.Select(property =>
            {
                if (property.Value is not string value || !properties.TryGetValue(property.Key, out var kind))
                    throw new UnsupportedLootException($"the state requirement {property.Key}={property.Value}");

                return Template("StateMatch")
                    .Set("property", Naming.ToIdentifier(property.Key))
                    .Set("value", kind.ToExpression(property.Key, value))
                    .Render();
            }));

        private string Functions(JsonObject owner)
            => Join(", ", owner.GetArray("functions"), function => Function((JsonObject)function!));

        private string Function(JsonObject function)
            => Type(function, "function") switch
            {
                "set_count" => Template("SetCount")
                    .Set("count", Number(function["count"]))
                    .Set("add", function.GetBool("add") == true ? "true" : "false")
                    .Set("conditions", Conditions(function))
                    .Render(),
                "explosion_decay" => Template("ExplosionDecay").Set("conditions", Conditions(function)).Render(),
                "apply_bonus" => Template("ApplyBonus")
                    .Set("enchantment", Naming.ToIdentifier(function.GetString("enchantment")!))
                    .Set("formula", Formula(function))
                    .Set("conditions", Conditions(function))
                    .Render(),
                "limit_count" => Template("LimitCount")
                    .Set("min", function.GetObject("limit")?.GetDouble("min") is { } min ? Double(min) : "null")
                    .Set("max", function.GetObject("limit")?.GetDouble("max") is { } max ? Double(max) : "null")
                    .Set("conditions", Conditions(function))
                    .Render(),
                "copy_components" => Template("CopyComponents")
                    .Set("components", Join(", ", function.GetArray("include"), component => "DataComponentType." + Naming.ToIdentifier((string)component!)))
                    .Set("conditions", Conditions(function))
                    .Render(),
                "copy_state" => Template("CopyState")
                    .Set("block", Naming.ToIdentifier(function.GetString("block")!))
                    .Set("properties", Join(", ", function.GetArray("properties"), property => "BlockProperty." + Naming.ToIdentifier((string)property!)))
                    .Set("conditions", Conditions(function))
                    .Render(),
                var other => throw new UnsupportedLootException($"the function '{other}'"),
            };

        private static string Formula(JsonObject function)
        {
            var parameters = function.GetObject("parameters") ?? new JsonObject();

            return Naming.StripNamespace(function.GetString("formula") ?? "") switch
            {
                "ore_drops" => Template("OreDrops").Render(),
                "uniform_bonus_count" => Template("UniformBonusCount")
                    .Set("bonusMultiplier", (parameters.GetInt("bonusMultiplier") ?? 1).ToString(CultureInfo.InvariantCulture))
                    .Render(),
                "binomial_with_bonus_count" => Template("BinomialWithBonusCount")
                    .Set("extra", (parameters.GetInt("extra") ?? 0).ToString(CultureInfo.InvariantCulture))
                    .Set("probability", Double(parameters["probability"]))
                    .Render(),
                var other => throw new UnsupportedLootException($"the bonus formula '{other}'"),
            };
        }

        /// <summary>
        /// A number is written plainly when it is fixed, or as an object naming
        /// how it is drawn.
        /// </summary>
        private static string Number(object? number)
        {
            if (number is double constant)
                return Template("Constant").Set("value", Double(constant)).Render();

            var provider = number as JsonObject ?? throw new UnsupportedLootException($"the number {number}");

            return Naming.StripNamespace(provider.GetString("type") ?? "constant") switch
            {
                "constant" => Template("Constant").Set("value", Double(provider["value"])).Render(),
                "uniform" => Template("Uniform").Set("min", Double(provider["min"])).Set("max", Double(provider["max"])).Render(),
                "binomial" => Template("Binomial").Set("n", Double(provider["n"])).Set("p", Double(provider["p"])).Render(),
                var other => throw new UnsupportedLootException($"the number provider '{other}'"),
            };
        }

        /// <summary>
        /// A set of items or blocks: one name, a list of names, or <c>#tag</c>.
        /// </summary>
        private static string Set(string type, object? value)
            => value switch
            {
                string tag when tag.StartsWith("#", StringComparison.Ordinal) => Template("Tag")
                    .Set("type", type)
                    .Set("name", Naming.ToIdentifier(tag.Substring(1)))
                    .Render(),
                string single => Set(type, new List<object?> { single }),
                List<object?> names => Template("Set")
                    .Set("values", Join(", ", names, name => Template("Member").Set("type", type).Set("name", Naming.ToIdentifier((string)name!)).Render()))
                    .Render(),
                _ => throw new UnsupportedLootException($"the {type.ToLowerInvariant()} list {value}"),
            };

        private static string Type(JsonObject owner, string key)
            => Naming.StripNamespace(owner.GetString(key) ?? throw new UnsupportedLootException($"'{key}' is missing in {Describe(owner)}"));

        private static string Double(object? value)
            => value is double number
                ? number.ToString("R", CultureInfo.InvariantCulture)
                : throw new UnsupportedLootException($"the number {value}");

        private static string Join(string separator, List<object?>? values, Func<object?, string> render)
            => values is null ? "" : string.Join(separator, values.Select(render));

        private static string Describe(JsonObject value)
            => "{" + string.Join(", ", value.Keys) + "}";

        private static CodeTemplate Template(string section)
            => CodeTemplate.Get("LootTables", section);
    }
}
