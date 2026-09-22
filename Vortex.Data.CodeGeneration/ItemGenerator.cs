using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Vortex.CodeGeneration;

namespace Vortex.Data.CodeGeneration;

/// <summary>
/// Generates <c>ItemProperties</c> from Mojang's items report: how far an item
/// stacks and how long it lasts.
/// </summary>
[Generator]
public sealed class ItemGenerator : IIncrementalGenerator
{
    private const string MaxStackSize = "minecraft:max_stack_size";
    private const string MaxDamage = "minecraft:max_damage";

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

            // The most common stack size is the default arm of the switch rather
            // than a thousand-item pattern.
            var code = CodeTemplate.Get("Items", "File")
                .Set("stackSizes", Cases(stackSizes.Skip(1)))
                .Set("defaultStackSize", stackSizes.FirstOrDefault()?.Key.ToString() ?? "64")
                .Set("maxDamages", Cases(maxDamages))
                .Render();

            output.AddSource("ItemProperties.g.cs", CodeFormatter.Format(code));
        });

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
