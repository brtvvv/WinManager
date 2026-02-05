using System.Security.Principal;
using WinManager.Models;

namespace WinManager.Services;

/// <summary>
/// Handles Windows (inbox) apps: status detection, winget/appx install/uninstall, definitions list.
/// Uses PowerShellAppxService for real Get-AppxPackage -AllUsers data.
/// </summary>
public class WindowsAppService
{
    private readonly ProcessRunner _runner = new();
    private readonly PowerShellAppxService _appxService = new();

    public bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>Builds app definitions and assigns current status from Get-AppxPackage -AllUsers.</summary>
    public async Task<IReadOnlyList<SoftwareItem>> GetWindowsAppsAsync(CancellationToken cancellationToken = default)
    {
        var definitions = CreateDefinitions();
        var installedList = await _appxService.GetInstalledAppxAllUsersAsync(cancellationToken);
        MapStatusAndPackageNames(definitions, installedList);
        return definitions;
    }

    /// <summary>Re-fetches installed Appx list and updates status + InstalledPackageNames for all given apps.</summary>
    public async Task RefreshStatusAsync(IEnumerable<SoftwareItem> apps, CancellationToken cancellationToken = default)
    {
        var installedList = await _appxService.GetInstalledAppxAllUsersAsync(cancellationToken);
        MapStatusAndPackageNames(apps.ToList(), installedList);
    }

    /// <summary>Maps installed Appx list to app status and InstalledPackageNames; updates StatusNote.</summary>
    private static void MapStatusAndPackageNames(IList<SoftwareItem> apps, List<AppxInstalledItem> installedList)
    {
        var installedByName = installedList
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var app in apps)
        {
            app.InstalledPackageNames.Clear();
            var matchedNames = new List<string>();

            foreach (var keyword in app.DetectionKeywords)
            {
                foreach (var kv in installedByName)
                {
                    if (kv.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!matchedNames.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                        {
                            matchedNames.Add(kv.Key);
                        }
                    }
                }
            }

            if (matchedNames.Count > 0)
            {
                app.Status = AppStatus.Installed;
                app.InstalledPackageNames.AddRange(matchedNames);
                app.StatusNote = "Installed";
            }
            else if (!string.IsNullOrWhiteSpace(app.WingetId))
            {
                var wingetInstalled = installedList.Any(x =>
                    x.Name.Equals(app.WingetId, StringComparison.OrdinalIgnoreCase));
                if (wingetInstalled)
                {
                    app.Status = AppStatus.Installed;
                    app.InstalledPackageNames.Add(app.WingetId!);
                    app.StatusNote = "Installed";
                }
                else
                {
                    app.Status = AppStatus.NotInstalled;
                    app.StatusNote = "Not installed";
                }
            }
            else
            {
                app.Status = AppStatus.NotInstalled;
                app.StatusNote = "Not installed";
            }
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

    /// <summary>Uninstall selected apps: only Installed; uses Get-AppxPackage -AllUsers &lt;Name&gt; | Remove-AppxPackage -AllUsers.</summary>
    public async Task UninstallAsync(IEnumerable<SoftwareItem> apps, Action<string> log, CancellationToken cancellationToken = default)
    {
        var toUninstall = apps.Where(a => a.Status == AppStatus.Installed && a.InstalledPackageNames.Count > 0).ToList();
        var total = toUninstall.Count;
        var index = 0;

        foreach (var app in toUninstall)
        {
            app.IsBusy = true;
            index++;
            var currentOp = $"Uninstalling {index}/{total}: {app.Name}";
            log($"[Uninstall] {currentOp}");

            foreach (var packageName in app.InstalledPackageNames)
            {
                if (string.IsNullOrWhiteSpace(packageName))
                {
                    continue;
                }

                // Get-AppxPackage -AllUsers <PackageName> | Remove-AppxPackage -AllUsers (single quotes avoid -Command escaping)
                var safeName = packageName.Replace("'", "''");
                var ps = $"Get-AppxPackage -AllUsers -Name '{safeName}' | Remove-AppxPackage -AllUsers";
                var result = await _runner.RunAsync("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -Command \"{ps}\"",
                    cancellationToken);

                app.StatusNote = result.Output?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(result.Output))
                {
                    log(result.Output);
                }
                if (!result.Success && result.ExitCode != 0)
                {
                    log($"Error (exit {result.ExitCode}): {app.Name} - {result.Output?.Trim() ?? "Unknown error"}");
                }
            }

            app.IsBusy = false;
        }

        if (toUninstall.Count == 0)
        {
            foreach (var app in apps)
            {
                if (app.Status != AppStatus.Installed)
                {
                    log($"[Uninstall] Skipped (not installed): {app.Name}");
                }
            }
        }
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

