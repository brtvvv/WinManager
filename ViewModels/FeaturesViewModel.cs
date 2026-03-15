using WinManager.Common;
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

        EnableItems = new List<FeatureItem>
        {
            new("Hyper-V Virtualization",
                "Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All -NoRestart",
                "Enable"),
            new("Legacy Media",
                "Enable-WindowsOptionalFeature -Online -FeatureName WindowsMediaPlayer -All -NoRestart",
                "Enable"),
            new("NFS",
                "Enable-WindowsOptionalFeature -Online -FeatureName ServicesForNFS-ClientOnly -All -NoRestart; Enable-WindowsOptionalFeature -Online -FeatureName ClientForNFS-Infrastructure -All -NoRestart",
                "Enable"),
            new("Windows Sandbox",
                "Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All -NoRestart",
                "Enable"),
            new("WSL",
                "wsl --install --no-distribution",
                "Enable")
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
        DismissStatusCommand = new RelayCommand(() => ShowStatus = false);
    }

    public IReadOnlyList<FeatureItem> DownloadItems { get; }
    public IReadOnlyList<FeatureItem> EnableItems { get; }
    public IReadOnlyList<FeatureItem> RunItems { get; }

    public RelayCommand<FeatureItem> ExecuteFeatureCommand { get; }
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

    private async void OnExecuteFeature(FeatureItem? item)
    {
        if (item is null || IsBusy)
            return;

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
}
