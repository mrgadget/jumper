using System.Text.Json.Serialization;

namespace uJump.Models;

/// <summary>
/// A single RDP target (typically a jumphost). Mirrors the shape of an entry
/// in connections.json.
/// </summary>
public class Connection
{
    /// <summary>Friendly name shown in the list.</summary>
    public string Name { get; set; } = "";

    /// <summary>Hostname or IP of the target machine.</summary>
    public string Host { get; set; } = "";

    /// <summary>RDP port. Defaults to 3389.</summary>
    public int Port { get; set; } = 3389;

    /// <summary>Optional pre-filled username, e.g. "DOMAIN\\user" or "user@domain".</summary>
    public string? Username { get; set; }

    /// <summary>Optional RD Gateway hostname for reaching hosts behind a gateway.</summary>
    public string? Gateway { get; set; }

    /// <summary>Optional group used to organise the list (e.g. "Production").</summary>
    public string? Group { get; set; }

    /// <summary>
    /// When true, scale the remote desktop to fit the window (no reconnect on
    /// resize). When false, the session renders at a fixed resolution and you use
    /// "Reconnect to fit" to re-match the window size crisply.
    /// </summary>
    public bool SmartSizing { get; set; } = true;

    /// <summary>Open the embedded session maximised.</summary>
    public bool FullScreen { get; set; } = true;

    /// <summary>Windowed width when <see cref="FullScreen"/> is false.</summary>
    public int Width { get; set; } = 1280;

    /// <summary>Windowed height when <see cref="FullScreen"/> is false.</summary>
    public int Height { get; set; } = 800;

    /// <summary>Free-form note shown as a tooltip.</summary>
    public string? Notes { get; set; }

    [JsonIgnore]
    public string DisplayGroup => string.IsNullOrWhiteSpace(Group) ? "Ungrouped" : Group!;

    [JsonIgnore]
    public string Endpoint => Port == 3389 ? Host : $"{Host}:{Port}";
}
