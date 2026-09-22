using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Vortex.CodeGeneration;

namespace Vortex.Data.CodeGeneration;

/// <summary>
/// Generates <c>ItemProperties</c> from Mojang's items report: how far an item
/// stacks, how long it lasts, and what it does as a tool.
/// </summary>
[Generator]
public sealed class ItemGenerator : IIncrementalGenerator
{
    private const string MaxStackSize = "minecraft:max_stack_size";
    private const string MaxDamage = "minecraft:max_damage";
    private const string ToolComponent = "minecraft:tool";

    public void Initialize(IncrementalGeneratorInitializationContext context)
        => context.RegisterSourceOutput(DataFiles.File(context, "items.json"), static (output, file) =>
        {
            var components = SimpleJson.Parse(file.Text)
                .Select(item => (Item: item.Key, Components: (item.Value as JsonObject)?.GetObject("components")))
                .Where(item => item.Components is not null)
                .ToList();

            var stackSizes = components
                .Where(item => item.Components!.GetInt(MaxStackSize) is not null)
                .GroupBy(item => item.Components!.GetInt(MaxStackSize)!.Value)
                .OrderByDescending(group => group.Count())
                .ToList();

            var maxDamages = components
                .Where(item => item.Components!.GetInt(MaxDamage) is not null)
                .GroupBy(item => item.Components!.GetInt(MaxDamage)!.Value);

            var tools = components
                .Where(item => item.Components!.GetObject(ToolComponent) is not null)
                .Select(item =>
                {
                    var name = Naming.ToIdentifier(item.Item);
                    var field = char.ToLowerInvariant(name[0]) + name.Substring(1);

                    return (Name: name, Field: field, Code: RenderTool(field, item.Components!.GetObject(ToolComponent)!));
                })
                .ToList();

            // The most common stack size is the default arm of the switch rather
            // than a thousand-item pattern.
            var code = CodeTemplate.Get("Items", "File")
                .Set("stackSizes", Cases(stackSizes.Skip(1)))
                .Set("defaultStackSize", stackSizes.FirstOrDefault()?.Key.ToString() ?? "64")
                .Set("maxDamages", Cases(maxDamages))
                .Set("toolCases", string.Join("\n", tools.Select(tool => CodeTemplate.Get("Items", "ToolCase")
                    .Set("name", tool.Name)
                    .Set("field", tool.Field)
                    .Render())))
                .Set("tools", string.Join("\n", tools.Select(tool => tool.Code)))
                .Render();

            output.AddSource("ItemProperties.g.cs", CodeFormatter.Format(code));
        });

    private static string RenderTool(string field, JsonObject tool)
        => CodeTemplate.Get("Items", "Tool")
            .Set("field", field)
            .Set("rules", string.Join(", ", (tool.GetArray("rules") ?? new List<object?>()).OfType<JsonObject>().Select(rule => CodeTemplate.Get("Items", "ToolRule")
                .Set("blocks", Blocks(rule["blocks"]))
                .Set("speed", rule.GetDouble("speed") is { } speed ? speed.ToString("R", CultureInfo.InvariantCulture) + "f" : "null")
                .Set("correctForDrops", rule.GetBool("correct_for_drops") is { } correct ? (correct ? "true" : "false") : "null")
                .Render())))
            .Render();

    /// <summary>
    /// The blocks a tool rule covers: a tag, one block, or a list of blocks.
    /// </summary>
    private static string Blocks(object? blocks)
        => blocks switch
        {
            string tag when tag.StartsWith("#", StringComparison.Ordinal) => CodeTemplate.Get("Items", "BlockTag").Set("name", Naming.ToIdentifier(tag.Substring(1))).Render(),
            string block => Blocks(new List<object?> { block }),
            List<object?> list => CodeTemplate.Get("Items", "BlockSet")
                .Set("blocks", string.Join(", ", list.Cast<string>().Select(block => CodeTemplate.Get("Items", "Block").Set("name", Naming.ToIdentifier(block)).Render())))
                .Render(),
            _ => throw new FormatException($"Unexpected tool rule blocks: {blocks}"),
        };

    private static string Cases(IEnumerable<IGrouping<int, (string Item, JsonObject? Components)>> groups)
        => string.Join("\n", groups
            .OrderBy(group => group.Key)
            .Select(group => CodeTemplate.Get("Items", "Case")
                .Set("items", string.Join(" or ", group.Select(item => CodeTemplate.Get("Items", "Item")
                    .Set("name", Naming.ToIdentifier(item.Item))
                    .Render())))
                .Set("value", group.Key.ToString())
                .Render()));
}
