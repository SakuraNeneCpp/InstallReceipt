using System.Text.Json;
using System.Text.Json.Serialization;
using InstallReceipt.Core.Models;

namespace InstallReceipt.Core.Persistence;

public static class SnapshotJsonStore
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static async Task SaveSnapshotAsync(Snapshot snapshot, string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? ".");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, snapshot, Options, cancellationToken);
    }

    public static async Task<Snapshot> LoadSnapshotAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Snapshot>(stream, Options, cancellationToken)
            ?? throw new InvalidDataException($"Snapshot file is empty or invalid: {path}");
    }

    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
