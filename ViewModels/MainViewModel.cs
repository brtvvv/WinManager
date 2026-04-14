using System.IO;
using Microsoft.Win32;
using WinManager.Common;
using WinManager.Models;
using WinManager.Models.Config;
using WinManager.Services;

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

        ApplyConfig(config);
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

    private void ApplyConfig(WinManagerConfig cfg)
    {
        foreach (var app in Programs.ExternalApps)
            app.IsSelected = cfg.SelectedExternalApps.Contains(app.Id);

        var priv = Optimization.PrivacyVm;
        foreach (var item in priv.ToggleGroups.SelectMany(g => g.Items))
        {
            if (cfg.Privacy.TryGetValue(item.Name, out var target) && item.IsEnabled != target)
                priv.TogglePrivacyCommand.Execute(item);
        }
        if (cfg.UacLevelName is not null)
        {
            var match = priv.UacOptions.FirstOrDefault(u => u.Level.ToString() == cfg.UacLevelName);
            if (match is not null) priv.SelectedUacOption = match;
        }
        if (cfg.DnsProviderName is not null)
        {
            var match = priv.DnsProviders.FirstOrDefault(d => d.Name == cfg.DnsProviderName);
            if (match is not null) priv.SelectedDnsProvider = match;
        }

        foreach (var item in Optimization.GamingVm.ToggleGroups.SelectMany(g => g.Items))
        {
            if (cfg.Gaming.TryGetValue(item.Name, out var target) && item.IsEnabled != target)
                Optimization.GamingVm.ToggleCommand.Execute(item);
        }

        foreach (var item in Optimization.UpdateVm.AllItems)
        {
            if (cfg.Updates.TryGetValue(item.Name, out var target) && item.IsEnabled != target)
                Optimization.UpdateVm.ToggleCommand.Execute(item);
        }

        foreach (var item in Optimization.NotificationsVm.ToggleGroups.SelectMany(g => g.Items))
        {
            if (cfg.Notifications.TryGetValue(item.Name, out var target) && item.IsEnabled != target)
                Optimization.NotificationsVm.ToggleCommand.Execute(item);
        }

        foreach (var item in Optimization.SoundVm.ToggleGroups.SelectMany(g => g.Items))
        {
            if (cfg.Sound.TryGetValue(item.Name, out var target) && item.IsEnabled != target)
                Optimization.SoundVm.ToggleCommand.Execute(item);
        }

        var pwr = Optimization.PowerVm;
        if (cfg.HibernateEnabled.HasValue && pwr.HibernateEnabled != cfg.HibernateEnabled.Value)
            pwr.ToggleHibernateCommand.Execute(null);
        foreach (var s in pwr.Settings)
        {
            if (cfg.PowerSettings.TryGetValue(s.Name, out var ps))
            {
                s.AcValue = ps.AcValue;
                s.DcValue = ps.DcValue;
                s.ApplyAcCommand.Execute(null);
                s.ApplyDcCommand.Execute(null);
            }
        }
    }
}
