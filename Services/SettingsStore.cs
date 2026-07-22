using System.IO;
using System.Text.Json;

namespace uJump.Services;

/// <summary>
/// Tiny persisted app state (currently just the last-connected server) stored in
/// %APPDATA%\uJump\state.json.
/// </summary>
public class SettingsStore
{
    private readonly string _path;

    public SettingsStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "uJump");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "state.json");
    }

    public string? LastConnected
    {
        get
        {
            try
            {
                if (!File.Exists(_path)) return null;
                var state = JsonSerializer.Deserialize<State>(File.ReadAllText(_path));
                return state?.LastConnected;
            }
            catch { return null; }
        }
        set
        {
            try { File.WriteAllText(_path, JsonSerializer.Serialize(new State { LastConnected = value })); }
            catch { /* best-effort */ }
        }
    }

    private class State
    {
        public string? LastConnected { get; set; }
    }
}
