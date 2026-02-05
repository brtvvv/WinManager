using System.Text.Json;
using WinManager.Models;

namespace WinManager.Services;

/// <summary>
/// Provides installed Appx packages for all users via PowerShell (Get-AppxPackage -AllUsers).
/// </summary>
public class PowerShellAppxService
{
    private const string GetInstalledAppxCommand =
        "Get-AppxPackage -AllUsers | Select-Object Name, PackageFullName | ConvertTo-Json -Depth 2 -Compress";

    private readonly ProcessRunner _runner = new();

    /// <summary>
    /// Returns list of installed Appx packages (Name, PackageFullName) for all users.
    /// Uses PowerShell and JSON output for reliable parsing.
    /// </summary>
    public async Task<List<AppxInstalledItem>> GetInstalledAppxAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{GetInstalledAppxCommand}\"",
            cancellationToken);

        if (!result.Success)
        {
            return new List<AppxInstalledItem>();
        }

        var json = result.Output?.Trim();
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<AppxInstalledItem>();
        }

        try
        {
            // PowerShell may output a single object (not array) when there's one item
            if (json.StartsWith('['))
            {
                var list = JsonSerializer.Deserialize<List<AppxInstalledItem>>(json);
                return list ?? new List<AppxInstalledItem>();
            }

            if (json == "null")
            {
                return new List<AppxInstalledItem>();
            }

            var single = JsonSerializer.Deserialize<AppxInstalledItem>(json);
            return single is null ? new List<AppxInstalledItem>() : new List<AppxInstalledItem> { single };
        }
        catch
        {
            return new List<AppxInstalledItem>();
        }
    }
}
