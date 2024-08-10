namespace Vortex.Framework.Abstraction;

/// <summary>
/// Represents the configuration for a Vortex client.
/// </summary>
public class VortexClientConfiguration
{
    /// <summary>
    /// Gets or sets the hostname of the server. Default value is "localhost".
    /// </summary>
    public string Hostname { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the port number of the server. Default value is 25565.
    /// </summary>
    public int Port { get; set; } = 25565;
}
