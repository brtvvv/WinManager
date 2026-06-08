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
        const string cat1 = "Browsers & Communication";
        const string cat2 = "Development Tools";
        const string cat3 = "Media & Entertainment";
        const string cat4 = "System & Security";
        const string cat5 = "Productivity";

        return new List<SoftwareItem>
        {
            new("chrome",   "Google Chrome",   "Google.Chrome",
                description: "Fast browser by Google",
                category: cat1, iconUrl: "https://icon.horse/icon/google.com"),
            new("firefox",  "Mozilla Firefox", "Mozilla.Firefox",
                description: "Open-source browser by Mozilla",
                category: cat1, iconUrl: "https://icon.horse/icon/mozilla.org"),
            new("operagx",  "Opera GX",        "Opera.OperaGX",
                description: "Gaming browser with resource limits",
                category: cat1, iconUrl: "https://icon.horse/icon/opera.com"),
            new("discord",  "Discord",         "Discord.Discord",
                description: "Voice, video & text chat for communities",
                category: cat1, iconUrl: "https://icon.horse/icon/discord.com"),
            new("telegram", "Telegram",        "Telegram.TelegramDesktop",
                description: "Fast and secure messaging app",
                category: cat1, iconUrl: "https://icon.horse/icon/telegram.org"),

            new("vscode",     "Visual Studio Code", "Microsoft.VisualStudioCode",
                description: "Lightweight code editor by Microsoft",
                category: cat2, iconUrl: "https://icon.horse/icon/code.visualstudio.com"),
            new("git",        "Git",               "Git.Git",
                description: "Distributed version control system",
                category: cat2, iconUrl: "https://icon.horse/icon/git-scm.com"),
            new("python",     "Python 3",           "Python.Python.3",
                description: "General-purpose programming language",
                category: cat2, iconUrl: "https://icon.horse/icon/python.org"),
            new("nodejs",     "Node.js LTS",        "OpenJS.NodeJS.LTS",
                description: "JavaScript runtime built on V8",
                category: cat2, iconUrl: "https://icon.horse/icon/nodejs.org"),
            new("jetbrains",  "JetBrains Toolbox",  "JetBrains.Toolbox",
                description: "Manager for all JetBrains IDEs",
                category: cat2, iconUrl: "https://icon.horse/icon/jetbrains.com"),

            new("vlc",     "VLC Media Player", "VideoLAN.VLC",
                description: "Free cross-platform media player",
                category: cat3, iconUrl: "https://icon.horse/icon/videolan.org"),
            new("spotify", "Spotify",          "Spotify.Spotify",
                description: "Music streaming service",
                category: cat3, iconUrl: "https://icon.horse/icon/spotify.com"),
            new("steam",   "Steam",            "Valve.Steam",
                description: "PC gaming platform by Valve",
                category: cat3, iconUrl: "https://icon.horse/icon/steampowered.com"),
            new("obs",     "OBS Studio",       "OBSProject.OBSStudio",
                description: "Free software for video recording and streaming",
                category: cat3, iconUrl: "https://icon.horse/icon/obsproject.com"),
            new("mpchc",   "MPC-HC",           "clsid2.mpc-hc",
                description: "Lightweight media player for Windows",
                category: cat3, iconUrl: "https://icon.horse/icon/mpc-hc.org"),

            new("7zip",        "7-Zip",           "7zip.7zip",
                description: "High-compression archive manager",
                category: cat4, iconUrl: "https://icon.horse/icon/7-zip.org"),
            new("malwarebytes","Malwarebytes",    "Malwarebytes.Malwarebytes",
                description: "Anti-malware and security scanner",
                category: cat4, iconUrl: "https://icon.horse/icon/malwarebytes.com"),
            new("rufus",       "Rufus",           "Rufus.Rufus",
                description: "Create bootable USB drives",
                category: cat4, iconUrl: "https://icon.horse/icon/rufus.ie"),
            new("cpuz",        "CPU-Z",           "CPUID.CPU-Z",
                description: "Hardware information and diagnostics",
                category: cat4, iconUrl: "https://icon.horse/icon/cpuid.com"),
            new("crystaldisk", "CrystalDiskInfo", "CrystalDewWorld.CrystalDiskInfo",
                description: "HDD and SSD health monitoring tool",
                category: cat4, iconUrl: "https://icon.horse/icon/crystalmark.info"),

            new("notepadpp",   "Notepad++",    "Notepad++.Notepad++",
                description: "Advanced text and code editor",
                category: cat5, iconUrl: "https://icon.horse/icon/notepad-plus-plus.org"),
            new("libreoffice", "LibreOffice",  "TheDocumentFoundation.LibreOffice",
                description: "Free and open-source office suite",
                category: cat5, iconUrl: "https://icon.horse/icon/libreoffice.org"),
            new("obsidian",    "Obsidian",     "Obsidian.Obsidian",
                description: "Knowledge base and note-taking with Markdown",
                category: cat5, iconUrl: "https://icon.horse/icon/obsidian.md"),
            new("sharex",      "ShareX",       "ShareX.ShareX",
                description: "Screen capture and file sharing tool",
                category: cat5, iconUrl: "https://icon.horse/icon/getsharex.com"),
            new("everything",  "Everything",  "voidtools.Everything",
                description: "Instant file search for Windows",
                category: cat5, iconUrl: "https://icon.horse/icon/voidtools.com"),
        };
    }

    public async Task InstallAsync(IEnumerable<SoftwareItem> apps, Action<string> log,
        CancellationToken cancellationToken = default, Action<int, int, string>? onProgress = null)
    {
        var list = apps.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            var app = list[i];
            onProgress?.Invoke(i, list.Count, app.Name);

            if (string.IsNullOrWhiteSpace(app.WingetId))
            {
                log($"No Winget ID for {app.Name}");
                continue;
            }

            log($"[Install] {app.Name}");
            var result = await _runner.RunAsync("winget",
                $"install --id \"{app.WingetId}\" --exact --silent --accept-source-agreements --accept-package-agreements",
                cancellationToken);
            log(result.Output);
        }
        onProgress?.Invoke(list.Count, list.Count, "Done");
    }

    public async Task UninstallAsync(IEnumerable<SoftwareItem> apps, Action<string> log,
        CancellationToken cancellationToken = default, Action<int, int, string>? onProgress = null)
    {
        var list = apps.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            var app = list[i];
            onProgress?.Invoke(i, list.Count, app.Name);

            if (string.IsNullOrWhiteSpace(app.WingetId))
            {
                log($"No Winget ID for {app.Name}");
                continue;
            }

            log($"[Uninstall] {app.Name}");
            var result = await _runner.RunAsync("winget",
                $"uninstall --id \"{app.WingetId}\" --exact --silent --accept-source-agreements",
                cancellationToken);
            if (result.Success)
                log(result.Output);
            else
                log($"[Uninstall FAILED] {app.Name}: {result.Output.Trim()}");
        }
        onProgress?.Invoke(list.Count, list.Count, "Done");
    }
}

