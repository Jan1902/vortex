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

    /// <summary>
    /// Gets or sets how many chunks in each direction the client asks the server
    /// for. The server caps this at its own view distance. Default value is 8.
    /// </summary>
    public byte ViewDistance { get; set; } = 8;

    /// <summary>
    /// Gets or sets the locale reported to the server. Default value is "en_us".
    /// </summary>
    public string Locale { get; set; } = "en_us";

    /// <summary>
    /// Gets or sets a value indicating whether to log debug detail, including every
    /// packet the client has no definition for. Useful while working on the
    /// protocol, noisy otherwise. Default value is <c>false</c>.
    /// </summary>
    public bool VerboseLogging { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to respawn automatically on death.
    /// A dead player sits on the death screen and the server stops sending it
    /// chunks, so a bot that does not respawn is blind until it does.
    /// Default value is <c>true</c>.
    /// </summary>
    public bool AutoRespawn { get; set; } = true;
}
