using System.IO;
using Microsoft.Win32;
using WinManager.Common;
using WinManager.Helpers;
using WinManager.Models;
using WinManager.Models.Config;
using WinManager.Services;
using WinManager.Views;

namespace WinManager.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService = new();
    private bool _showWelcome = true;
    private string _configStatus = string.Empty;
    private AppSection _selectedSection;

    public MainViewModel()
    {
        Programs      = new ProgramsViewModel();
        Optimization  = new OptimizationViewModel();
        Customization = new CustomizationViewModel();
        Features      = new FeaturesViewModel();
        Misc          = new MiscViewModel();
        SelectedSection = AppSection.Programs;

        SelectSectionCommand = new RelayCommand<AppSection>(s => SelectedSection = s);
        GetStartedCommand    = new RelayCommand(OnGetStarted);
        SaveConfigCommand    = new AsyncRelayCommand(SaveConfigAsync);
        LoadConfigCommand    = new AsyncRelayCommand(LoadConfigAsync);
    }

    public ProgramsViewModel     Programs      { get; }
    public OptimizationViewModel Optimization  { get; }
    public CustomizationViewModel Customization { get; }
    public FeaturesViewModel     Features      { get; }
    public MiscViewModel         Misc          { get; }

    public RelayCommand<AppSection> SelectSectionCommand { get; }
    public RelayCommand             GetStartedCommand    { get; }
    public AsyncRelayCommand        SaveConfigCommand    { get; }
    public AsyncRelayCommand        LoadConfigCommand    { get; }

    public bool ShowWelcome
    {
        get => _showWelcome;
        private set => SetProperty(ref _showWelcome, value);
    }

    public string ConfigStatus
    {
        get => _configStatus;
        private set => SetProperty(ref _configStatus, value);
    }

    public AppSection SelectedSection
    {
        get => _selectedSection;
        set => SetProperty(ref _selectedSection, value);
    }

    public string WindowsVersionDisplay => WindowsVersion.DisplayName;

    public string OptimizationMessage  => "Coming soon: system optimization tweaks.";
    public string CustomizationMessage => "Coming soon: personalization and UI tweaks.";

    private void OnGetStarted()
    {
        ShowWelcome = false;
        SelectedSection = AppSection.Programs;
    }

    private async Task SaveConfigAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title            = "Save WinManager Config",
            Filter           = "JSON config (*.json)|*.json",
            InitialDirectory = ConfigService.DefaultDirectory,
            FileName         = "winmanager-config.json"
        };

        Directory.CreateDirectory(ConfigService.DefaultDirectory);

        if (dialog.ShowDialog() != true)
            return;

        var config = SnapshotConfig();
        await _configService.SaveAsync(config, dialog.FileName);
        ConfigStatus = $"Saved to {Path.GetFileName(dialog.FileName)}";
    }

    private async Task LoadConfigAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title            = "Load WinManager Config",
            Filter           = "JSON config (*.json)|*.json",
            InitialDirectory = ConfigService.DefaultDirectory
        };

        if (dialog.ShowDialog() != true)
            return;

        var config = await _configService.LoadAsync(dialog.FileName);
        if (config is null)
        {
            ConfigStatus = "Failed to load config";
            return;
        }

        var progressWindow = new ConfigLoadProgressWindow();
        var progressVm = new ConfigLoadProgressViewModel();
        progressWindow.DataContext = progressVm;
        progressWindow.Owner = System.Windows.Application.Current.MainWindow;
        progressWindow.Show();

        try
        {
            await ApplyConfigAsync(config, progressVm);
        }
        finally
        {
            progressWindow.Close();
        }

        ShowWelcome = false;
        SelectedSection = AppSection.Programs;
        ConfigStatus = $"Loaded {Path.GetFileName(dialog.FileName)}";
    }

    private WinManagerConfig SnapshotConfig()
    {
        var cfg = new WinManagerConfig { SavedAt = DateTime.Now };

        foreach (var item in Features.EnableItems)
            cfg.WindowsFeatures[item.WindowsFeatureName] = item.IsEnabled;

        foreach (var app in Programs.ExternalApps.Where(a => a.IsSelected))
            cfg.SelectedExternalApps.Add(app.Id);

        var priv = Optimization.PrivacyVm;
        foreach (var item in priv.ToggleGroups.SelectMany(g => g.Items))
            cfg.Privacy[item.Name] = item.IsEnabled;
        cfg.UacLevelName    = priv.SelectedUacOption?.Level.ToString();
        cfg.DnsProviderName = priv.SelectedDnsProvider?.Name;

        foreach (var item in Optimization.GamingVm.ToggleGroups.SelectMany(g => g.Items))
            cfg.Gaming[item.Name] = item.IsEnabled;

        foreach (var item in Optimization.UpdateVm.AllItems)
            cfg.Updates[item.Name] = item.IsEnabled;

        foreach (var item in Optimization.NotificationsVm.ToggleGroups.SelectMany(g => g.Items))
            cfg.Notifications[item.Name] = item.IsEnabled;

        foreach (var item in Optimization.SoundVm.ToggleGroups.SelectMany(g => g.Items))
            cfg.Sound[item.Name] = item.IsEnabled;

        var pwr = Optimization.PowerVm;
        cfg.PowerPlanGuid    = pwr.SelectedPlan?.Guid;
        cfg.HibernateEnabled = pwr.HibernateEnabled;
        foreach (var s in pwr.Settings)
            cfg.PowerSettings[s.Name] = new PowerSettingConfig
                { AcValue = s.AcValue, DcValue = s.DcValue };

        return cfg;
    }

    // Builds a flat list of (label, async action) for every setting whose
    // current state differs from the loaded config, then runs them serially
    // through the progress view-model. We call the underlying services
    // directly instead of going through the toggle commands, so each step is
    // awaitable and can't race against the next one.
    //
    // Side effects normally triggered by sub-VM OnToggle handlers (Explorer
    // restarts, secondary policy writes, service restarts) are intentionally
    // skipped during bulk apply — they would compound and slow the whole
    // pass. The user can toggle individual items afterwards if they need the
    // live-apply behaviour.
    private async Task ApplyConfigAsync(WinManagerConfig cfg, ConfigLoadProgressViewModel progressVm)
    {
        var privacyService = new PrivacySettingsService();
        var powerService = new PowerService();
        var runner = new ProcessRunner();
        var steps = new List<(string label, Func<Task> action)>();

        foreach (var app in Programs.ExternalApps)
            app.IsSelected = cfg.SelectedExternalApps.Contains(app.Id);

        var privVm = Optimization.PrivacyVm;
        foreach (var item in privVm.ToggleGroups.SelectMany(g => g.Items))
        {
            if (cfg.Privacy.TryGetValue(item.Name, out var target) && item.IsEnabled != target)
                steps.Add((item.Name, async () =>
                {
                    await privacyService.SetStateAsync(item, target);
                    await privacyService.ReadStateAsync(item);
                }));
        }

        if (cfg.UacLevelName is not null)
        {
            var match = privVm.UacOptions.FirstOrDefault(u => u.Level.ToString() == cfg.UacLevelName);
            if (match is not null && privVm.SelectedUacOption?.Level != match.Level)
                steps.Add(($"UAC: {match.Name}", () =>
                {
                    privVm.SelectedUacOption = match;
                    return Task.CompletedTask;
                }));
        }

        if (cfg.DnsProviderName is not null)
        {
            var match = privVm.DnsProviders.FirstOrDefault(d => d.Name == cfg.DnsProviderName);
            if (match is not null && privVm.SelectedDnsProvider?.Name != match.Name)
                steps.Add(($"DNS: {match.Name}", async () =>
                {
                    await privacyService.SetDnsProviderAsync(match);
                }));
        }

        foreach (var item in Optimization.GamingVm.ToggleGroups.SelectMany(g => g.Items))
        {
            if (cfg.Gaming.TryGetValue(item.Name, out var target) && item.IsEnabled != target)
                steps.Add((item.Name, async () =>
                {
                    await privacyService.SetStateAsync(item, target);
                    await privacyService.ReadStateAsync(item);
                }));
        }

        foreach (var item in Optimization.UpdateVm.AllItems)
        {
            if (cfg.Updates.TryGetValue(item.Name, out var target) && item.IsEnabled != target)
                steps.Add((item.Name, async () =>
                {
                    await privacyService.SetStateAsync(item, target);
                    await privacyService.ReadStateAsync(item);
                }));
        }

        foreach (var item in Optimization.NotificationsVm.ToggleGroups.SelectMany(g => g.Items))
        {
            if (cfg.Notifications.TryGetValue(item.Name, out var target) && item.IsEnabled != target)
                steps.Add((item.Name, async () =>
                {
                    await privacyService.SetStateAsync(item, target);
                    await privacyService.ReadStateAsync(item);
                }));
        }

        foreach (var item in Optimization.SoundVm.ToggleGroups.SelectMany(g => g.Items))
        {
            if (cfg.Sound.TryGetValue(item.Name, out var target) && item.IsEnabled != target)
                steps.Add((item.Name, async () =>
                {
                    await privacyService.SetStateAsync(item, target);
                    await privacyService.ReadStateAsync(item);
                }));
        }

        foreach (var feature in Features.EnableItems)
        {
            if (feature.IsNotAvailable) continue;
            if (cfg.WindowsFeatures.TryGetValue(feature.WindowsFeatureName, out var target) &&
                feature.IsEnabled != target)
            {
                var command = target
                    ? $"Enable-WindowsOptionalFeature -Online -FeatureName '{feature.WindowsFeatureName}' -All -NoRestart"
                    : $"Disable-WindowsOptionalFeature -Online -FeatureName '{feature.WindowsFeatureName}' -NoRestart";
                steps.Add(($"Feature: {feature.Name}", async () =>
                {
                    await runner.RunAsync("powershell.exe",
                        $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                        timeout: TimeSpan.FromMinutes(10));
                    feature.IsEnabled = target;
                }));
            }
        }

        var pwr = Optimization.PowerVm;
        if (cfg.HibernateEnabled.HasValue && pwr.HibernateEnabled != cfg.HibernateEnabled.Value)
        {
            var target = cfg.HibernateEnabled.Value;
            steps.Add(($"Hibernate: {(target ? "enabled" : "disabled")}", async () =>
            {
                if (await powerService.IsHibernateSupportedAsync())
                {
                    await powerService.SetHibernateAsync(target);
                    pwr.HibernateEnabled = target;
                }
            }));
        }

        if (pwr.SelectedPlan is not null)
        {
            var planGuid = pwr.SelectedPlan.Guid;
            foreach (var s in pwr.Settings)
            {
                if (!cfg.PowerSettings.TryGetValue(s.Name, out var ps)) continue;

                if (s.AcValue != ps.AcValue)
                {
                    var ac = ps.AcValue;
                    var setting = s;
                    steps.Add(($"{s.Name} (AC)", async () =>
                    {
                        await powerService.SetValueAsync(planGuid, setting.SubGuid,
                            setting.SettingGuid, true, ac);
                        // LoadValue uses an internal suppression flag so it
                        // updates the UI without re-firing ValueChanged (which
                        // would call SetValueAsync a second time).
                        setting.LoadValue(ac, true);
                    }));
                }

                if (s.DcValue != ps.DcValue)
                {
                    var dc = ps.DcValue;
                    var setting = s;
                    steps.Add(($"{s.Name} (DC)", async () =>
                    {
                        await powerService.SetValueAsync(planGuid, setting.SubGuid,
                            setting.SettingGuid, false, dc);
                        setting.LoadValue(dc, false);
                    }));
                }
            }
        }

        await progressVm.RunAsync(steps);
    }
}
