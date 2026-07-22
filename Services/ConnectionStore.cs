using System.IO;
using System.Text.Json;
using uJump.Models;

namespace uJump.Services;

/// <summary>
/// Loads and saves the connections file. The file lives next to the executable
/// so it is easy to find, edit by hand, and check into source control.
/// </summary>
public class ConnectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public string FilePath { get; }
    private readonly string _examplePath;

    public ConnectionStore()
    {
        FilePath = Path.Combine(AppContext.BaseDirectory, "connections.json");
        _examplePath = Path.Combine(AppContext.BaseDirectory, "connections.example.json");
    }

    public List<Connection> Load()
    {
        if (!File.Exists(FilePath))
        {
            // First run: seed the personal file from the shipped example so the
            // user has an editable starting point.
            if (File.Exists(_examplePath))
                File.Copy(_examplePath, FilePath);
            else
                Save(SampleFile().Connections);
        }

        var json = File.ReadAllText(FilePath);
        var file = JsonSerializer.Deserialize<ConnectionFile>(json, JsonOptions)
                   ?? new ConnectionFile();
        return file.Connections;
    }

    public void Save(IEnumerable<Connection> connections)
    {
        var file = new ConnectionFile { Connections = connections.ToList() };
        var json = JsonSerializer.Serialize(file, JsonOptions);
        File.WriteAllText(FilePath, json);
    }

    private static ConnectionFile SampleFile() => new()
    {
        Connections =
        {
            new Connection
            {
                Name = "Example Jumphost",
                Host = "jump01.example.com",
                Port = 3389,
                Username = "DOMAIN\\your.user",
            },
        },
    };
}
