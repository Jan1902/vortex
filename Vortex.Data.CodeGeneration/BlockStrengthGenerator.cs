using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Vortex.CodeGeneration;

namespace Vortex.Data.CodeGeneration;

/// <summary>
/// Generates <c>BlockStrength</c>: how hard each block is and whether it needs
/// the right tool to drop anything.
/// </summary>
/// <remarks>
/// Neither is in Mojang's blocks report, which only lists states, so both come
/// from PrismarineJS's <c>blocks.json</c>: its <c>hardness</c>, and whether it
/// names <c>harvestTools</c>.
/// </remarks>
[Generator]
public sealed class BlockStrengthGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => context.RegisterSourceOutput(DataFiles.File(context, "prismarine/blocks.json"), static (output, file) =>
        {
            var blocks = SimpleJson.ParseArray(file.Text)
                .OfType<JsonObject>()
                .Where(block => block.GetString("name") is not null)
                .Select(block => (
                    Name: Naming.ToIdentifier(block.GetString("name")!),
                    Hardness: block.GetDouble("hardness") ?? -1,
                    RequiresTool: block.GetObject("harvestTools") is { Count: > 0 }))
                .ToList();

            var byHardness = blocks
                .GroupBy(block => block.Hardness)
                .OrderByDescending(group => group.Count())
                .ToList();

            // The most common hardness is the default arm rather than a long pattern.
            var code = Template("File")
                .Set("hardness", string.Join("\n", byHardness.Skip(1).OrderBy(group => group.Key).Select(group => Template("Case")
                    .Set("blocks", Or(group.Select(block => block.Name)))
                    .Set("value", Float(group.Key))
                    .Render())))
                .Set("defaultHardness", byHardness.Count > 0 ? Float(byHardness[0].Key) : "0f")
                .Set("requiresTool", Or(blocks.Where(block => block.RequiresTool).Select(block => block.Name)))
                .Render();

            output.AddSource("BlockStrength.g.cs", CodeFormatter.Format(code));
        });

    private static string Or(IEnumerable<string> names)
        => string.Join(" or ", names.Select(name => Template("Block").Set("name", name).Render()));

    private static string Float(double value)
        => value.ToString("R", CultureInfo.InvariantCulture) + "f";

    private static CodeTemplate Template(string section)
        => CodeTemplate.Get("BlockStrength", section);
}
