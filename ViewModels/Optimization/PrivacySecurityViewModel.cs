using System.Collections.ObjectModel;
using System.Linq;
using WinManager.Common;
using WinManager.Helpers;
using WinManager.Models;
using WinManager.Services;

namespace WinManager.ViewModels.Optimization;

public class PrivacySecurityViewModel : OptimizationCategoryViewModelBase
{
    private readonly UacService _uacService = new();
    private readonly PrivacySettingsService _privacyService = new();
    private UacOption? _selectedUacOption;
    private DnsProvider? _selectedDnsProvider;
    private string _statusMessage = string.Empty;
    private bool _showStatus;
    private bool _suppressUacChange;
    private bool _suppressDnsChange;

    public PrivacySecurityViewModel() : base("Privacy & Security")
    {
        UacOptions = new ObservableCollection<UacOption>
        {
            new(UacLevel.PromptForCredentials, "Prompt for Credentials",
                "Always prompt for credentials on the secure desktop"),
            new(UacLevel.AlwaysNotify, "Always notify",
                "Notify when apps or you try to make changes"),
            new(UacLevel.NotifyAppChanges, "Notify when apps try to make changes",
                "Default. Notify only when apps try to make changes"),
            new(UacLevel.NotifyAppChangesNoDim, "Notify when apps try to make changes (no dim)",
                "Same as above but without dimming the desktop"),
            new(UacLevel.NeverNotify, "Never notify",
                "Never notify about changes (not recommended)")
        };

        DnsProviders = new ObservableCollection<DnsProvider>
        {
            new("Default (ISP / DHCP)", null, null, isDefault: true),
            new("Google (8.8.8.8)", "8.8.8.8", "8.8.4.4"),
            new("Cloudflare (1.1.1.1)", "1.1.1.1", "1.0.0.1"),
            new("Quad9 (9.9.9.9)", "9.9.9.9", "149.112.112.112"),
        };

        ToggleGroups = BuildToggleGroups();

        DismissStatusCommand = new RelayCommand(() => ShowStatus = false);
        TogglePrivacyCommand = new RelayCommand<PrivacyToggleItem>(OnTogglePrivacy);

        DetectCurrentLevel();
        _ = LoadToggleStatesAsync();
        _ = DetectCurrentDnsAsync();
    }

    // ── UAC (unchanged) ──────────────────────────────────────────

    public ObservableCollection<UacOption> UacOptions { get; }

    public UacOption? SelectedUacOption
    {
        get => _selectedUacOption;
        set
        {
            var previous = _selectedUacOption;
            if (!SetProperty(ref _selectedUacOption, value))
                return;

            if (_suppressUacChange || value is null || previous is null)
                return;

            var (success, message, _) = _uacService.SetUacLevel(value.Level);
            StatusMessage = message;
            ShowStatus = true;

            if (!success)
            {
                _suppressUacChange = true;
                SelectedUacOption = previous;
                _suppressUacChange = false;
            }
        }
    }

    public bool IsAdministrator => _uacService.IsAdministrator;

    public bool IsCortanaAvailable => !WindowsVersion.IsAtLeast23H2;

    private void DetectCurrentLevel()
    {
        var current = _uacService.GetCurrentUacLevel();
        _suppressUacChange = true;
        SelectedUacOption = UacOptions.FirstOrDefault(o => o.Level == current);
        _suppressUacChange = false;
    }

    // ── DNS Provider ──────────────────────────────────────────────

    public ObservableCollection<DnsProvider> DnsProviders { get; }

    public DnsProvider? SelectedDnsProvider
    {
        get => _selectedDnsProvider;
        set
        {
            var previous = _selectedDnsProvider;
            if (!SetProperty(ref _selectedDnsProvider, value))
                return;

            if (_suppressDnsChange || value is null || previous is null)
                return;

            _ = ApplyDnsChangeAsync(value, previous);
        }
    }

    private async Task ApplyDnsChangeAsync(DnsProvider selected, DnsProvider previous)
    {
        StatusMessage = $"Applying DNS: {selected.Name}...";
        ShowStatus = true;

        var success = await _privacyService.SetDnsProviderAsync(selected);

        if (success)
        {
            StatusMessage = $"DNS changed to {selected.Name}. DoH {(selected.IsDefault ? "disabled" : "enabled")}.";
        }
        else
        {
            StatusMessage = "DNS change failed. Run as administrator.";
            _suppressDnsChange = true;
            SelectedDnsProvider = previous;
            _suppressDnsChange = false;
        }
    }

    private async Task DetectCurrentDnsAsync()
    {
        var servers = await _privacyService.GetCurrentDnsServersAsync();

        DnsProvider? match = null;
        foreach (var provider in DnsProviders)
        {
            if (provider.IsDefault) continue;
            if (servers.Any(s => s == provider.PrimaryDns || s == provider.SecondaryDns))
            {
                match = provider;
                break;
            }
        }

        _suppressDnsChange = true;
        SelectedDnsProvider = match ?? DnsProviders[0];
        _suppressDnsChange = false;
    }

    // ── Toggle groups ────────────────────────────────────────────

    public IReadOnlyList<PrivacyToggleGroup> ToggleGroups { get; }

    public RelayCommand<PrivacyToggleItem> TogglePrivacyCommand { get; }

    private async Task LoadToggleStatesAsync()
    {
        var allItems = ToggleGroups.SelectMany(g => g.Items).ToList();
        await _privacyService.ReadAllStatesAsync(allItems);
    }

    private async void OnTogglePrivacy(PrivacyToggleItem? item)
    {
        if (item is null) return;

        var target = !item.IsEnabled;
        var action = target ? "Enabling" : "Disabling";
        StatusMessage = $"{action}: {item.Name}...";
        ShowStatus = true;

        var success = await _privacyService.SetStateAsync(item, target);

        if (success)
        {
            await _privacyService.ReadStateAsync(item);
            item.IsChecking = false;
            StatusMessage = $"{item.Name} — {item.StatusText.ToLowerInvariant()} successfully.";
        }
        else
        {
            StatusMessage = $"{item.Name} — failed. Run as administrator.";
        }
    }

    // ── Shared ───────────────────────────────────────────────────

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

    // ── Item definitions ─────────────────────────────────────────

    private static IReadOnlyList<PrivacyToggleGroup> BuildToggleGroups() => new List<PrivacyToggleGroup>
    {
        new("Device & System Security", new List<PrivacyToggleItem>
        {
            new("Block Workplace Join Messages",
                "Prevent workplace join prompts from appearing",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin",
                "BlockAADWorkplaceJoin", 1, 0) { DefaultIsEnabled = false },

            new("Prevent BitLocker Auto Encryption",
                "Stop automatic BitLocker device encryption",
                "HKLM", @"SYSTEM\CurrentControlSet\Control\BitLocker",
                "PreventDeviceEncryption", 1, 0) { DefaultIsEnabled = false },

            new("Remote Assistance",
                "Allow others to connect to your PC for help",
                "HKLM", @"SYSTEM\CurrentControlSet\Control\Remote Assistance",
                "fAllowToGetHelp", 1, 0),
        }),

        new("Networking & Connectivity", new List<PrivacyToggleItem>
        {
            new("WiFi Sense",
                "Auto-connect to suggested Wi-Fi networks",
                "HKLM", @"SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config",
                "AutoConnectAllowedOEM", 1, 0),
        }),

        new("Diagnostics & Telemetry", new List<PrivacyToggleItem>
        {
            PrivacyToggleItem.ForService("Windows Error Reporting",
                "Send crash reports to Microsoft via WerSvc", "WerSvc"),

            new("Send Diagnostic Data",
                "Send full telemetry data to Microsoft",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                "AllowTelemetry", 3, 0),

            new("Tailored Experiences",
                "Personalized tips based on diagnostic data",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy",
                "TailoredExperiencesWithDiagnosticDataEnabled", 1, 0),

            new("Allow Feedback Requests",
                "Let Windows ask for feedback periodically",
                "HKCU", @"SOFTWARE\Microsoft\Siuf\Rules",
                "NumberOfSIUFInPeriod", 1, 0) { DeleteOnEnable = true },

            new("App Diagnostic Access",
                "Allow apps to access diagnostic information",
                "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics",
                "Value", "Allow", "Deny") { IsStringValue = true },
        }),

        new("Advertising & Content", new List<PrivacyToggleItem>
        {
            new("Advertising ID",
                "Let apps use your advertising ID for targeted ads",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
                "Enabled", 1, 0),

            new("Content Delivery Manager",
                "Suggested apps, OEM pre-installs and silent installs",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                "ContentDeliveryAllowed", 1, 0)
            {
                ExtraValueNames = new[]
                {
                    "OemPreInstalledAppsEnabled",
                    "PreInstalledAppsEnabled",
                    "SilentInstalledAppsEnabled",
                    "PreInstalledAppsEverEnabled"
                }
            },

            new("Subscribed Content (Tips & Suggestions)",
                "Show tips, tricks and suggestions in Windows",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                "SubscribedContent-338389Enabled", 1, 0),
        }),

        new("Lock Screen", new List<PrivacyToggleItem>
        {
            new("Lock Workstation",
                "Allow locking the PC via Win+L, Start menu, and Ctrl+Alt+Del",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                "DisableLockWorkstation", 0, 1),

            new("Windows Spotlight",
                "Dynamic backgrounds and content on lock screen",
                "HKCU", @"SOFTWARE\Policies\Microsoft\Windows\CloudContent",
                "DisableWindowsSpotlightFeatures", 0, 1),

            new("Lock Screen Fun Facts & Tips",
                "Rotating facts and tips on the lock screen",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                "RotatingLockScreenEnabled", 1, 0),

            new("Lock Screen Slideshow",
                "Photo slideshow on the lock screen",
                "HKCU", @"SOFTWARE\Policies\Microsoft\Windows\Personalization",
                "NoLockScreenSlideshow", 0, 1),
        }),

        new("Search & Assistant", new List<PrivacyToggleItem>
        {
            new("Search History on This Device",
                "Store search history locally on this device",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\SearchSettings",
                "IsDeviceSearchHistoryEnabled", 1, 0),

            new("Show Search Highlights",
                "Display trending content in Windows Search",
                "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Feeds\DSB",
                "ShowDynamicContent", 1, 0),

            new("Allow Cortana",
                "Enable Cortana voice assistant features",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                "AllowCortana", 1, 0),
        }),

        new("App Permissions", new List<PrivacyToggleItem>
        {
            new("Location Access",
                "Allow apps to access your location",
                "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
                "Value", "Allow", "Deny") { IsStringValue = true },

            new("Camera Access",
                "Allow apps to access your camera",
                "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam",
                "Value", "Allow", "Deny") { IsStringValue = true },

            new("Microphone Access",
                "Allow apps to access your microphone",
                "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone",
                "Value", "Allow", "Deny") { IsStringValue = true },

            new("User Account Info Access",
                "Allow apps to access your account information",
                "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\userAccountInformation",
                "Value", "Allow", "Deny") { IsStringValue = true },
        }),

        new("Cloud & Backup", new List<PrivacyToggleItem>
        {
            new("Disable OneDrive Automatic Backup",
                "Block OneDrive Known Folder Move (KFM) opt-in",
                "HKLM", @"SOFTWARE\Policies\Microsoft\OneDrive",
                "KFMBlockOptIn", 1, 0) { DefaultIsEnabled = false },
        }),
    };
}
