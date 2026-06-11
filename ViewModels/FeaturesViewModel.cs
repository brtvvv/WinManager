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
                "Run",
                description: "Works with local accounts only \u2014 Microsoft accounts are not supported"),
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
                await ReadFeatureStateAsync(feature);
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
            await ReadFeatureStateAsync(feature);
        }
        catch
        {
        }
    }

    // Get-WindowsOptionalFeature returns the feature's State on a single line.
    // On Home editions some optional features (e.g. Hyper-V, WindowsMediaPlayer)
    // are stripped entirely — the cmdlet emits no State value (empty stdout)
    // and writes "feature is unknown" to stderr. Treat empty / unrecognized
    // output as "Not available" so the UI disables the toggle instead of
    // misleading the user.
    private async Task ReadFeatureStateAsync(WindowsFeatureItem feature)
    {
        var result = await RunPowerShellWithFallbackAsync(
            $"(Get-WindowsOptionalFeature -Online -FeatureName '{feature.WindowsFeatureName}').State");

        var state = result.Output?.Trim() ?? string.Empty;

        if (!result.Success || string.IsNullOrEmpty(state) ||
            state.IndexOf("unknown", StringComparison.OrdinalIgnoreCase) >= 0 ||
            state.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            feature.IsNotAvailable = true;
            feature.IsEnabled = false;
            return;
        }

        if (state.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("Disabled", StringComparison.OrdinalIgnoreCase) ||
            state.EndsWith("Pending", StringComparison.OrdinalIgnoreCase))
        {
            feature.IsNotAvailable = false;
            feature.IsEnabled = state.StartsWith("Enabled", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            feature.IsNotAvailable = true;
            feature.IsEnabled = false;
        }
    }

    private async void OnToggleFeature(WindowsFeatureItem? feature)
    {
        if (feature is null || IsBusy)
            return;

        if (feature.IsNotAvailable)
        {
            StatusMessage = $"{feature.Name} is not available on this Windows edition.";
            ShowStatus = true;
            return;
        }

        IsBusy = true;
        var action = feature.IsEnabled ? "Disabling" : "Enabling";
        StatusMessage = $"{action}: {feature.Name}... (this can take several minutes)";
        ShowStatus = true;

        try
        {
            var command = feature.IsEnabled
                ? $"Disable-WindowsOptionalFeature -Online -FeatureName '{feature.WindowsFeatureName}' -NoRestart"
                : $"Enable-WindowsOptionalFeature -Online -FeatureName '{feature.WindowsFeatureName}' -All -NoRestart";

            // Enable/Disable-WindowsOptionalFeature can run well past 5 minutes
            // for large features (Hyper-V, WSL); the default timeout would kill
            // the operation silently. Bump to 10 minutes.
            var result = await RunPowerShellWithFallbackAsync(command,
                timeout: TimeSpan.FromMinutes(10));

            await RefreshFeatureStateAsync(feature);

            StatusMessage = result.Success
                ? $"{feature.Name} \u2014 {feature.StatusText.ToLowerInvariant()} successfully. Restart may be required."
                : $"{feature.Name} \u2014 failed. {result.Output.Trim()}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{feature.Name} \u2014 error: {ex.Message}";
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
        StatusMessage = $"Working: {item.Name}...";
        ShowStatus = true;

        try
        {
            var result = await RunPowerShellWithFallbackAsync(item.PowerShellCommand);

            if (item.Name == "Set Up Autologin")
            {
                // netplwiz only works for local accounts; warn the user
                // explicitly after launch instead of leaving "completed".
                StatusMessage = "netplwiz opened. Works with local accounts only \u2014 Microsoft accounts are not supported.";
            }
            else
            {
                StatusMessage = result.Success
                    ? $"{item.Name} \u2014 completed successfully."
                    : $"{item.Name} \u2014 failed. {result.Output.Trim()}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"{item.Name} \u2014 error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // On some 21H2 installs powershell.exe is not on PATH for child processes
    // (rare but reported), so RunAsync("powershell.exe", ...) silently fails.
    // Try the bare name first, then fall back to the absolute System32 path.
    // The fallback only fires for ProcessRunner startup failures (-1 exit code
    // with no output), not for timeouts or normal non-zero exits.
    private async Task<ProcessResult> RunPowerShellWithFallbackAsync(string command,
        TimeSpan? timeout = null)
    {
        var args = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"";

        var first = await _runner.RunAsync("powershell.exe", args, timeout: timeout);
        if (first.Success || first.ExitCode != -1 || first.Output.StartsWith("Timed out", StringComparison.OrdinalIgnoreCase))
            return first;

        const string fullPath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
        if (!System.IO.File.Exists(fullPath))
            return first;

        return await _runner.RunAsync(fullPath, args, timeout: timeout);
    }

    private async Task InstallAllDotNetAsync()
    {
        IsBusy = true;
        ShowStatus = true;
        var missing = new List<string>();

        try
        {
            StatusMessage = "Installing .NET Framework 3.5...";
            await _runner.RunAsync("dism.exe",
                "/online /enable-feature /featurename:NetFx3 /all /norestart /LimitAccess",
                timeout: TimeSpan.FromMinutes(10));
            if (!await IsNetFx3EnabledAsync()) missing.Add(".NET Framework 3.5");

            foreach (var major in new[] { 6, 7, 8, 9 })
            {
                StatusMessage = $"Installing .NET {major} Runtime...";
                await _runner.RunAsync("winget",
                    $"install --id Microsoft.DotNet.DesktopRuntime.{major} --exact --silent --accept-source-agreements --accept-package-agreements",
                    timeout: TimeSpan.FromMinutes(5));
                if (!await IsDesktopRuntimeInstalledAsync(major))
                    missing.Add($".NET {major} Desktop Runtime");
            }

            StatusMessage = missing.Count == 0
                ? "All .NET runtimes installed successfully."
                : $"Missing after install: {string.Join(", ", missing)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"All .NET Framework \u2014 error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // winget exits non-zero for benign outcomes ("already installed", "no
    // applicable upgrade found"). Verify each runtime by querying `dotnet`
    // itself instead of trusting the exit code.
    private async Task<bool> IsDesktopRuntimeInstalledAsync(int major)
    {
        var result = await _runner.RunAsync("dotnet", "--list-runtimes",
            timeout: TimeSpan.FromSeconds(30));
        if (!result.Success) return false;
        var prefix = $"Microsoft.WindowsDesktop.App {major}.";
        return result.Output?.Contains(prefix, StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task<bool> IsNetFx3EnabledAsync()
    {
        var result = await _runner.RunAsync("dism.exe",
            "/online /get-featureinfo /featurename:NetFx3",
            timeout: TimeSpan.FromMinutes(2));
        if (!result.Success) return false;
        return result.Output?.IndexOf("State : Enabled", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
