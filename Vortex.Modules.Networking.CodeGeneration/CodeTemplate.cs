using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Vortex.Modules.Networking.CodeGeneration;

/// <summary>
/// Represents a code template that can be rendered with variable replacements.
/// </summary>
public class CodeTemplate
{
    private readonly string _template;
    private readonly Dictionary<string, string> _replacements = new Dictionary<string, string>();

    /// <summary>
    /// Initializes a new instance of the CodeTemplate class with the specified template file name.
    /// </summary>
    /// <param name="templateFileName">The name of the template file.</param>
    public CodeTemplate(string templateFileName)
    {
        _template = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream($"{GetType().Namespace}.Templates.{templateFileName}.ct"))!.ReadToEnd();
    }

    /// <summary>
    /// Sets the value of a variable in the code template.
    /// </summary>
    /// <param name="variable">The name of the variable.</param>
    /// <param name="value">The value to set.</param>
    public void Set(string variable, string value)
    {
        _replacements[variable] = value;
    }

    /// <summary>
    /// Renders the code template with the variable replacements applied.
    /// </summary>
    /// <returns>The rendered code template.</returns>
    public string Render()
    {
        var result = _template;

        foreach (var replacement in _replacements.ToArray())
        {
            result = result.Replace($"{{{{{replacement.Key}}}}}", replacement.Value);
        }

        return result;
    }
}
