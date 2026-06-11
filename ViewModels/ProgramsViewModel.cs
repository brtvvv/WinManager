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
    private readonly AsyncRelayCommand _uninstallWindowsCommand;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _installExternalCommand;
    private readonly AsyncRelayCommand _uninstallExternalCommand;
    private string _searchQuery = string.Empty;
    private string _log = string.Empty;
    private string _status = "Ready";
    private bool _isBusy;
    private CancellationTokenSource? _cts;
    private double _progressValue;
    private string _progressText = string.Empty;

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

        if (ExternalAppsView is System.Windows.Data.ListCollectionView lcv)
            lcv.GroupDescriptions.Add(
                new System.Windows.Data.PropertyGroupDescription(nameof(SoftwareItem.Category)));

        LoadCommand = new AsyncRelayCommand(InitializeAsync, () => !IsBusy);
        _refreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        _uninstallWindowsCommand = new AsyncRelayCommand(UninstallSelectedWindowsAsync, CanRunWindowsAction);
        _installExternalCommand = new AsyncRelayCommand(InstallSelectedExternalAsync, CanRunExternalAction);
        _uninstallExternalCommand = new AsyncRelayCommand(UninstallSelectedExternalAsync, CanRunExternalAction);
        SelectAllCommand = new RelayCommand(() => SelectAllWindows());
        SelectInstalledCommand = new RelayCommand(() => SelectWindowsByStatus(AppStatus.Installed));
        SelectNotInstalledCommand = new RelayCommand(() => SelectWindowsByStatus(AppStatus.NotInstalled));
        ClearSelectionCommand = new RelayCommand(() => ClearSelection());
        SelectAllExternalCommand = new RelayCommand(() => SelectAllExternal());
        ClearExternalCommand = new RelayCommand(() => ClearExternalSelection());
        CancelCommand = new RelayCommand(CancelOperation, () => IsBusy);
    }

    public ObservableCollection<SoftwareItem> WindowsApps { get; } = new();
    public ObservableCollection<SoftwareItem> ExternalApps { get; } = new();

    public ICollectionView WindowsAppsView { get; }
    public ICollectionView ExternalAppsView { get; }

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand RefreshCommand => _refreshCommand;
    public AsyncRelayCommand UninstallWindowsCommand => _uninstallWindowsCommand;
    public AsyncRelayCommand InstallExternalCommand => _installExternalCommand;
    public AsyncRelayCommand UninstallExternalCommand => _uninstallExternalCommand;
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectInstalledCommand { get; }
    public RelayCommand SelectNotInstalledCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }
    public RelayCommand SelectAllExternalCommand { get; }
    public RelayCommand ClearExternalCommand { get; }
    public RelayCommand CancelCommand { get; }

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

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
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

    private async Task UninstallSelectedWindowsAsync()
    {
        var selected = WindowsApps.Where(x => x.IsSelected).ToList();
        if (!selected.Any()) return;

        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            if (!_windowsService.IsAdministrator)
                AppendLog("Warning: not running as Administrator; uninstall may fail for some apps.");
            Status = $"Uninstalling ({selected.Count})...";
            await _windowsService.UninstallAsync(selected, AppendLog, _cts.Token, OnProgress);
            Status = "Refreshing statuses...";
            await _windowsService.RefreshStatusAsync(WindowsApps, _cts.Token);
            WindowsAppsView.Refresh();
            Status = "Ready";
        }
        catch (OperationCanceledException)
        {
            AppendLog("Uninstallation cancelled.");
            Status = "Cancelled";
        }
        finally
        {
            ResetProgress();
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
            RaiseCommandState();
        }
    }

    private async Task InstallSelectedExternalAsync()
    {
        var selected = ExternalApps.Where(x => x.IsSelected).ToList();
        if (!selected.Any()) return;

        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            Status = $"Installing external ({selected.Count})";
            await _externalService.InstallAsync(selected, AppendLog, _cts.Token, OnProgress);
            Status = "Ready";
        }
        catch (OperationCanceledException)
        {
            AppendLog("Installation cancelled.");
            Status = "Cancelled";
        }
        finally
        {
            ResetProgress();
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
            RaiseCommandState();
        }
    }

    private async Task UninstallSelectedExternalAsync()
    {
        var selected = ExternalApps.Where(x => x.IsSelected).ToList();
        if (!selected.Any()) return;

        _cts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            Status = $"Uninstalling external ({selected.Count})";
            await _externalService.UninstallAsync(selected, AppendLog, _cts.Token, OnProgress);
            Status = "Ready";
        }
        catch (OperationCanceledException)
        {
            AppendLog("Uninstallation cancelled.");
            Status = "Cancelled";
        }
        finally
        {
            ResetProgress();
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
            RaiseCommandState();
        }
    }

    private void CancelOperation()
    {
        _cts?.Cancel();
        Status = "Cancelling...";
    }

    private void OnProgress(int current, int total, string name)
    {
        ProgressValue = total > 0 ? (double)current / total * 100 : 0;
        ProgressText = total > 0 ? $"{current + 1}/{total} — {name}" : name;
    }

    private void ResetProgress()
    {
        ProgressValue = 0;
        ProgressText = string.Empty;
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
            || app.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
            || app.Category.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
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
        _uninstallWindowsCommand.RaiseCanExecuteChanged();
        _installExternalCommand.RaiseCanExecuteChanged();
        _uninstallExternalCommand.RaiseCanExecuteChanged();
        _refreshCommand.RaiseCanExecuteChanged();
        LoadCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }
}

