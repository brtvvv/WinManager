using WinManager.Common;
using WinManager.Models;
using WinManager.Services;

namespace WinManager.ViewModels.Customization;

public class StartMenuViewModel : CustomizationCategoryViewModelBase
{
    private readonly PrivacySettingsService _service = new();
    private readonly ProcessRunner _runner = new();
    private string _statusMessage = string.Empty;
    private bool _showStatus;

    private readonly PrivacyToggleItem _bingSearch;

    public StartMenuViewModel() : base("Start Menu")
    {
        _bingSearch = new("Disable Bing Search in Start Menu",
            "Prevents Start Menu search from showing web results powered by Bing",
            "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search",
            "BingSearchEnabled", 1, 0);

        LayoutToggles = new List<PrivacyToggleItem>
        {
            new("Recommended Section",
                "Show the Recommended section in the Start Menu with recent files and suggestions",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\Explorer",
                "HideRecommendedSection", 0, 1),
        };

        BehaviorToggles = new List<PrivacyToggleItem>
        {
            new("Show Recently Added Apps",
                "Display newly installed applications at the top of the Start Menu app list",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Start",
                "ShowRecentList", 1, 0),

            new("Show Most Used Apps",
                "Display your most frequently launched apps in the Start Menu",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Start",
                "ShowFrequentList", 1, 0),

            _bingSearch,
        };

        ToggleCommand = new RelayCommand<PrivacyToggleItem>(OnToggle);
        CleanStartMenuCommand = new RelayCommand(OnCleanStartMenu);
        DismissStatusCommand = new RelayCommand(() => ShowStatus = false);

        _ = InitializeAsync();
    }

    public IReadOnlyList<PrivacyToggleItem> LayoutToggles { get; }
    public IReadOnlyList<PrivacyToggleItem> BehaviorToggles { get; }
    public RelayCommand<PrivacyToggleItem> ToggleCommand { get; }
    public RelayCommand CleanStartMenuCommand { get; }
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

    private async Task InitializeAsync()
    {
        var all = LayoutToggles.Concat(BehaviorToggles).ToList();
        await _service.ReadAllStatesAsync(all);
    }

    // ── Toggles ──────────────────────────────────────────────────

    private async void OnToggle(PrivacyToggleItem? item)
    {
        if (item is null) return;

        var target = !item.IsEnabled;
        StatusMessage = $"{(target ? "Enabling" : "Disabling")}: {item.Name}...";
        ShowStatus = true;

        var success = await _service.SetStateAsync(item, target);

        if (success && item == _bingSearch)
            await SetBingSearchSecondaryAsync(target);

        if (success)
        {
            await _service.ReadStateAsync(item);
            item.IsChecking = false;
            StatusMessage = $"{item.Name} \u2014 {item.StatusText.ToLowerInvariant()} successfully.";
        }
        else
        {
            StatusMessage = $"{item.Name} \u2014 failed. Run as administrator.";
        }
    }

    private async Task SetBingSearchSecondaryAsync(bool bingEnabled)
    {
        var val = bingEnabled ? 0 : 1;
        var hp = @"HKCU:\SOFTWARE\Policies\Microsoft\Windows\Explorer";
        await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"New-Item -Path '{hp}' -Force -EA SilentlyContinue | Out-Null; Set-ItemProperty -Path '{hp}' -Name 'DisableSearchBoxSuggestions' -Value {val} -Type DWord -Force\"");
    }

    // ── Clean Start Menu ─────────────────────────────────────────

    private async void OnCleanStartMenu()
    {
        var result = System.Windows.MessageBox.Show(
            "This will remove all pinned apps from the Start Menu. Continue?",
            "Clean Start Menu",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        StatusMessage = "Cleaning Start Menu...";
        ShowStatus = true;

        var success = await RunPsSuccessAsync(
            "$p = Join-Path $env:LocalAppData 'Packages\\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\\LocalState'; " +
            "Remove-Item (Join-Path $p 'start2.bin') -Force -EA SilentlyContinue; " +
            "Stop-Process -Name StartMenuExperienceHost -Force -EA SilentlyContinue; " +
            "Start-Sleep -Milliseconds 800; " +
            "Start-Process StartMenuExperienceHost -EA SilentlyContinue");

        StatusMessage = success
            ? "Start Menu cleaned successfully."
            : "Start Menu clean failed.";
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<bool> RunPsSuccessAsync(string command)
    {
        var r = await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"");
        return r.Success;
    }
}
