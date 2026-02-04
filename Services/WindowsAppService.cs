using System.Security.Principal;
using System.Text;
using WinManager.Models;

namespace WinManager.Services;

/// <summary>
/// Handles Windows (inbox) apps: status detection, winget/appx install/uninstall, definitions list.
/// </summary>
public class WindowsAppService
{
    private readonly ProcessRunner _runner = new();

    public bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>Builds app definitions and assigns current status notes.</summary>
    public async Task<IReadOnlyList<SoftwareItem>> GetWindowsAppsAsync(CancellationToken cancellationToken = default)
    {
        var definitions = CreateDefinitions();
        var installedAppxNames = await GetInstalledAppxNames(cancellationToken);

        foreach (var app in definitions)
        {
            app.Status = await DetectStatusAsync(app, installedAppxNames, cancellationToken);
            app.StatusNote = app.Status switch
            {
                AppStatus.Installed => "Installed",
                AppStatus.NotInstalled => "Not installed",
                _ => "Unknown"
            };
        }

        return definitions;
    }

    /// <summary>Update status for a set of apps based on current system state.</summary>
    public async Task RefreshStatusAsync(IEnumerable<SoftwareItem> apps, CancellationToken cancellationToken = default)
    {
        var installedAppxNames = await GetInstalledAppxNames(cancellationToken);
        foreach (var app in apps)
        {
            app.Status = await DetectStatusAsync(app, installedAppxNames, cancellationToken);
        }
    }

    /// <summary>Install selected apps via winget if available.</summary>
    public async Task InstallAsync(IEnumerable<SoftwareItem> apps, Action<string> log, CancellationToken cancellationToken = default)
    {
        foreach (var app in apps)
        {
            app.IsBusy = true;
            log($"[Install] {app.Name}");

            if (!string.IsNullOrWhiteSpace(app.WingetId))
            {
                var args = $"install --id \"{app.WingetId}\" --exact --silent --accept-source-agreements --accept-package-agreements";
                var result = await _runner.RunAsync("winget", args, cancellationToken);
                app.StatusNote = result.Output.Trim();
                log(result.Output);
            }
            else
            {
                app.StatusNote = "No Winget package found, skipped.";
                log($"No Winget ID for {app.Name}, skipping install.");
            }

            app.IsBusy = false;
        }
    }

    /// <summary>Uninstall selected apps via winget or Appx removal fallback.</summary>
    public async Task UninstallAsync(IEnumerable<SoftwareItem> apps, Action<string> log, CancellationToken cancellationToken = default)
    {
        foreach (var app in apps)
        {
            app.IsBusy = true;
            log($"[Uninstall] {app.Name}");

            if (!string.IsNullOrWhiteSpace(app.WingetId))
            {
                var args = $"uninstall --id \"{app.WingetId}\" --exact --silent --accept-source-agreements --accept-package-agreements";
                var result = await _runner.RunAsync("winget", args, cancellationToken);
                app.StatusNote = result.Output.Trim();
                log(result.Output);
            }
            else if (app.DetectionKeywords.Any())
            {
                var keyword = app.DetectionKeywords.First();
                var ps = new StringBuilder();
                ps.Append($"Get-AppxPackage -AllUsers | Where-Object {{$_.Name -like '*{keyword}*'}} ");
                ps.Append("| ForEach-Object { Remove-AppxPackage -AllUsers $_.PackageFullName }");
                var result = await _runner.RunAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{ps}\"", cancellationToken);
                app.StatusNote = result.Output.Trim();
                log(result.Output);
            }
            else
            {
                app.StatusNote = "Cannot uninstall (no pattern defined).";
                log($"No uninstall pattern for {app.Name}.");
            }

            app.IsBusy = false;
        }
    }

    private async Task<HashSet<string>> GetInstalledAppxNames(CancellationToken cancellationToken)
    {
        var names = await _runner.RunPowerShellListAsync("Get-AppxPackage -AllUsers | Select-Object -ExpandProperty Name", cancellationToken);
        return names.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<AppStatus> DetectStatusAsync(SoftwareItem app, HashSet<string> installedAppxNames, CancellationToken cancellationToken)
    {
        if (app.DetectionKeywords.Any(pattern => installedAppxNames.Any(name => name.Contains(pattern, StringComparison.OrdinalIgnoreCase))))
        {
            return AppStatus.Installed;
        }

        if (!string.IsNullOrWhiteSpace(app.WingetId))
        {
            var installed = await IsWingetInstalled(app.WingetId!, cancellationToken);
            return installed ? AppStatus.Installed : AppStatus.NotInstalled;
        }

        return AppStatus.NotInstalled;
    }

    private async Task<bool> IsWingetInstalled(string wingetId, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync("winget", $"list --id \"{wingetId}\" --exact --accept-source-agreements --accept-package-agreements", cancellationToken);
        if (!result.Success)
        {
            return false;
        }

        return result.Output.Contains(wingetId, StringComparison.OrdinalIgnoreCase);
    }

    private static List<SoftwareItem> CreateDefinitions()
    {
        // Definitions inspired by the screenshot provided by the user.
        return new List<SoftwareItem>
        {
            new("alarms", "Alarms & Clock", "Microsoft.WindowsAlarms", new[] { "WindowsAlarms" }, "Clock and alarms"),
            new("camera", "Camera", "Microsoft.WindowsCamera", new[] { "WindowsCamera" }, "Windows camera app"),
            new("devhome", "Dev Home", "Microsoft.DevHome", new[] { "DevHome" }, "Developer dashboard"),
            new("mailcalendar", "Mail and Calendar", "microsoft.windowscommunicationsapps", new[] { "windowscommunicationsapps" }, "Mail & calendar client"),
            new("edge", "Microsoft Edge", "Microsoft.Edge", new[] { "MicrosoftEdge" }, "Default Edge browser"),
            new("teams", "Microsoft Teams", "Microsoft.Teams", new[] { "Teams" }, "Teams client"),
            new("weather", "MSN Weather", "Microsoft.BingWeather", new[] { "BingWeather" }, "Weather app"),
            new("outlook", "Outlook for Windows", "Microsoft.OutlookForWindows", new[] { "Outlook" }, "New Outlook client"),
            new("phonelink", "Phone Link", "Microsoft.YourPhone", new[] { "YourPhone" }, "Link to Android/iOS"),
            new("quickassist", "Quick Assist", "MicrosoftCorporationII.QuickAssist", new[] { "QuickAssist" }, "Remote assistance"),
            new("soundrecorder", "Sound Recorder", "Microsoft.WindowsSoundRecorder", new[] { "SoundRecorder" }, "Audio recorder"),
            new("todo", "To Do", "Microsoft.Todos", new[] { "Todos" }, "Microsoft To Do"),
            new("gamebarplugin", "Xbox Game Bar Plugin", "Microsoft.XboxGameOverlay", new[] { "XboxGameOverlay" }, "Game Bar components"),
            new("3dviewer", "3D Viewer", "Microsoft.Microsoft3DViewer", new[] { "Microsoft3DViewer" }, "3D model viewer"),
            new("mixedreality", "Mixed Reality Portal", "Microsoft.MixedReality.Portal", new[] { "MixedReality" }, "MR/VR portal"),
            new("skype", "Skype", "Microsoft.SkypeApp", new[] { "SkypeApp" }, "Skype client"),
            new("bingsearch", "Bing Search", null, new[] { "Bing" }, "Bing web search integration"),
            new("clipchamp", "Clipchamp", "Clipchamp.Clipchamp", new[] { "Clipchamp" }, "Video editor"),
            new("feedbackhub", "Feedback Hub", "Microsoft.WindowsFeedbackHub", new[] { "WindowsFeedbackHub" }, "Feedback & diagnostics"),
            new("maps", "Maps", "Microsoft.WindowsMaps", new[] { "WindowsMaps" }, "Maps app"),
            new("news", "Microsoft News", "Microsoft.BingNews", new[] { "BingNews" }, "News feed"),
            new("movies", "Movies & TV", "Microsoft.ZuneVideo", new[] { "ZuneVideo" }, "Movies & TV player"),
            new("notepad", "Notepad", "Microsoft.WindowsNotepad", new[] { "WindowsNotepad" }, "Notepad text editor"),
            new("paint", "Paint", "Microsoft.Paint", new[] { "MSPaint" }, "Classic Paint"),
            new("photos", "Photos", "Microsoft.WindowsPhotos", new[] { "WindowsPhotos", "Photos" }, "Photos viewer/editor"),
            new("snipping", "Snipping Tool", "Microsoft.ScreenSketch", new[] { "ScreenSketch" }, "Screenshot tool"),
            new("stickynotes", "Sticky Notes", "Microsoft.MicrosoftStickyNotes", new[] { "StickyNotes" }, "Sticky notes"),
            new("xbox", "Xbox", "Microsoft.GamingApp", new[] { "GamingApp" }, "Xbox app"),
            new("xboxid", "Xbox Identity Provider", "Microsoft.XboxIdentityProvider", new[] { "XboxIdentityProvider" }, "Xbox auth provider"),
            new("cortana", "Cortana", "Microsoft.549981C3F5F10", new[] { "549981C3F5F10" }, "Cortana assistant"),
            new("onenote", "OneNote", "Microsoft.Office.OneNote", new[] { "OneNote" }, "OneNote app"),
            new("tips", "Tips", "Microsoft.Getstarted", new[] { "Getstarted" }, "Windows tips"),
            new("calculator", "Calculator", "Microsoft.WindowsCalculator", new[] { "WindowsCalculator" }, "Calculator"),
            new("copilot", "Copilot", null, new[] { "Copilot" }, "Copilot experience"),
            new("gethelp", "Get Help", "Microsoft.GetHelp", new[] { "GetHelp" }, "Support/Help app"),
            new("mediaplayer", "Media Player", "Microsoft.ZuneMusic", new[] { "ZuneMusic" }, "Media Player"),
            new("store", "Microsoft Store", "Microsoft.WindowsStore", new[] { "WindowsStore" }, "Store client"),
            new("officehub", "MS 365 Copilot (Office Hub)", "Microsoft.MicrosoftOfficeHub", new[] { "MicrosoftOfficeHub" }, "Office hub"),
            new("onedrive", "OneDrive", "Microsoft.OneDrive", new[] { "OneDrive" }, "OneDrive client"),
            new("people", "People", "Microsoft.People", new[] { "Microsoft.People" }, "Contacts app"),
            new("powerautomate", "Power Automate", "Microsoft.PowerAutomateDesktop", new[] { "PowerAutomate" }, "Power Automate Desktop"),
            new("solitaire", "Solitaire Collection", "Microsoft.MicrosoftSolitaireCollection", new[] { "Solitaire" }, "Solitaire games"),
            new("terminal", "Terminal", "Microsoft.WindowsTerminal", new[] { "WindowsTerminal" }, "Windows Terminal"),
            new("xboxgamebar", "Xbox Game Bar", "Microsoft.XboxGamingOverlay", new[] { "XboxGamingOverlay" }, "Game Bar"),
            new("xboxlive", "Xbox Live In-Game Experience", "Microsoft.Xbox.TCUI", new[] { "Xbox" }, "Xbox Live overlay"),
            new("familysafety", "Microsoft Family Safety", "Microsoft.Family", new[] { "Family" }, "Family Safety"),
            new("paint3d", "Paint 3D", "Microsoft.MSPaint", new[] { "MSPaint" }, "Paint 3D")
        };
    }
}

