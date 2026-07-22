using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using uJump.Models;
using uJump.Rdp;
using uJump.Services;

namespace uJump;

public partial class MainWindow : Window
{
    private readonly ConnectionStore _store = new();
    private readonly SettingsStore _settings = new();

    private readonly DispatcherTimer _cursorTimer;
    private readonly DispatcherTimer _stateTimer;

    private Connection? _connection;
    private RdpAxHost? _host;
    private RdpSession? _session;

    private bool _sessionActive;
    private bool _fullscreen;
    private bool _hasConnected;
    private bool _closing;
    private Rect _launcherBounds;   // window bounds to return to when the session ends
    private Rect _windowedBounds;   // bounds used for the windowed session mode

    public MainWindow()
    {
        InitializeComponent();

        _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _cursorTimer.Tick += PollCursor;
        _stateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _stateTimer.Tick += PollState;

        // A Popup is a separate top-level window that doesn't follow its parent, so
        // nudge it back into place whenever the window moves or resizes.
        LocationChanged += (_, _) => RepositionTopBar();
        SizeChanged += (_, _) => RepositionTopBar();

        LoadConnections();
    }

    private void RepositionTopBar()
    {
        if (!TopBar.IsOpen) return;
        var offset = TopBar.HorizontalOffset;
        TopBar.HorizontalOffset = offset + 1;
        TopBar.HorizontalOffset = offset;
    }

    // ---- launcher -----------------------------------------------------------

    private void LoadConnections()
    {
        List<Connection> connections;
        try
        {
            connections = _store.Load();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load connections from\n{_store.FilePath}\n\n{ex.Message}",
                "uJump", MessageBoxButton.OK, MessageBoxImage.Error);
            connections = new List<Connection>();
        }

        ServerCombo.ItemsSource = connections;
        var last = _settings.LastConnected;
        ServerCombo.SelectedItem = connections.FirstOrDefault(c => c.Name == last)
                                   ?? connections.FirstOrDefault();
    }

    private Connection? Selected => ServerCombo.SelectedItem as Connection;

    private void Windowed_Click(object sender, RoutedEventArgs e) => StartSession(fullScreen: false);

    private void Fullscreen_Click(object sender, RoutedEventArgs e) => StartSession(fullScreen: true);

    // ---- session lifecycle --------------------------------------------------

    private void StartSession(bool fullScreen)
    {
        var connection = Selected;
        if (connection is null)
        {
            MessageBox.Show(this, "No jumphost selected. Add one to connections.json.",
                "uJump", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _settings.LastConnected = connection.Name;
        _connection = connection;
        _launcherBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
        _windowedBounds = _launcherBounds;

        _host = new RdpAxHost();
        FormsHost.Child = _host;
        _host.EnsureCreated();
        _session = new RdpSession(_host, connection);

        SmartToggle.IsChecked = connection.SmartSizing;
        BarTitle.Text = $"{connection.Name}   ({connection.Endpoint})";

        LauncherPanel.Visibility = Visibility.Collapsed;
        SessionPanel.Visibility = Visibility.Visible;

        _sessionActive = true;
        _hasConnected = false;
        _fullscreen = false;
        ModeButton.Content = "Full screen";
        if (fullScreen)
            ApplyMode(true);

        // Connect once the session view has laid out so ClientSize is correct.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            try
            {
                var (w, h) = ViewportPixels();
                _session!.Connect(w, h);
                _cursorTimer.Start();
                _stateTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not start the RDP session:\n\n{ex.Message}",
                    "uJump", MessageBoxButton.OK, MessageBoxImage.Error);
                EndSession();
            }
        });
    }

    private void EndSession()
    {
        if (!_sessionActive) return;
        _sessionActive = false;

        _cursorTimer.Stop();
        _stateTimer.Stop();
        TopBar.IsOpen = false;

        try { _session?.Disconnect(); } catch { }
        try { FormsHost.Child = null; _host?.Dispose(); } catch { }
        _session = null;
        _host = null;

        // Return the window to the launcher.
        _fullscreen = false;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        WindowState = WindowState.Normal;
        Left = _launcherBounds.Left; Top = _launcherBounds.Top;
        Width = _launcherBounds.Width; Height = _launcherBounds.Height;

        SessionPanel.Visibility = Visibility.Collapsed;
        LauncherPanel.Visibility = Visibility.Visible;
    }

    // ---- window modes -------------------------------------------------------

    private void ApplyMode(bool fullScreen)
    {
        if (fullScreen == _fullscreen) return;

        if (fullScreen && !_fullscreen)
            _windowedBounds = new Rect(Left, Top, ActualWidth, ActualHeight);

        _fullscreen = fullScreen;

        if (fullScreen)
        {
            // Borderless, sized to the WORK AREA (not the whole screen) so the local
            // taskbar stays visible and the remote taskbar stacks above it.
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Normal;
            var wa = SystemParameters.WorkArea;
            Left = wa.Left; Top = wa.Top; Width = wa.Width; Height = wa.Height;
            ModeButton.Content = "Windowed";
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Normal;
            Left = _windowedBounds.Left; Top = _windowedBounds.Top;
            Width = _windowedBounds.Width; Height = _windowedBounds.Height;
            ModeButton.Content = "Full screen";
        }

        AdjustRemoteToViewport();
    }

    private void Mode_Click(object sender, RoutedEventArgs e)
    {
        TopBar.IsOpen = false;
        ApplyMode(!_fullscreen);
    }

    /// <summary>After a resize/mode change, match the remote resolution live if the
    /// host supports it (no reconnect); otherwise smart sizing scales the image.</summary>
    private void AdjustRemoteToViewport()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (_session is null || !_session.IsConnected) return;
            var (w, h) = ViewportPixels();
            _session.TryUpdateDisplay(w, h);
        });
    }

    private (int Width, int Height) ViewportPixels()
    {
        int w = _host?.ClientSize.Width ?? 0;
        int h = _host?.ClientSize.Height ?? 0;
        if (w < 640 || h < 480) { w = 1280; h = 800; }
        return (w, h);
    }

    // ---- top control bar reveal --------------------------------------------

    private void PollCursor(object? sender, EventArgs e)
    {
        if (_closing || !_sessionActive) return;

        var src = PresentationSource.FromVisual(this);
        if (src is null) return;
        double scale = src.CompositionTarget.TransformToDevice.M22;

        var tl = RootGrid.PointToScreen(new System.Windows.Point(0, 0));
        var tr = RootGrid.PointToScreen(new System.Windows.Point(RootGrid.ActualWidth, 0));
        var pos = System.Windows.Forms.Cursor.Position; // screen pixels

        bool withinX = pos.X >= tl.X && pos.X <= tr.X;
        double revealBand = 44 * scale;

        bool open;
        if (!withinX)
            open = false;
        else if (!TopBar.IsOpen)
            open = pos.Y <= tl.Y + 2 && pos.Y >= tl.Y - 6; // must hit the content's top edge
        else
            open = pos.Y <= tl.Y + revealBand;               // stay open over the bar

        TopBar.IsOpen = open;
    }

    // ---- session state ------------------------------------------------------

    private void PollState(object? sender, EventArgs e)
    {
        if (_session is null) return;
        var state = _session.ConnectionState;
        if (state == 1) _hasConnected = true;

        // Once we've been connected, a drop returns us to the launcher.
        if (_hasConnected && state == 0 && _sessionActive)
            EndSession();
    }

    // ---- bar actions --------------------------------------------------------

    private void Smart_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        _session.SmartSizing = SmartToggle.IsChecked == true;
        if (SmartToggle.IsChecked != true)
            AdjustRemoteToViewport();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => EndSession();

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        _cursorTimer.Stop();
        _stateTimer.Stop();
        TopBar.IsOpen = false;
        try { _session?.Disconnect(); } catch { }
        try { FormsHost.Child = null; _host?.Dispose(); } catch { }
    }
}
