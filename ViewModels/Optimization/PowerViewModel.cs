using System.Collections.ObjectModel;
using WinManager.Common;
using WinManager.Models;
using WinManager.Services;

namespace WinManager.ViewModels.Optimization;

public class PowerViewModel : OptimizationCategoryViewModelBase
{
    private readonly PowerService _powerService = new();
    private PowerPlan? _selectedPlan;
    private bool _suppressPlanChange;
    private bool _hibernateEnabled;
    private bool _isLaptop;
    private string _statusMessage = string.Empty;
    private bool _showStatus;

    private readonly PowerSettingItem _hibernateTimeout;
    private readonly PowerSettingItem _lidClose;

    public PowerViewModel() : base("Power")
    {
        var timeOpts = BuildTimeOptions();
        var wirelessOpts = BuildWirelessOptions();
        var buttonOpts = BuildButtonOptions();

        _hibernateTimeout = new("Hibernate After",
            "Save session to disk after a period of inactivity",
            "238c9fa8-0aad-41ed-83f4-97be242c8f20",
            "9d7815a6-7ee4-497e-8888-515a05f02364",
            options: timeOpts) { IsVisible = false };

        _lidClose = new("Lid Close",
            "Define what happens when the laptop lid is closed",
            "4f971e89-eebd-4455-a8de-9e59040e7347",
            "5ca83367-6e45-459f-a27b-476b1d01c936",
            options: buttonOpts) { IsVisible = false };

        Settings = new List<PowerSettingItem>
        {
            new("Display",
                "Turn off the display after a period of inactivity",
                "7516b95f-f776-4464-8c53-06167f40cc99",
                "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e",
                options: timeOpts),

            new("Hard Disk",
                "Turn off the hard disk after a period of inactivity",
                "0012ee47-9041-4b5d-9b77-535fba8b1442",
                "6738e2c4-e8a5-4a42-b16a-e040e769756e",
                options: timeOpts),

            new("Wireless Adapter",
                "Power saving mode for the wireless adapter",
                "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1",
                "12bbebe6-58d6-4636-95bb-3217ef867c1a",
                options: wirelessOpts),

            new("Sleep",
                "Put the computer to sleep after a period of inactivity",
                "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                "29f6c1db-86da-48c5-9fdb-f2de3304d29f",
                options: timeOpts),

            _hibernateTimeout,

            new("Power Button",
                "Define what happens when the power button is pressed",
                "4f971e89-eebd-4455-a8de-9e59040e7347",
                "7648efa3-dd9c-4e3e-b566-50f929386280",
                options: buttonOpts),

            _lidClose,

            new("Minimum Processor State",
                "Minimum percentage of processor frequency",
                "54533251-82be-4824-96c1-47b60b740d00",
                "893dee8e-2bef-41e0-89c6-b55d0929964c",
                isSlider: true),

            new("Maximum Processor State",
                "Maximum percentage of processor frequency",
                "54533251-82be-4824-96c1-47b60b740d00",
                "bc5038f7-23e0-4960-96da-33abaf5935ec",
                isSlider: true),
        };

        foreach (var s in Settings)
            s.ValueChanged = OnSettingValueChanged;

        ToggleHibernateCommand = new RelayCommand(() => _ = ToggleHibernateAsync());
        DismissStatusCommand = new RelayCommand(() => ShowStatus = false);

        _ = InitializeAsync();
    }

    // ── Plan selector ────────────────────────────────────────

    public ObservableCollection<PowerPlan> Plans { get; } = new();

    public PowerPlan? SelectedPlan
    {
        get => _selectedPlan;
        set
        {
            if (!SetProperty(ref _selectedPlan, value)) return;
            if (_suppressPlanChange || value is null) return;
            _ = SwitchPlanAsync(value);
        }
    }

    // ── Settings ─────────────────────────────────────────────

    public IReadOnlyList<PowerSettingItem> Settings { get; }

    // ── Hibernate ────────────────────────────────────────────

    public bool HibernateEnabled
    {
        get => _hibernateEnabled;
        set
        {
            if (SetProperty(ref _hibernateEnabled, value))
            {
                Notify(nameof(HibernateButtonLabel));
                Notify(nameof(HibernateStatusText));
                _hibernateTimeout.IsVisible = value;
            }
        }
    }

    public string HibernateButtonLabel => HibernateEnabled ? "Disable" : "Enable";
    public string HibernateStatusText => HibernateEnabled ? "Enabled" : "Disabled";
    public RelayCommand ToggleHibernateCommand { get; }

    // ── Laptop detection ─────────────────────────────────────

    public bool IsLaptop
    {
        get => _isLaptop;
        set
        {
            if (SetProperty(ref _isLaptop, value))
                _lidClose.IsVisible = value;
        }
    }

    // ── Status ───────────────────────────────────────────────

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

    // ── Initialization ───────────────────────────────────────

    private async Task InitializeAsync()
    {
        try
        {
            var plans = await _powerService.GetPlansAsync();

            var recGuid = await _powerService.EnsureRecommendedPlanAsync(plans);
            if (recGuid != null &&
                !plans.Any(p => p.Guid.Equals(recGuid, StringComparison.OrdinalIgnoreCase)))
            {
                plans = await _powerService.GetPlansAsync();
            }

            _suppressPlanChange = true;
            Plans.Clear();
            foreach (var p in plans) Plans.Add(p);
            SelectedPlan = Plans.FirstOrDefault(p => p.IsActive) ?? Plans.FirstOrDefault();
            _suppressPlanChange = false;

            if (SelectedPlan != null)
            {
                await LoadAllSettingsAsync(SelectedPlan.Guid);
                IsLaptop = await _powerService.IsLaptopAsync(SelectedPlan.Guid);
            }

            HibernateEnabled = await _powerService.IsHibernateEnabledAsync();
        }
        catch
        {
            StatusMessage = "Failed to load power settings.";
            ShowStatus = true;
        }
    }

    // ── Plan switching ───────────────────────────────────────

    private async Task SwitchPlanAsync(PowerPlan plan)
    {
        StatusMessage = $"Switching to {plan.Name}...";
        ShowStatus = true;

        var success = await _powerService.SetActivePlanAsync(plan.Guid);
        if (success)
        {
            await LoadAllSettingsAsync(plan.Guid);
            StatusMessage = $"Active plan: {plan.Name}";
        }
        else
        {
            StatusMessage = "Failed to switch plan. Run as administrator.";
        }
    }

    // ── Setting I/O ──────────────────────────────────────────

    private async Task LoadAllSettingsAsync(string planGuid)
    {
        var tasks = Settings
            .Where(s => s.IsVisible || s == _hibernateTimeout || s == _lidClose)
            .Select(async s =>
            {
                try
                {
                    var (ac, dc) = await _powerService.QuerySettingAsync(
                        planGuid, s.SubGuid, s.SettingGuid);
                    s.LoadValue(ac, true);
                    s.LoadValue(dc, false);
                }
                catch { /* setting may not exist on this hardware */ }
            });
        await Task.WhenAll(tasks);
    }

    private async void OnSettingValueChanged(PowerSettingItem item, bool isAc)
    {
        if (SelectedPlan is null) return;

        var value = item.GetValue(isAc);
        var source = isAc ? "AC" : "DC";

        var success = await _powerService.SetValueAsync(
            SelectedPlan.Guid, item.SubGuid, item.SettingGuid, isAc, value);

        StatusMessage = success
            ? $"{item.Name} ({source}) updated."
            : $"{item.Name} — failed. Run as administrator.";
        ShowStatus = true;
    }

    // ── Hibernate toggle ─────────────────────────────────────

    private async Task ToggleHibernateAsync()
    {
        var target = !HibernateEnabled;
        StatusMessage = target ? "Enabling hibernation..." : "Disabling hibernation...";
        ShowStatus = true;

        var success = await _powerService.SetHibernateAsync(target);
        if (success)
        {
            HibernateEnabled = target;
            StatusMessage = $"Hibernation {(target ? "enabled" : "disabled")}.";

            if (target && SelectedPlan != null)
            {
                try
                {
                    var (ac, dc) = await _powerService.QuerySettingAsync(
                        SelectedPlan.Guid,
                        _hibernateTimeout.SubGuid,
                        _hibernateTimeout.SettingGuid);
                    _hibernateTimeout.LoadValue(ac, true);
                    _hibernateTimeout.LoadValue(dc, false);
                }
                catch { /* hibernate timeout query may fail */ }
            }
        }
        else
        {
            StatusMessage = "Failed to change hibernation. Run as administrator.";
        }
    }

    // ── Static option lists ──────────────────────────────────

    private static IReadOnlyList<PowerSettingOption> BuildTimeOptions() => new List<PowerSettingOption>
    {
        new("Never", 0),
        new("1 min", 60),
        new("2 min", 120),
        new("3 min", 180),
        new("5 min", 300),
        new("10 min", 600),
        new("15 min", 900),
        new("20 min", 1200),
        new("30 min", 1800),
        new("45 min", 2700),
        new("1 hour", 3600),
        new("2 hours", 7200),
        new("5 hours", 18000),
    };

    private static IReadOnlyList<PowerSettingOption> BuildWirelessOptions() => new List<PowerSettingOption>
    {
        new("Maximum Performance", 0),
        new("Minimum Power Saving", 1),
        new("Medium Power Saving", 2),
        new("Maximum Power Saving", 3),
    };

    private static IReadOnlyList<PowerSettingOption> BuildButtonOptions() => new List<PowerSettingOption>
    {
        new("Do nothing", 0),
        new("Sleep", 1),
        new("Hibernate", 2),
        new("Shut down", 3),
        new("Turn off the display", 4),
    };
}
