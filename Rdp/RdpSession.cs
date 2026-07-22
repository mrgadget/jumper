using uJump.Models;

namespace uJump.Rdp;

/// <summary>
/// Thin controller over an <see cref="RdpAxHost"/> that configures and drives a
/// single embedded RDP session via the control's late-bound COM interface.
/// </summary>
public class RdpSession
{
    private readonly RdpAxHost _host;
    private Connection _connection;

    public RdpSession(RdpAxHost host, Connection connection)
    {
        _host = host;
        _connection = connection;
    }

    /// <summary>0 = disconnected, 1 = connected, 2 = connecting.</summary>
    public int ConnectionState
    {
        get
        {
            try { return (int)(_host.Ocx?.Connected ?? 0); }
            catch { return 0; }
        }
    }

    public bool IsConnected => ConnectionState == 1;

    /// <summary>Configure the control from the connection and open the session.</summary>
    public void Connect(int pixelWidth, int pixelHeight)
    {
        dynamic rdp = _host.Ocx ?? throw new InvalidOperationException("RDP control not initialised.");

        rdp.Server = _connection.Host;

        // Credentials manage themselves: with CredSSP/NLA enabled and no password
        // set, Windows automatically offers the current logon and any credential
        // saved for this host (Credential Manager, key "TERMSRV/<host>"), and only
        // shows a prompt when nothing matches. We never set or store a password.
        if (!string.IsNullOrWhiteSpace(_connection.Username))
            rdp.UserName = _connection.Username; // pre-fills / selects the saved cred

        dynamic adv = rdp.AdvancedSettings9;
        adv.RDPPort = _connection.Port;
        adv.EnableCredSspSupport = true; // NLA — enables single sign-on / saved creds
        adv.SmartSizing = _connection.SmartSizing;

        // RD Gateway (common for jumphosts).
        if (!string.IsNullOrWhiteSpace(_connection.Gateway))
        {
            adv.GatewayHostname = _connection.Gateway;
            adv.GatewayUsageMethod = 1;       // TSC_PROXY_MODE_DIRECT: always use gateway
            adv.GatewayProfileUsageMethod = 1; // explicit
            adv.GatewayCredsSource = 4;        // prompt / any
        }

        // Start at the current viewport size for a crisp 1:1 session.
        SetDesktopSize(rdp, pixelWidth, pixelHeight);

        rdp.Connect();
    }

    public void Disconnect()
    {
        try
        {
            if (ConnectionState != 0)
                _host.Ocx?.Disconnect();
        }
        catch { /* control may already be tearing down */ }
    }

    /// <summary>
    /// Toggle scale-to-fit. Takes effect live while connected — no reconnect,
    /// the remote image is simply scaled into the viewport.
    /// </summary>
    public bool SmartSizing
    {
        get
        {
            try { return (bool)_host.Ocx!.AdvancedSettings9.SmartSizing; }
            catch { return _connection.SmartSizing; }
        }
        set
        {
            _connection.SmartSizing = value;
            try { _host.Ocx!.AdvancedSettings9.SmartSizing = value; } catch { }
        }
    }

    /// <summary>
    /// Try to update the remote resolution in place (dynamic display, no drop).
    /// Only works when connected to a host that supports the Display channel;
    /// returns false if unavailable, in which case the caller should reconnect.
    /// </summary>
    public bool TryUpdateDisplay(int pixelWidth, int pixelHeight)
    {
        if (!IsConnected) return false;
        try
        {
            // IMsRdpClient9.UpdateSessionDisplaySettings(width, height,
            //   physicalWidth, physicalHeight, orientation, desktopScale, deviceScale)
            _host.Ocx!.UpdateSessionDisplaySettings(
                (uint)pixelWidth, (uint)pixelHeight, 0u, 0u, 0u, 100u, 100u);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SetDesktopSize(dynamic rdp, int pixelWidth, int pixelHeight)
    {
        // Clamp to values the control accepts (200..8192).
        int w = Math.Clamp(pixelWidth, 640, 8192);
        int h = Math.Clamp(pixelHeight, 480, 8192);
        rdp.DesktopWidth = w;
        rdp.DesktopHeight = h;
    }
}
