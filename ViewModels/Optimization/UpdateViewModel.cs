using System.ServiceProcess;
using Microsoft.Win32;
using WinManager.Common;
using WinManager.Helpers;
using WinManager.Models;
using WinManager.Services;

namespace WinManager.ViewModels.Optimization;

public class UpdateViewModel : OptimizationCategoryViewModelBase
{
    private readonly PrivacySettingsService _service = new();
    private readonly ProcessRunner _runner = new();
    private string _statusMessage = string.Empty;
    private bool _showStatus;
    private bool _isApplyingPreset;

    private readonly PrivacyToggleItem _deliveryOpt;
    private readonly PrivacyToggleItem _msProducts;
    private readonly List<PrivacyToggleItem> _standardItems;

    public UpdateViewModel() : base("Updates")
    {
        _deliveryOpt = new("Delivery Optimization",
            "Allows Windows to download updates from other PCs on your network and the internet",
            "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
            "DODownloadMode", 0, 100);

        var storeApps = new PrivacyToggleItem(
            "Auto Update Microsoft Store Apps",
            "Automatically keeps Microsoft Store applications up to date",
            "HKLM", @"SOFTWARE\Policies\Microsoft\WindowsStore",
            "AutoDownload", 4, 2);

        var latestUpdates = new PrivacyToggleItem(
            "Get the Latest Updates as Soon as Available",
            "Receive updates immediately when released, before broader rollout",
            "HKLM", @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
            "IsContinuousInnovationOptedIn", 1, 0) { DefaultIsEnabled = false };

        _msProducts = new PrivacyToggleItem(
            "Receive Updates for Other Microsoft Products",
            "Include updates for Office and other Microsoft software via Windows Update",
            "", "", "", 0, 0);

        var getUpToDate = new PrivacyToggleItem(
            "Get Me Up to Date",
            "Restart automatically to finish installing updates as quickly as possible",
            "HKLM", @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
            "IsExpedited", 1, 0) { DefaultIsEnabled = false };

        var preventRestart = new PrivacyToggleItem(
            "Prevent Automatic Restarts",
            "Windows will not restart automatically to apply updates while you are signed in",
            "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
            "NoAutoRebootWithLoggedOnUsers", 1, 0) { DefaultIsEnabled = false };

        var restartNotify = new PrivacyToggleItem(
            "Notify Me When a Restart Is Required",
            "Show a notification when a restart is needed to finish installing updates",
            "HKLM", @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
            "RestartNotificationsAllowed2", 1, 0);

        var meteredUpdates = new PrivacyToggleItem(
            "Download Updates Over Metered Connections",
            "Allow Windows Update to download updates even when on a metered network",
            "HKLM", @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
            "AllowAutoWindowsUpdateDownloadOverMeteredNetwork", 1, 0) { DefaultIsEnabled = false };

        AllItems = new List<PrivacyToggleItem>
        {
            _deliveryOpt, storeApps, latestUpdates, _msProducts,
            getUpToDate, preventRestart, restartNotify, meteredUpdates
        };

        _standardItems = AllItems.Where(i => i != _deliveryOpt && i != _msProducts).ToList();

        //                         DO    Store Latest MSProd UpToDt PrvRst Notify Meter
        Presets = new List<UpdatePreset>
        {
            new("Normal",
                "Windows default settings",
                new[] { true,  true,  false, true,  true,  false, true,  false }),

            new("Security Updates Only",
                "Receives only security patches, reduces bandwidth and restart frequency",
                new[] { false, false, false, false, false, true,  true,  false },
                badge: "Recommended"),

            new("Disabled",
                "Disables all automatic updates. Security risk \u2014 use only in isolated environments",
                new[] { false, false, false, false, false, true,  false, false },
                badge: "Not Recommended", isWarning: true),
        };

        ToggleCommand = new RelayCommand<PrivacyToggleItem>(OnToggle);
        ApplyPresetCommand = new RelayCommand<UpdatePreset>(OnApplyPreset);
        DismissStatusCommand = new RelayCommand(() => ShowStatus = false);

        _ = InitializeAsync();
    }

    public IReadOnlyList<PrivacyToggleItem> AllItems { get; }
    public IReadOnlyList<UpdatePreset> Presets { get; }

    public bool IsLatestUpdatesAvailable => WindowsVersion.IsAtLeast22H2;
    public RelayCommand<PrivacyToggleItem> ToggleCommand { get; }
    public RelayCommand<UpdatePreset> ApplyPresetCommand { get; }
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
        await Task.WhenAll(
            _service.ReadAllStatesAsync(_standardItems),
            ReadDeliveryOptStateAsync(),
            ReadMsProductsStateAsync());
        DetectActivePreset();
    }

    // ── Individual toggle ────────────────────────────────────────

    private async void OnToggle(PrivacyToggleItem? item)
    {
        if (item is null || _isApplyingPreset) return;

        var target = !item.IsEnabled;
        StatusMessage = $"{(target ? "Enabling" : "Disabling")}: {item.Name}...";
        ShowStatus = true;

        var success = await WriteItemAsync(item, target);

        if (success)
        {
            await ReadItemAsync(item);
            DetectActivePreset();
            StatusMessage = $"{item.Name} \u2014 {item.StatusText.ToLowerInvariant()} successfully.";
        }
        else
        {
            StatusMessage = $"{item.Name} \u2014 failed. Run as administrator.";
        }
    }

    // ── Presets ──────────────────────────────────────────────────

    private async void OnApplyPreset(UpdatePreset? preset)
    {
        if (preset is null) return;

        _isApplyingPreset = true;
        StatusMessage = $"Applying preset: {preset.Name}...";
        ShowStatus = true;

        try
        {
            for (int i = 0; i < AllItems.Count && i < preset.TargetStates.Length; i++)
                await WriteItemAsync(AllItems[i], preset.TargetStates[i]);

            await ApplyPresetExtrasAsync(preset);

            await Task.WhenAll(
                _service.ReadAllStatesAsync(_standardItems),
                ReadDeliveryOptStateAsync(),
                ReadMsProductsStateAsync());
            DetectActivePreset();

            StatusMessage = $"Preset \"{preset.Name}\" applied successfully.";
        }
        catch
        {
            StatusMessage = $"Preset \"{preset.Name}\" \u2014 some settings failed. Run as administrator.";
        }
        finally
        {
            _isApplyingPreset = false;
        }
    }

    private async Task ApplyPresetExtrasAsync(UpdatePreset preset)
    {
        var auPath = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";

        if (preset == Presets[0])
        {
            await RunPsSuccessAsync(
                $"New-Item -Path '{auPath}' -Force -EA SilentlyContinue | Out-Null; " +
                $"Remove-ItemProperty -Path '{auPath}' -Name 'AUOptions' -EA SilentlyContinue; " +
                $"Remove-ItemProperty -Path '{auPath}' -Name 'NoAutoUpdate' -EA SilentlyContinue; " +
                "Set-Service 'wuauserv' -StartupType Manual -EA SilentlyContinue; " +
                "Start-Service 'wuauserv' -EA SilentlyContinue");
        }
        else if (preset == Presets[1])
        {
            await RunPsSuccessAsync(
                $"New-Item -Path '{auPath}' -Force -EA SilentlyContinue | Out-Null; " +
                $"Set-ItemProperty -Path '{auPath}' -Name 'AUOptions' -Value 3 -Type DWord -Force; " +
                $"Remove-ItemProperty -Path '{auPath}' -Name 'NoAutoUpdate' -EA SilentlyContinue; " +
                "Set-Service 'wuauserv' -StartupType Manual -EA SilentlyContinue; " +
                "Start-Service 'wuauserv' -EA SilentlyContinue");
        }
        else if (preset == Presets[2])
        {
            await RunPsSuccessAsync(
                $"New-Item -Path '{auPath}' -Force -EA SilentlyContinue | Out-Null; " +
                $"Set-ItemProperty -Path '{auPath}' -Name 'NoAutoUpdate' -Value 1 -Type DWord -Force; " +
                $"Remove-ItemProperty -Path '{auPath}' -Name 'AUOptions' -EA SilentlyContinue; " +
                "Stop-Service 'wuauserv' -Force -EA SilentlyContinue; " +
                "Set-Service 'wuauserv' -StartupType Disabled -EA SilentlyContinue");
        }
    }

    private void DetectActivePreset()
    {
        foreach (var preset in Presets)
        {
            bool match = true;
            for (int i = 0; i < AllItems.Count && i < preset.TargetStates.Length; i++)
            {
                if (AllItems[i].IsEnabled != preset.TargetStates[i])
                {
                    match = false;
                    break;
                }
            }
            preset.IsActive = match;
        }
    }

    // ── Unified read / write dispatch ────────────────────────────

    private async Task<bool> WriteItemAsync(PrivacyToggleItem item, bool enable)
    {
        if (item == _deliveryOpt) return await WriteDeliveryOptAsync(enable);
        if (item == _msProducts) return await WriteMsProductsAsync(enable);
        return await _service.SetStateAsync(item, enable);
    }

    private async Task ReadItemAsync(PrivacyToggleItem item)
    {
        if (item == _deliveryOpt) { await ReadDeliveryOptStateAsync(); return; }
        if (item == _msProducts) { await ReadMsProductsStateAsync(); return; }
        await _service.ReadStateAsync(item);
        item.IsChecking = false;
    }

    // ── Delivery Optimization (custom: OFF = 100, ON = remove key) ─

    private async Task ReadDeliveryOptStateAsync()
    {
        try
        {
            var hp = @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
            var result = await RunPsAsync(
                $"try {{ (Get-ItemProperty -Path '{hp}' -Name 'DODownloadMode' -EA Stop).DODownloadMode }} catch {{ 'NOTFOUND' }}");
            var output = result.Trim();
            _deliveryOpt.IsEnabled = output is "NOTFOUND" or ""
                || !int.TryParse(output, out var val) || val != 100;
        }
        catch { _deliveryOpt.IsEnabled = true; }
        _deliveryOpt.IsChecking = false;
    }

    private Task<bool> WriteDeliveryOptAsync(bool enable)
    {
        // All registry + service work done in-process so it cannot fail with
        // "Run as administrator" from a child PowerShell whose token is filtered.
        return Task.Run(() =>
        {
            try
            {
                const string keyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";

                if (enable)
                {
                    // Remove the policy value (if any) and bring DoSvc back to Manual.
                    using (var k = Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", writable: true))
                    {
                        k?.DeleteValue("DODownloadMode", throwOnMissingValue: false);
                    }
                    SetServiceStartMode("DoSvc", ServiceStartMode.Manual);
                    StartServiceSafe("DoSvc");
                    return true;
                }

                // Disable: write policy, stop service, set to Disabled.
                Registry.SetValue(keyPath, "DODownloadMode", 100, RegistryValueKind.DWord);
                StopServiceSafe("DoSvc");
                SetServiceStartMode("DoSvc", ServiceStartMode.Disabled);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    private static void StopServiceSafe(string name)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
            }
        }
        catch { }
    }

    private static void StartServiceSafe(string name)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
            }
        }
        catch { }
    }

    private static void SetServiceStartMode(string name, ServiceStartMode mode)
    {
        // ServiceController has no public StartType setter pre-.NET 9, so we use sc.exe
        var startArg = mode switch
        {
            ServiceStartMode.Disabled => "disabled",
            ServiceStartMode.Manual => "demand",
            ServiceStartMode.Automatic => "auto",
            _ => "demand"
        };
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("sc.exe", $"config {name} start= {startArg}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(3000);
        }
        catch { }
    }

    // ── MS Products (custom: COM API) ────────────────────────────

    private const string MsUpdateServiceId = "7971f918-a847-4430-9279-4a52d1efe18d";

    private Task ReadMsProductsStateAsync()
    {
        try
        {
            _msProducts.IsEnabled = IsMsUpdateServiceRegistered();
        }
        catch { _msProducts.IsEnabled = true; }
        _msProducts.IsChecking = false;
        return Task.CompletedTask;
    }

    private async Task<bool> WriteMsProductsAsync(bool enable)
    {
        // PowerShell child processes inherit a "filtered" admin token that
        // makes Microsoft.Update.ServiceManager reject AddService2 with
        // 0x80070005 on 25H2. We instantiate the COM object in-process from
        // the already-elevated WinManager so the call succeeds.
        return await Task.Run(() =>
        {
            try
            {
                var type = Type.GetTypeFromProgID("Microsoft.Update.ServiceManager");
                if (type is null) return false;
                dynamic mgr = Activator.CreateInstance(type)!;
                mgr.ClientApplicationID = "WinManager";
                if (enable)
                    mgr.AddService2(MsUpdateServiceId, 7, "");
                else
                    mgr.RemoveService(MsUpdateServiceId);
                return true;
            }
            catch { return false; }
        });
    }

    private static bool IsMsUpdateServiceRegistered()
    {
        try
        {
            var type = Type.GetTypeFromProgID("Microsoft.Update.ServiceManager");
            if (type is null) return false;
            dynamic mgr = Activator.CreateInstance(type)!;
            foreach (var svc in mgr.Services)
            {
                if (string.Equals((string)svc.ServiceID, MsUpdateServiceId,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch { return false; }
    }

    // ── PowerShell helpers ───────────────────────────────────────

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
