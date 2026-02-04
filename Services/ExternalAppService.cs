using WinManager.Models;

namespace WinManager.Services;

/// <summary>
/// Provides definitions and winget install/uninstall for external (non-inbox) apps.
/// </summary>
public class ExternalAppService
{
    private readonly ProcessRunner _runner = new();

    public IReadOnlyList<SoftwareItem> GetExternalApps()
    {
        return new List<SoftwareItem>
        {
            new("chrome", "Google Chrome", "Google.Chrome", description: "Google Chrome browser"),
            new("vscode", "Visual Studio Code", "Microsoft.VisualStudioCode", description: "Lightweight code editor"),
            new("vcredist", "VC++ Redistributable", "Microsoft.VCRedist.2015+.x64", description: "VC++ runtime package"),
            new("7zip", "7zip", "7zip.7zip", description: "7zip archiver"),
            new("vlc", "VLC media player", "VideoLAN.VLC", description: "Video/audio player"),
            new("notepadpp", "Notepad++", "Notepad++.Notepad++", description: "Notepad++ text editor")
        };
    }

    /// <summary>Install selected external apps through winget.</summary>
    public async Task InstallAsync(IEnumerable<SoftwareItem> apps, Action<string> log, CancellationToken cancellationToken = default)
    {
        foreach (var app in apps)
        {
            if (string.IsNullOrWhiteSpace(app.WingetId))
            {
                log($"No Winget ID for {app.Name}");
                continue;
            }

            log($"[Install] {app.Name}");
            var result = await _runner.RunAsync("winget", $"install --id \"{app.WingetId}\" --exact --silent --accept-source-agreements --accept-package-agreements", cancellationToken);
            log(result.Output);
        }
    }

    /// <summary>Uninstall selected external apps through winget.</summary>
    public async Task UninstallAsync(IEnumerable<SoftwareItem> apps, Action<string> log, CancellationToken cancellationToken = default)
    {
        foreach (var app in apps)
        {
            if (string.IsNullOrWhiteSpace(app.WingetId))
            {
                log($"No Winget ID for {app.Name}");
                continue;
            }

            log($"[Uninstall] {app.Name}");
            var result = await _runner.RunAsync("winget", $"uninstall --id \"{app.WingetId}\" --exact --silent --accept-source-agreements --accept-package-agreements", cancellationToken);
            log(result.Output);
        }
    }
}

