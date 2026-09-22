using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Vortex.Modules.Networking.CodeGeneration;

/// <summary>
/// A piece of code with <c>{{placeholders}}</c>, taken from one section of a
/// template file.
/// </summary>
/// <remarks>
/// <para>
/// Template files live under <c>Templates/</c> as embedded resources. A file is
/// split into sections by lines of the form <c>## SectionName</c>, so related
/// snippets stay together in one file.
/// </para>
/// <para>
/// Both setting a placeholder the template does not have and rendering with one
/// left unset throw. A mistake in a template therefore fails the generator
/// instead of quietly producing code that does not compile somewhere else.
/// </para>
/// </remarks>
internal sealed class CodeTemplate
{
    private const string SectionMarker = "## ";

    private static readonly Regex PlaceholderPattern = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Files = new();

    private readonly string _template;
    private readonly HashSet<string> _placeholders;
    private readonly Dictionary<string, string> _values = [];

    private CodeTemplate(string template)
    {
        _template = template;
        _placeholders = new HashSet<string>(PlaceholderPattern.Matches(template).Cast<Match>().Select(m => m.Groups[1].Value));
    }

    /// <summary>
    /// Gets a fresh instance of a template section.
    /// </summary>
    /// <param name="file">The template file name without its <c>.ct</c> extension.</param>
    /// <param name="section">The section inside the file.</param>
    public static CodeTemplate Get(string file, string section)
    {
        var sections = Files.GetOrAdd(file, LoadFile);

        if (!sections.TryGetValue(section, out var template))
            throw new ArgumentException($"The template file '{file}' has no section '{section}'.");

        return new CodeTemplate(template);
    }

    /// <summary>
    /// Sets the value of a placeholder.
    /// </summary>
    public CodeTemplate Set(string placeholder, string value)
    {
        if (!_placeholders.Contains(placeholder))
            throw new ArgumentException($"The template has no placeholder '{placeholder}'.");

        _values[placeholder] = value;

        return this;
    }

    /// <summary>
    /// Renders the template with every placeholder replaced.
    /// </summary>
    /// <remarks>
    /// Replacing happens in a single pass, so a value that itself contains
    /// <c>{{...}}</c> is inserted as it is rather than being replaced again.
    /// </remarks>
    public string Render()
    {
        var missing = _placeholders.Where(p => !_values.ContainsKey(p)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"The template placeholders {string.Join(", ", missing)} were not set.");

        return PlaceholderPattern.Replace(_template, m => _values[m.Groups[1].Value]);
    }

    private static IReadOnlyDictionary<string, string> LoadFile(string file)
    {
        var assembly = typeof(CodeTemplate).Assembly;
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith($".Templates.{file}.ct", StringComparison.Ordinal))
            ?? throw new ArgumentException($"There is no template file '{file}'.");

        using var reader = new StreamReader(assembly.GetManifestResourceStream(resourceName)!);

        var sections = new Dictionary<string, string>();
        string? currentSection = null;
        var currentLines = new List<string>();

        void Flush()
        {
            if (currentSection is not null)
                sections[currentSection] = string.Join("\n", currentLines).Trim();
        }

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith(SectionMarker, StringComparison.Ordinal))
            {
                Flush();

                currentSection = line.Substring(SectionMarker.Length).Trim();
                currentLines.Clear();

                continue;
            }

            currentLines.Add(line);
        }

        Flush();

        return sections;
    }
}
