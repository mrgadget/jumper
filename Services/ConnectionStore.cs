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

    public ConnectionStore()
    {
        FilePath = Path.Combine(AppContext.BaseDirectory, "connections.json");
    }

    public List<Connection> Load()
    {
        if (!File.Exists(FilePath))
        {
            var sample = SampleFile();
            Save(sample.Connections);
            return sample.Connections;
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
                Group = "Production",
                Notes = "Edit connections.json to add your own hosts.",
            },
            new Connection
            {
                Name = "Example via RD Gateway",
                Host = "10.0.0.15",
                Port = 3389,
                Username = "DOMAIN\\your.user",
                Gateway = "gateway.example.com",
                Group = "Production",
            },
        },
    };
}
