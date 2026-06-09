using System.Collections.ObjectModel;
using WinManager.Common;
using WinManager.Helpers;
using WinManager.Models;
using WinManager.Services;

namespace WinManager.ViewModels.Customization;

public class TaskbarViewModel : CustomizationCategoryViewModelBase
{
    private readonly PrivacySettingsService _service = new();
    private readonly ProcessRunner _runner = new();
    private string _statusMessage = string.Empty;
    private bool _showStatus;
    private bool _isBusy;
    private PowerSettingOption? _selectedSearchOption;
    private PowerSettingOption? _selectedAlignmentOption;
    private bool _suppressSearchChange;
    private bool _suppressAlignmentChange;

    public TaskbarViewModel() : base("Taskbar")
    {
        SearchOptions = new ObservableCollection<PowerSettingOption>
        {
            new("Hidden", 0),
            new("Icon only", 1),
            new("Search box", 2),
        };

        AlignmentOptions = new ObservableCollection<PowerSettingOption>
        {
            new("Left", 0),
            new("Center", 1),
        };

        var itemTogglesList = new List<PrivacyToggleItem>
        {
            new("Show Task View Button",
                "Display the Task View button on the taskbar for virtual desktops and timeline",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "ShowTaskViewButton", 1, 0),

            new("Show Widgets",
                "Display the Widgets button on the taskbar for news, weather and quick info",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "TaskbarDa", 1, 0),
        };

        if (WindowsVersion.IsAtLeast23H2)
        {
            itemTogglesList.Insert(1, new("Copilot Button",
                "Show or hide the Microsoft Copilot preview button on the taskbar",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "ShowCopilotButton", 1, 0));
        }

        ItemToggles = itemTogglesList;

        var behaviorTogglesList = new List<PrivacyToggleItem>();

        if (WindowsVersion.IsAtLeast23H2)
        {
            behaviorTogglesList.Add(new("Enable End Task in Taskbar",
                "Right-clicking a taskbar app shows an End Task option to force close it",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings",
                "TaskbarEndTask", 1, 0) { DefaultIsEnabled = false });
        }

        BehaviorToggles = behaviorTogglesList;

        ToggleCommand = new RelayCommand<PrivacyToggleItem>(OnToggle);
        CleanTaskbarCommand = new RelayCommand(OnCleanTaskbar);
        DismissStatusCommand = new RelayCommand(() => ShowStatus = false);

        _ = InitializeAsync();
    }

    public IReadOnlyList<PrivacyToggleItem> ItemToggles { get; }
    public IReadOnlyList<PrivacyToggleItem> BehaviorToggles { get; }
    public ObservableCollection<PowerSettingOption> SearchOptions { get; }
    public ObservableCollection<PowerSettingOption> AlignmentOptions { get; }
    public RelayCommand<PrivacyToggleItem> ToggleCommand { get; }
    public RelayCommand CleanTaskbarCommand { get; }
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

    public PowerSettingOption? SelectedSearchOption
    {
        get => _selectedSearchOption;
        set
        {
            var previous = _selectedSearchOption;
            if (!SetProperty(ref _selectedSearchOption, value)) return;
            if (_suppressSearchChange || value is null || previous is null) return;
            _ = ApplySearchAsync(value.Value);
        }
    }

    public PowerSettingOption? SelectedAlignmentOption
    {
        get => _selectedAlignmentOption;
        set
        {
            var previous = _selectedAlignmentOption;
            if (!SetProperty(ref _selectedAlignmentOption, value)) return;
            if (_suppressAlignmentChange || value is null || previous is null) return;
            _ = ApplyAlignmentAsync(value.Value);
        }
    }

    // ── Initialization ───────────────────────────────────────────

    private async Task InitializeAsync()
    {
        var allToggles = ItemToggles.Concat(BehaviorToggles).ToList();
        await Task.WhenAll(
            _service.ReadAllStatesAsync(allToggles),
            ReadSearchModeAsync(),
            ReadAlignmentAsync());
    }

    // ── Toggles ──────────────────────────────────────────────────

    private async void OnToggle(PrivacyToggleItem? item)
    {
        if (item is null || _isBusy) return;
        _isBusy = true;

        var target = !item.IsEnabled;
        StatusMessage = $"{(target ? "Enabling" : "Disabling")}: {item.Name}...";
        ShowStatus = true;

        await RestartExplorerAsync();
        var success = await _service.SetStateAsync(item, target);
        await StartExplorerAsync();

        if (success)
        {
            await _service.ReadStateAsync(item);
            item.IsChecking = false;
            StatusMessage = $"{item.Name} \u2014 {item.StatusText.ToLowerInvariant()} successfully.";
        }
        else
        {
            StatusMessage = $"{item.Name} \u2014 failed. Check permissions.";
        }

        _isBusy = false;
    }

    // ── Dropdowns ────────────────────────────────────────────────

    private async Task ReadSearchModeAsync()
    {
        var hp = @"HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search";
        var val = await ReadDwordAsync(hp, "SearchboxTaskbarMode", 1);
        _suppressSearchChange = true;
        SelectedSearchOption = SearchOptions.FirstOrDefault(o => o.Value == val) ?? SearchOptions[1];
        _suppressSearchChange = false;
    }

    private async Task ReadAlignmentAsync()
    {
        var hp = @"HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        var val = await ReadDwordAsync(hp, "TaskbarAl", 1);
        _suppressAlignmentChange = true;
        SelectedAlignmentOption = AlignmentOptions.FirstOrDefault(o => o.Value == val) ?? AlignmentOptions[1];
        _suppressAlignmentChange = false;
    }

    private async Task ApplySearchAsync(int value)
    {
        if (_isBusy) return;
        _isBusy = true;

        StatusMessage = "Applying: Search in Taskbar...";
        ShowStatus = true;

        await RestartExplorerAsync();

        var success = await RunPsSuccessAsync(
            $"New-Item -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Search' -Force -EA SilentlyContinue | Out-Null; " +
            $"Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Search' -Name 'SearchboxTaskbarMode' -Value {value} -Type DWord -Force");

        await StartExplorerAsync();

        StatusMessage = success
            ? "Search in Taskbar updated successfully."
            : "Search in Taskbar \u2014 failed. Check permissions.";

        _isBusy = false;
    }

    private async Task ApplyAlignmentAsync(int value)
    {
        if (_isBusy) return;
        _isBusy = true;

        StatusMessage = "Applying: Taskbar Alignment...";
        ShowStatus = true;

        await RestartExplorerAsync();

        var success = await RunPsSuccessAsync(
            $"New-Item -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Force -EA SilentlyContinue | Out-Null; " +
            $"Set-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'TaskbarAl' -Value {value} -Type DWord -Force");

        await StartExplorerAsync();

        StatusMessage = success
            ? "Taskbar Alignment updated successfully."
            : "Taskbar Alignment \u2014 failed. Check permissions.";

        _isBusy = false;
    }

    // ── Clean Taskbar ────────────────────────────────────────────

    private async void OnCleanTaskbar()
    {
        var result = System.Windows.MessageBox.Show(
            "This will remove all pinned apps from the taskbar and restart Explorer. Continue?",
            "Clean Taskbar",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        StatusMessage = "Cleaning taskbar...";
        ShowStatus = true;

        var success = await RunPsSuccessAsync(
            "$p = Join-Path $env:APPDATA 'Microsoft\\Internet Explorer\\Quick Launch\\User Pinned\\TaskBar'; " +
            "Get-ChildItem $p -Recurse -EA SilentlyContinue | Remove-Item -Recurse -Force -EA SilentlyContinue; " +
            "Remove-Item 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Taskband' -Recurse -Force -EA SilentlyContinue; " +
            "Stop-Process -Name explorer -Force; Start-Process explorer");

        StatusMessage = success
            ? "Taskbar cleaned successfully."
            : "Taskbar clean failed.";
    }

    // ── Explorer lifecycle ───────────────────────────────────────

    private async Task RestartExplorerAsync()
    {
        await RunPsSuccessAsync("Stop-Process -Name explorer -Force");
    }

    private async Task StartExplorerAsync()
    {
        await RunPsSuccessAsync("Start-Process explorer");
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<int> ReadDwordAsync(string hkPath, string name, int defaultValue)
    {
        try
        {
            var result = await RunPsAsync(
                $"try {{ (Get-ItemProperty -Path '{hkPath}' -Name '{name}' -EA Stop).'{name}' }} catch {{ 'NOTFOUND' }}");
            var output = result.Trim();
            if (output is "NOTFOUND" or "") return defaultValue;
            return int.TryParse(output, out var val) ? val : defaultValue;
        }
        catch { return defaultValue; }
    }

    private async Task<string> RunPsAsync(string command)
    {
        var result = await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"");
        return result.Output;
    }

    private async Task<bool> RunPsSuccessAsync(string command)
    {
        var result = await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"");
        return result.Success;
    }
}
