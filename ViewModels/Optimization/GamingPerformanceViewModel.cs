using System.Runtime.InteropServices;
using WinManager.Common;
using WinManager.Models;
using WinManager.Services;

namespace WinManager.ViewModels.Optimization;

public class GamingPerformanceViewModel : OptimizationCategoryViewModelBase
{
    private readonly PrivacySettingsService _service = new();
    private readonly ProcessRunner _runner = new();
    private string _statusMessage = string.Empty;
    private bool _showStatus;

    private readonly PrivacyToggleItem _mouseAccelItem;
    private readonly PrivacyToggleItem _backgroundAppsItem;
    private readonly PrivacyToggleItem _enhancedSearchItem;

    public GamingPerformanceViewModel() : base("Gaming & Performance")
    {
        _mouseAccelItem = new("Enhance Pointer Precision",
            "Applies mouse acceleration \u2014 improves casual use but reduces consistency for gaming",
            "HKCU", @"Control Panel\Mouse",
            "MouseSpeed", "1", "0") { IsStringValue = true };

        // HKLM AppPrivacy policy: 1 = allow, 2 = deny. On a fresh system the
        // value isn't written yet — Windows default is "allow", so seed the
        // toggle as enabled so the first click correctly disables.
        _backgroundAppsItem = new("Background Apps",
            "Allows apps to receive info, send notifications and stay up to date even when not in use",
            "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
            "LetAppsRunInBackground", 1, 2)
        { DefaultIsEnabled = true };

        // The HKCU SearchSettings flags only update the Settings UI display.
        // The key Windows actually reads to switch to "Find My Files" indexing
        // is HKLM\SOFTWARE\Microsoft\Windows Search\EnableFindMyFiles, and the
        // WSearch service has to be restarted to pick it up.
        _enhancedSearchItem = new("Search Entire Filesystem",
            "Enables Enhanced \"Find My Files\" indexing \u2014 Windows Search indexes the entire drive for faster results",
            "HKLM", @"SOFTWARE\Microsoft\Windows Search",
            "EnableFindMyFiles", 1, 0)
        { DefaultIsEnabled = false };

        ToggleGroups = new List<PrivacyToggleGroup>
        {
            new("Gaming", new List<PrivacyToggleItem>
            {
                new("Game Mode",
                    "Prioritizes CPU and GPU resources for games, reduces background activity during gameplay",
                    "HKCU", @"SOFTWARE\Microsoft\GameBar",
                    "AutoGameModeEnabled", 1, 0),

                _mouseAccelItem,
            }),

            new("Performance", new List<PrivacyToggleItem>
            {
                _backgroundAppsItem,

                new("Storage Sense",
                    "Automatically frees up space by removing temporary files and content in the recycle bin",
                    "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy",
                    "01", 1, 0) { DefaultIsEnabled = false },

                _enhancedSearchItem,
            }),
        };

        ToggleCommand = new RelayCommand<PrivacyToggleItem>(OnToggle);
        DismissStatusCommand = new RelayCommand(() => ShowStatus = false);

        _ = LoadStatesAsync();
    }

    public IReadOnlyList<PrivacyToggleGroup> ToggleGroups { get; }
    public RelayCommand<PrivacyToggleItem> ToggleCommand { get; }
    public RelayCommand DismissStatusCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool ShowStatus
    {
        get => _showStatus;
        private set => SetProperty(ref _showStatus, value);
    }

    private async Task LoadStatesAsync()
    {
        var allItems = ToggleGroups.SelectMany(g => g.Items).ToList();
        await _service.ReadAllStatesAsync(allItems);
    }

    private async void OnToggle(PrivacyToggleItem? item)
    {
        if (item is null) return;

        var target = !item.IsEnabled;
        var action = target ? "Enabling" : "Disabling";
        StatusMessage = $"{action}: {item.Name}...";
        ShowStatus = true;

        var success = await _service.SetStateAsync(item, target);

        if (success)
        {
            if (item == _mouseAccelItem)
            {
                await SetMouseThresholds(target);
                await ApplyMouseAccelLiveAsync(target);
            }
            else if (item == _enhancedSearchItem)
            {
                // WSearch service caches EnableFindMyFiles in-memory; restart
                // it so the new indexing scope takes effect immediately.
                await _runner.RunAsync("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -Command \"Restart-Service WSearch -Force -EA SilentlyContinue\"");
            }

            await _service.ReadStateAsync(item);
            item.IsChecking = false;
            StatusMessage = $"{item.Name} \u2014 {item.StatusText.ToLowerInvariant()} successfully.";
        }
        else
        {
            StatusMessage = $"{item.Name} \u2014 failed. Run as administrator.";
        }
    }

    private async Task SetMouseThresholds(bool enable)
    {
        var t1 = enable ? "6" : "0";
        var t2 = enable ? "10" : "0";
        var hp = @"HKCU:\Control Panel\Mouse";
        await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"Set-ItemProperty -Path '{hp}' -Name 'MouseThreshold1' -Value '{t1}' -Type String -Force; Set-ItemProperty -Path '{hp}' -Name 'MouseThreshold2' -Value '{t2}' -Type String -Force\"");
    }

    // Run SystemParametersInfo in-process so there are no PowerShell quoting
    // pitfalls. SPIF_UPDATEINIFILE persists the change for new sessions and
    // SPIF_SENDCHANGE broadcasts WM_SETTINGCHANGE so the running session
    // picks it up without sign-out.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, int[] pvParam, uint fWinIni);

    private const uint SPI_SETMOUSE = 0x0004;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    private Task ApplyMouseAccelLiveAsync(bool enable)
    {
        var mouseParams = enable ? new[] { 6, 10, 1 } : new[] { 0, 0, 0 };
        SystemParametersInfo(SPI_SETMOUSE, 0, mouseParams, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        return Task.CompletedTask;
    }
}
