using WinManager.Common;
using WinManager.Helpers;
using WinManager.Models;
using WinManager.Services;

namespace WinManager.ViewModels;

public class FeaturesViewModel : ObservableObject
{
    private readonly ProcessRunner _runner = new();
    private string _statusMessage = string.Empty;
    private bool _showStatus;
    private bool _isBusy;

    public FeaturesViewModel()
    {
        DownloadItems = new List<FeatureItem>
        {
            new("All .NET Framework",
                "Enable-WindowsOptionalFeature -Online -FeatureName NetFx3 -All -NoRestart",
                "Install")
        };

        EnableItems = new List<WindowsFeatureItem>
        {
            new("Hyper-V Virtualization", "Microsoft-Hyper-V-All"),
            new("Legacy Media", "WindowsMediaPlayer"),
            new("NFS", "ServicesForNFS-ClientOnly"),
            new("Windows Sandbox", "Containers-DisposableClientVM"),
            new("WSL", "Microsoft-Windows-Subsystem-Linux")
        };

        RunItems = new List<FeatureItem>
        {
            new("Reset Network",
                "netsh winsock reset; netsh int ip reset; ipconfig /flushdns; ipconfig /release; ipconfig /renew",
                "Run"),
            new("Set Up Autologin",
                "Start-Process netplwiz",
                "Run"),
            new("System Corruption Scan",
                "Start-Process powershell -ArgumentList 'sfc /scannow; Read-Host Press_Enter' -Verb RunAs",
                "Run")
        };

        ExecuteFeatureCommand = new RelayCommand<FeatureItem>(OnExecuteFeature);
        ToggleFeatureCommand = new RelayCommand<WindowsFeatureItem>(OnToggleFeature);
        DismissStatusCommand = new RelayCommand(() => ShowStatus = false);

        _ = CheckFeatureStatesAsync();
    }

    public IReadOnlyList<FeatureItem> DownloadItems { get; }
    public IReadOnlyList<WindowsFeatureItem> EnableItems { get; }
    public IReadOnlyList<FeatureItem> RunItems { get; }

    public bool IsLegacyMediaSupported => !WindowsVersion.IsAtLeast22H2;

    public RelayCommand<FeatureItem> ExecuteFeatureCommand { get; }
    public RelayCommand<WindowsFeatureItem> ToggleFeatureCommand { get; }
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

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    private async Task CheckFeatureStatesAsync()
    {
        var tasks = EnableItems.Select(async feature =>
        {
            try
            {
                var result = await _runner.RunAsync("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -Command \"(Get-WindowsOptionalFeature -Online -FeatureName '{feature.WindowsFeatureName}').State\"");

                var state = result.Output.Trim();
                feature.IsEnabled = state.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
            }
            finally
            {
                feature.IsChecking = false;
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task RefreshFeatureStateAsync(WindowsFeatureItem feature)
    {
        try
        {
            var result = await _runner.RunAsync("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -Command \"(Get-WindowsOptionalFeature -Online -FeatureName '{feature.WindowsFeatureName}').State\"");

            var state = result.Output.Trim();
            feature.IsEnabled = state.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
        }
    }

    private async void OnToggleFeature(WindowsFeatureItem? feature)
    {
        if (feature is null || IsBusy)
            return;

        IsBusy = true;
        var action = feature.IsEnabled ? "Disabling" : "Enabling";
        StatusMessage = $"{action}: {feature.Name}...";
        ShowStatus = true;

        try
        {
            var command = feature.IsEnabled
                ? $"Disable-WindowsOptionalFeature -Online -FeatureName '{feature.WindowsFeatureName}' -NoRestart"
                : $"Enable-WindowsOptionalFeature -Online -FeatureName '{feature.WindowsFeatureName}' -All -NoRestart";

            var result = await _runner.RunAsync("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"");

            await RefreshFeatureStateAsync(feature);

            StatusMessage = result.Success
                ? $"{feature.Name} — {feature.StatusText.ToLowerInvariant()} successfully. Restart may be required."
                : $"{feature.Name} — failed. {result.Output.Trim()}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{feature.Name} — error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnExecuteFeature(FeatureItem? item)
    {
        if (item is null || IsBusy)
            return;

        if (item.Name == "All .NET Framework")
        {
            await InstallAllDotNetAsync();
            return;
        }

        IsBusy = true;
        StatusMessage = $"Running: {item.Name}...";
        ShowStatus = true;

        try
        {
            var result = await _runner.RunAsync("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -Command \"{item.PowerShellCommand}\"");

            StatusMessage = result.Success
                ? $"{item.Name} — completed successfully."
                : $"{item.Name} — failed. {result.Output.Trim()}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{item.Name} — error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InstallAllDotNetAsync()
    {
        IsBusy = true;
        ShowStatus = true;
        bool allSucceeded = true;

        try
        {
            StatusMessage = "Installing .NET Framework 3.5...";
            var r1 = await _runner.RunAsync("dism.exe",
                "/online /enable-feature /featurename:NetFx3 /all /norestart /LimitAccess",
                timeout: TimeSpan.FromMinutes(10));
            if (!r1.Success) allSucceeded = false;

            StatusMessage = "Installing .NET 6 Runtime...";
            var r2 = await _runner.RunAsync("winget",
                "install --id Microsoft.DotNet.DesktopRuntime.6 --exact --silent --accept-source-agreements --accept-package-agreements",
                timeout: TimeSpan.FromMinutes(5));
            if (!r2.Success) allSucceeded = false;

            StatusMessage = "Installing .NET 7 Runtime...";
            var r3 = await _runner.RunAsync("winget",
                "install --id Microsoft.DotNet.DesktopRuntime.7 --exact --silent --accept-source-agreements --accept-package-agreements",
                timeout: TimeSpan.FromMinutes(5));
            if (!r3.Success) allSucceeded = false;

            StatusMessage = "Installing .NET 8 Runtime...";
            var r4 = await _runner.RunAsync("winget",
                "install --id Microsoft.DotNet.DesktopRuntime.8 --exact --silent --accept-source-agreements --accept-package-agreements",
                timeout: TimeSpan.FromMinutes(5));
            if (!r4.Success) allSucceeded = false;

            StatusMessage = "Installing .NET 9 Runtime...";
            var r5 = await _runner.RunAsync("winget",
                "install --id Microsoft.DotNet.DesktopRuntime.9 --exact --silent --accept-source-agreements --accept-package-agreements",
                timeout: TimeSpan.FromMinutes(5));
            if (!r5.Success) allSucceeded = false;

            StatusMessage = allSucceeded
                ? "All .NET runtimes installed successfully."
                : "Completed with errors — some runtimes may not have installed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"All .NET Framework — error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
