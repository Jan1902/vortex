using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Vortex.CodeGeneration;

/// <summary>
/// One data file handed to a generator through <c>AdditionalFiles</c>.
/// </summary>
/// <param name="Name">
/// The file's path below the folder it was looked up in, without <c>.json</c>,
/// e.g. <c>mineable/pickaxe</c> for a tag.
/// </param>
/// <param name="Text">The file's content.</param>
internal sealed record DataFile(string Name, string Text);

/// <summary>
/// Finds the data files the update script copies into a project's
/// <c>Resources</c> folder.
/// </summary>
/// <remarks>
/// The files are passed as <c>AdditionalFiles</c> rather than embedded in the
/// generator, so a generator only runs in the project that lists them and
/// reruns only when they change.
/// </remarks>
internal static class DataFiles
{
    private const string ResourcesFolder = "/Resources/";

    /// <summary>
    /// The single file at <paramref name="path"/> below <c>Resources</c>, e.g. <c>blocks.json</c>.
    /// </summary>
    public static IncrementalValuesProvider<DataFile> File(IncrementalGeneratorInitializationContext context, string path)
        => context.AdditionalTextsProvider
            .Where(file => Normalize(file.Path).EndsWith(ResourcesFolder + path, StringComparison.OrdinalIgnoreCase))
            .Select((file, cancellationToken) => new DataFile(path, file.GetText(cancellationToken)?.ToString() ?? ""));

    /// <summary>
    /// Every JSON file below <paramref name="folder"/> in <c>Resources</c>, e.g.
    /// <c>tags/block</c>, collected into one value sorted by name.
    /// </summary>
    /// <remarks>
    /// Sorted so the generated code does not depend on the order the build
    /// happens to list the files in.
    /// </remarks>
    public static IncrementalValueProvider<EquatableArray<DataFile>> Folder(IncrementalGeneratorInitializationContext context, string folder)
    {
        var prefix = ResourcesFolder + folder + "/";

        return context.AdditionalTextsProvider
            .Select((file, cancellationToken) =>
            {
                var path = Normalize(file.Path);
                var start = path.LastIndexOf(prefix, StringComparison.OrdinalIgnoreCase);

                if (start < 0 || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    return null;

                var name = path.Substring(start + prefix.Length, path.Length - start - prefix.Length - ".json".Length);

                return new DataFile(name, file.GetText(cancellationToken)?.ToString() ?? "");
            })
            .Where(file => file is not null)
            .Collect()
            .Select((files, _) => new EquatableArray<DataFile>([.. files.OrderBy(f => f!.Name, StringComparer.Ordinal).Select(f => f!)]));
    }

    private static string Normalize(string path)
        => path.Replace('\\', '/');
}
