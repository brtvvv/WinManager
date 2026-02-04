using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using WinManager.Common;
using WinManager.Models;
using WinManager.Services;

namespace WinManager.ViewModels;

/// <summary>
/// Handles Programs & Features: loads Windows/External apps, filtering, selection, install/uninstall commands.
/// </summary>
public class ProgramsViewModel : ObservableObject
{
    private readonly WindowsAppService _windowsService;
    private readonly ExternalAppService _externalService;
    private readonly AsyncRelayCommand _installWindowsCommand;
    private readonly AsyncRelayCommand _uninstallWindowsCommand;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _installExternalCommand;
    private readonly AsyncRelayCommand _uninstallExternalCommand;
    private string _searchQuery = string.Empty;
    private string _log = string.Empty;
    private string _status = "Ready";
    private bool _isBusy;

    public ProgramsViewModel()
        : this(new WindowsAppService(), new ExternalAppService())
    {
    }

    public ProgramsViewModel(WindowsAppService windowsService, ExternalAppService externalService)
    {
        _windowsService = windowsService;
        _externalService = externalService;

        WindowsAppsView = CollectionViewSource.GetDefaultView(WindowsApps);
        WindowsAppsView.Filter = FilterWindows;

        ExternalAppsView = CollectionViewSource.GetDefaultView(ExternalApps);
        ExternalAppsView.Filter = FilterExternal;

        LoadCommand = new AsyncRelayCommand(InitializeAsync, () => !IsBusy);
        _refreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        _installWindowsCommand = new AsyncRelayCommand(InstallSelectedWindowsAsync, CanRunWindowsAction);
        _uninstallWindowsCommand = new AsyncRelayCommand(UninstallSelectedWindowsAsync, CanRunWindowsAction);
        _installExternalCommand = new AsyncRelayCommand(InstallSelectedExternalAsync, CanRunExternalAction);
        _uninstallExternalCommand = new AsyncRelayCommand(UninstallSelectedExternalAsync, CanRunExternalAction);
        SelectAllCommand = new RelayCommand(() => SelectAllWindows());
        SelectInstalledCommand = new RelayCommand(() => SelectWindowsByStatus(AppStatus.Installed));
        SelectNotInstalledCommand = new RelayCommand(() => SelectWindowsByStatus(AppStatus.NotInstalled));
        ClearSelectionCommand = new RelayCommand(() => ClearSelection());
        SelectAllExternalCommand = new RelayCommand(() => SelectAllExternal());
        ClearExternalCommand = new RelayCommand(() => ClearExternalSelection());
    }

    public ObservableCollection<SoftwareItem> WindowsApps { get; } = new();
    public ObservableCollection<SoftwareItem> ExternalApps { get; } = new();

    public ICollectionView WindowsAppsView { get; }
    public ICollectionView ExternalAppsView { get; }

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand RefreshCommand => _refreshCommand;
    public AsyncRelayCommand InstallWindowsCommand => _installWindowsCommand;
    public AsyncRelayCommand UninstallWindowsCommand => _uninstallWindowsCommand;
    public AsyncRelayCommand InstallExternalCommand => _installExternalCommand;
    public AsyncRelayCommand UninstallExternalCommand => _uninstallExternalCommand;
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectInstalledCommand { get; }
    public RelayCommand SelectNotInstalledCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }
    public RelayCommand SelectAllExternalCommand { get; }
    public RelayCommand ClearExternalCommand { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                WindowsAppsView.Refresh();
                ExternalAppsView.Refresh();
            }
        }
    }

    public string Log
    {
        get => _log;
        private set => SetProperty(ref _log, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandState();
            }
        }
    }

    public bool IsAdministrator => _windowsService.IsAdministrator;

    /// <summary>Initial load of apps; called once on window load.</summary>
    public async Task InitializeAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            Status = "Loading applications...";
            await LoadWindowsAsync();
            LoadExternal();
            Status = "Ready";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Re-check installed state for Windows apps.</summary>
    public async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            Status = "Refreshing statuses...";
            await _windowsService.RefreshStatusAsync(WindowsApps, CancellationToken.None);
            WindowsAppsView.Refresh();
            Status = "Ready";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Populate Windows apps definitions + attach change handlers.</summary>
    private async Task LoadWindowsAsync()
    {
        WindowsApps.Clear();
        var apps = await _windowsService.GetWindowsAppsAsync();
        foreach (var app in apps)
        {
            app.PropertyChanged += OnAppPropertyChanged;
            WindowsApps.Add(app);
        }
        WindowsAppsView.Refresh();
    }

    /// <summary>Populate external apps list.</summary>
    private void LoadExternal()
    {
        ExternalApps.Clear();
        foreach (var app in _externalService.GetExternalApps())
        {
            app.PropertyChanged += OnExternalPropertyChanged;
            ExternalApps.Add(app);
        }
        ExternalAppsView.Refresh();
    }

    /// <summary>Install selected Windows apps via WindowsAppService.</summary>
    private async Task InstallSelectedWindowsAsync()
    {
        var selected = WindowsApps.Where(x => x.IsSelected).ToList();
        if (!selected.Any())
        {
            return;
        }

        try
        {
            IsBusy = true;
            Status = $"Installing ({selected.Count})";
            await _windowsService.InstallAsync(selected, AppendLog, CancellationToken.None);
            await _windowsService.RefreshStatusAsync(selected, CancellationToken.None);
        }
        finally
        {
            IsBusy = false;
            RaiseCommandState();
        }
    }

    /// <summary>Uninstall selected Windows apps via WindowsAppService.</summary>
    private async Task UninstallSelectedWindowsAsync()
    {
        var selected = WindowsApps.Where(x => x.IsSelected).ToList();
        if (!selected.Any())
        {
            return;
        }

        try
        {
            IsBusy = true;
            Status = $"Uninstalling ({selected.Count})";
            await _windowsService.UninstallAsync(selected, AppendLog, CancellationToken.None);
            await _windowsService.RefreshStatusAsync(selected, CancellationToken.None);
        }
        finally
        {
            IsBusy = false;
            RaiseCommandState();
        }
    }

    /// <summary>Install selected external apps via ExternalAppService.</summary>
    private async Task InstallSelectedExternalAsync()
    {
        var selected = ExternalApps.Where(x => x.IsSelected).ToList();
        if (!selected.Any())
        {
            return;
        }

        try
        {
            IsBusy = true;
            Status = $"Installing external ({selected.Count})";
            await _externalService.InstallAsync(selected, AppendLog, CancellationToken.None);
        }
        finally
        {
            IsBusy = false;
            RaiseCommandState();
        }
    }

    /// <summary>Uninstall selected external apps via ExternalAppService.</summary>
    private async Task UninstallSelectedExternalAsync()
    {
        var selected = ExternalApps.Where(x => x.IsSelected).ToList();
        if (!selected.Any())
        {
            return;
        }

        try
        {
            IsBusy = true;
            Status = $"Uninstalling external ({selected.Count})";
            await _externalService.UninstallAsync(selected, AppendLog, CancellationToken.None);
        }
        finally
        {
            IsBusy = false;
            RaiseCommandState();
        }
    }

    /// <summary>Prepend single-line log text to in-memory log (used for debug/diagnostics).</summary>
    private void AppendLog(string text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        Log = $"{DateTime.Now:HH:mm:ss} {trimmed}{Environment.NewLine}{Log}";
    }

    private bool FilterWindows(object obj)
    {
        if (obj is not SoftwareItem app)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return true;
        }

        return app.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
            || app.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
    }

    private bool FilterExternal(object obj)
    {
        if (obj is not SoftwareItem app)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return true;
        }

        return app.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
            || app.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
    }

    private void SelectAllWindows()
    {
        foreach (var app in WindowsApps)
        {
            app.IsSelected = true;
        }
    }

    private void SelectWindowsByStatus(AppStatus status)
    {
        foreach (var app in WindowsApps)
        {
            app.IsSelected = app.Status == status;
        }
    }

    private void ClearSelection()
    {
        foreach (var app in WindowsApps)
        {
            app.IsSelected = false;
        }
    }

    private void SelectAllExternal()
    {
        foreach (var app in ExternalApps)
        {
            app.IsSelected = true;
        }
    }

    private void ClearExternalSelection()
    {
        foreach (var app in ExternalApps)
        {
            app.IsSelected = false;
        }
    }

    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SoftwareItem.IsSelected))
        {
            RaiseCommandState();
        }
    }

    private void OnExternalPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SoftwareItem.IsSelected))
        {
            RaiseCommandState();
        }
    }

    private bool CanRunWindowsAction() => !IsBusy && WindowsApps.Any(x => x.IsSelected);
    private bool CanRunExternalAction() => !IsBusy && ExternalApps.Any(x => x.IsSelected);

    private void RaiseCommandState()
    {
        _installWindowsCommand.RaiseCanExecuteChanged();
        _uninstallWindowsCommand.RaiseCanExecuteChanged();
        _installExternalCommand.RaiseCanExecuteChanged();
        _uninstallExternalCommand.RaiseCanExecuteChanged();
        _refreshCommand.RaiseCanExecuteChanged();
        LoadCommand.RaiseCanExecuteChanged();
    }
}

