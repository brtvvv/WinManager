using System.IO;
using System.Text.Json;
using WinManager.Models.Config;

namespace WinManager.Services;

public class ConfigService
{
    private static readonly JsonSerializerOptions _options =
        new() { WriteIndented = true };

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WinManager");

    public static string DefaultFilePath =>
        Path.Combine(DefaultDirectory, "winmanager-config.json");

    public async Task SaveAsync(WinManagerConfig config, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(config, _options);
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<WinManagerConfig?> LoadAsync(string path)
    {
        if (!File.Exists(path))
            return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<WinManagerConfig>(json, _options);
    }
}
