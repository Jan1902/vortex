using System.Reflection;

namespace Vortex.Shared;
public static class ResourceHelper
{
    public static string ReadResource(string resourceName)
    {
        var assembly = Assembly.GetCallingAssembly();
        var fullResourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(resourceName));
        using var stream = assembly.GetManifestResourceStream(fullResourceName);

        if (stream == null)
            throw new InvalidOperationException($"Resource {resourceName} not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
