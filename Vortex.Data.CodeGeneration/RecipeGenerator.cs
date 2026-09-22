using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Vortex.CodeGeneration;

namespace Vortex.Data.CodeGeneration;

/// <summary>
/// Generates <c>Recipes</c> from the recipe files of Mojang's data generator:
/// one typed property per recipe, and the list of all of them.
/// </summary>
/// <remarks>
/// Lyrox combined the recipe files into one JSON document to read at runtime.
/// Here they become code instead, so the items and tags they name are checked
/// by the compiler and nothing is parsed when the bot starts.
/// </remarks>
[Generator]
public sealed class RecipeGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor UnknownRecipeType = new(
        "VXD003",
        "Unknown recipe type",
        "The recipe '{0}' has the type '{1}', which the recipe generator does not know",
        "Vortex.Data",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
        => context.RegisterSourceOutput(DataFiles.Folder(context, "recipe"), static (output, files) =>
        {
            if (files.Count == 0)
                return;

            var categories = new SortedSet<string>(StringComparer.Ordinal);
            var recipes = new List<(string Name, string Code)>();

            foreach (var file in files)
            {
                var identifier = "minecraft:" + file.Name;
                var recipe = SimpleJson.Parse(file.Text);

                if (recipe.GetString("category") is { } category)
                    categories.Add(category);

                if (Render(identifier, recipe) is not { } rendered)
                {
                    output.ReportDiagnostic(Diagnostic.Create(UnknownRecipeType, Location.None, identifier, recipe.GetString("type")));
                    continue;
                }

                var name = Naming.ToIdentifier(identifier);

                recipes.Add((name, Template("Property")
                    .Set("identifier", identifier)
                    .Set("type", rendered.Type)
                    .Set("name", name)
                    .Set("value", rendered.Value)
                    .Render()));
            }

            var code = Template("File")
                .Set("categories", string.Join("\n", categories.Select(category => Template("Category")
                    .Set("identifier", category)
                    .Set("name", Naming.ToIdentifier(category))
                    .Render())))
                .Set("recipes", string.Join("\n", recipes.Select(recipe => recipe.Code)))
                .Set("all", string.Join("\n", recipes.Select(recipe => Template("AllEntry").Set("name", recipe.Name).Render())))
                .Render();

            output.AddSource("Recipes.g.cs", CodeFormatter.Format(code));
        });

    /// <summary>
    /// Renders the construction of one recipe.
    /// </summary>
    /// <returns>The recipe's C# type and the expression creating it, or <c>null</c> for an unknown type.</returns>
    private static (string Type, string Value)? Render(string identifier, JsonObject recipe)
    {
        var type = Naming.StripNamespace(recipe.GetString("type") ?? "");

        switch (type)
        {
            case "crafting_shaped":
                return ("ShapedRecipe", Crafting("Shaped", identifier, recipe)
                    .Set("pattern", string.Join(", ", (recipe.GetArray("pattern") ?? new List<object?>()).Cast<string>().Select(row => Naming.ToLiteral(row))))
                    .Set("key", string.Join(", ", (recipe.GetObject("key") ?? new JsonObject()).Select(entry => Template("KeyEntry")
                        .Set("symbol", Naming.ToLiteral(entry.Key[0]))
                        .Set("ingredient", Ingredient(entry.Value))
                        .Render())))
                    .Render());

            case "crafting_shapeless":
                return ("ShapelessRecipe", Crafting("Shapeless", identifier, recipe)
                    .Set("ingredients", string.Join(", ", (recipe.GetArray("ingredients") ?? new List<object?>()).Select(Ingredient)))
                    .Render());

            case "smelting" or "blasting" or "smoking" or "campfire_cooking":
                return ("CookingRecipe", Crafting("Cooking", identifier, recipe)
                    .Set("station", Naming.ToIdentifier(type))
                    .Set("ingredient", Ingredient(recipe["ingredient"]))
                    .Set("cookingTime", (recipe.GetInt("cookingtime") ?? 0).ToString(CultureInfo.InvariantCulture))
                    .Set("experience", (recipe.GetDouble("experience") ?? 0).ToString("R", CultureInfo.InvariantCulture))
                    .Render());

            case "stonecutting":
                return ("StonecuttingRecipe", Template("Stonecutting")
                    .Set("identifier", Naming.ToLiteral(identifier))
                    .Set("ingredient", Ingredient(recipe["ingredient"]))
                    .Set("result", Result(recipe))
                    .Render());

            case "smithing_transform":
                return ("SmithingTransformRecipe", Smithing("SmithingTransform", identifier, recipe)
                    .Set("result", Result(recipe))
                    .Render());

            case "smithing_trim":
                return ("SmithingTrimRecipe", Smithing("SmithingTrim", identifier, recipe).Render());

            case "crafting_decorated_pot":
            case var special when special.StartsWith("crafting_special_", StringComparison.Ordinal):
                return ("SpecialRecipe", Template("Special")
                    .Set("identifier", Naming.ToLiteral(identifier))
                    .Set("category", Naming.ToIdentifier(recipe.GetString("category") ?? "misc"))
                    .Set("serializer", Naming.ToIdentifier(type))
                    .Render());

            default:
                return null;
        }
    }

    /// <summary>
    /// A template with the parts crafting and cooking recipes share filled in.
    /// </summary>
    private static CodeTemplate Crafting(string section, string identifier, JsonObject recipe)
        => Template(section)
            .Set("identifier", Naming.ToLiteral(identifier))
            .Set("category", Naming.ToIdentifier(recipe.GetString("category") ?? "misc"))
            .Set("group", recipe.GetString("group") is { } group ? Naming.ToLiteral(group) : "null")
            .Set("result", Result(recipe));

    private static CodeTemplate Smithing(string section, string identifier, JsonObject recipe)
        => Template(section)
            .Set("identifier", Naming.ToLiteral(identifier))
            .Set("template", Ingredient(recipe["template"]))
            .Set("base", Ingredient(recipe["base"]))
            .Set("addition", Ingredient(recipe["addition"]));

    private static string Result(JsonObject recipe)
    {
        var result = recipe.GetObject("result")!;

        return Template("Result")
            .Set("item", Naming.ToIdentifier(result.GetString("id")!))
            .Set("count", (result.GetInt("count") ?? 1).ToString(CultureInfo.InvariantCulture))
            .Render();
    }

    /// <summary>
    /// An ingredient is an item, a tag, or a list of alternatives of either.
    /// </summary>
    private static string Ingredient(object? ingredient)
        => ingredient switch
        {
            List<object?> alternatives => Template("AnyOf").Set("alternatives", string.Join(", ", alternatives.Select(Ingredient))).Render(),
            JsonObject item when item.GetString("item") is { } name => Template("Item").Set("name", Naming.ToIdentifier(name)).Render(),
            JsonObject tag when tag.GetString("tag") is { } name => Template("Tag").Set("name", Naming.ToIdentifier(name)).Render(),
            _ => throw new FormatException($"Unexpected ingredient: {ingredient}"),
        };

    private static CodeTemplate Template(string section)
        => CodeTemplate.Get("Recipes", section);
}
