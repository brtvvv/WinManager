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

    public GamingPerformanceViewModel() : base("Gaming & Performance")
    {
        _mouseAccelItem = new("Enhance Pointer Precision",
            "Applies mouse acceleration \u2014 improves casual use but reduces consistency for gaming",
            "HKCU", @"Control Panel\Mouse",
            "MouseSpeed", "1", "0") { IsStringValue = true };

        // HKLM AppPrivacy policy is the authoritative key Windows reads to gate
        // background apps globally — 1 = allow, 2 = deny. The per-user
        // BackgroundAccessApplications value is ignored on modern builds.
        _backgroundAppsItem = new("Background Apps",
            "Allows apps to receive info, send notifications and stay up to date even when not in use",
            "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
            "LetAppsRunInBackground", 1, 2);

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

                // IsEnhancedSearchInterfaceEnabled alone doesn't switch indexing
                // scope. Mirror IsEnhancedSearchEnabled in the same key — the
                // value Windows actually checks to enable enhanced indexing.
                new("Search Entire Filesystem",
                    "Enables Enhanced indexing mode \u2014 Windows Search indexes the entire drive for faster results",
                    "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\SearchSettings",
                    "IsEnhancedSearchInterfaceEnabled", 1, 0)
                {
                    DefaultIsEnabled = false,
                    ExtraValueNames = new[] { "IsEnhancedSearchEnabled" }
                },
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
            else if (item == _backgroundAppsItem)
                await SetBackgroundAppToggle(target);

            await _service.ReadStateAsync(item);
            item.IsChecking = false;
            StatusMessage = $"{item.Name} — {item.StatusText.ToLowerInvariant()} successfully.";
        }
        else
        {
            StatusMessage = $"{item.Name} — failed. Run as administrator.";
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

    // SPI_SETMOUSE through P/Invoke with int[] is fragile (the API actually
    // expects a pointer to a 3-element array, not a managed int[]). Use the
    // same approach Control Panel uses: UpdatePerUserSystemParameters forces
    // Windows to re-read all Control Panel\Mouse values we just wrote.
    private async Task ApplyMouseAccelLiveAsync(bool _)
    {
        await _runner.RunAsync("rundll32.exe", "user32.dll,UpdatePerUserSystemParameters");
    }

    private async Task SetBackgroundAppToggle(bool enable)
    {
        // HKLM policy LetAppsRunInBackground — 1 = allow, 2 = deny.
        var val = enable ? 1 : 2;
        var hp = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy";
        await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"New-Item -Path '{hp}' -Force -EA SilentlyContinue | Out-Null; Set-ItemProperty -Path '{hp}' -Name 'LetAppsRunInBackground' -Value {val} -Type DWord -Force\"");
    }
}
