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

        _backgroundAppsItem = new("Background Apps",
            "Allows apps to receive info, send notifications and stay up to date even when not in use",
            "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
            "GlobalUserDisabled", 0, 1);

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

                new("Search Entire Filesystem",
                    "Enables Enhanced indexing mode \u2014 Windows Search indexes the entire drive for faster results",
                    "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\SearchSettings",
                    "IsEnhancedSearchInterfaceEnabled", 1, 0) { DefaultIsEnabled = false },
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

    private async Task ApplyMouseAccelLiveAsync(bool enable)
    {
        var speed = enable ? 1 : 0;
        var t1 = enable ? 6 : 0;
        var t2 = enable ? 10 : 0;
        var script =
            "Add-Type -TypeDefinition @'" +
            "using System; using System.Runtime.InteropServices; " +
            "public class M { " +
            "[DllImport(\"user32.dll\")] public static extern bool SystemParametersInfo(uint u, uint p, int[] v, uint f); }" +
            "'@ -EA SilentlyContinue; " +
            $"[M]::SystemParametersInfo(0x0004, 0, [int[]]@({t1},{t2},{speed}), 0x03)";
        await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"");
    }

    private async Task SetBackgroundAppToggle(bool enable)
    {
        var val = enable ? 1 : 0;
        var hp = @"HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search";
        await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"New-Item -Path '{hp}' -Force -EA SilentlyContinue | Out-Null; Set-ItemProperty -Path '{hp}' -Name 'BackgroundAppGlobalToggle' -Value {val} -Type DWord -Force\"");
    }
}
