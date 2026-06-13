using WinManager.Common;
using WinManager.Models;
using WinManager.Services;

namespace WinManager.ViewModels.Optimization;

public class NotificationsViewModel : OptimizationCategoryViewModelBase
{
    private readonly PrivacySettingsService _service = new();
    private readonly ProcessRunner _runner = new();
    private string _statusMessage = string.Empty;
    private bool _showStatus;

    private readonly PrivacyToggleItem _showNotificationsItem;
    private readonly PrivacyToggleItem _systemTrayItem;

    public NotificationsViewModel() : base("Notifications")
    {
        // NoToastApplicationNotification under the Explorer policy key is the
        // system-wide kill switch for all toast notifications. Enabled = 0
        // (no policy = notifications on), Disabled = 1 (policy set = off).
        // On 25H2 toasts flow through wpnuserservice which also reads
        // DisableNotificationCenter + ToastEnabled — we write all three in
        // ApplyShowNotificationsExtrasAsync after the primary SetStateAsync.
        _showNotificationsItem = new("Show Notifications",
            "Enable or disable all Windows toast notifications globally. A sign-out or Explorer restart may be needed for full effect.",
            "HKCU", @"SOFTWARE\Policies\Microsoft\Windows\CurrentVersion\PushNotifications",
            "NoToastApplicationNotification", 0, 1);

        _systemTrayItem = new("Show Notifications in the System Tray",
            "Display notification badges and alerts in the taskbar system tray area",
            "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings",
            "NOC_GLOBAL_SETTING_BADGE_ENABLED", 1, 0);

        ToggleGroups = new List<PrivacyToggleGroup>
        {
            new("Notifications", new List<PrivacyToggleItem>
            {
                _showNotificationsItem,

                new("Allow Notifications to Play Sounds",
                    "Notifications will play a sound when they appear",
                    "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings",
                    "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", 1, 0),

                new("Show Notifications on the Lock Screen",
                    "Display notifications when the screen is locked",
                    "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings",
                    "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK", 1, 0),

                _systemTrayItem,
            }),
        };

        ToggleCommand = new RelayCommand<PrivacyToggleItem>(OnToggle);
        DismissStatusCommand = new RelayCommand(() => ShowStatus = false);

        _ = LoadStatesAsync();
    }

    public IReadOnlyList<PrivacyToggleGroup> ToggleGroups { get; }
    public RelayCommand<PrivacyToggleItem> ToggleCommand { get; }
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

    private async Task LoadStatesAsync()
    {
        var allItems = ToggleGroups.SelectMany(g => g.Items).ToList();
        await _service.ReadAllStatesAsync(allItems);
    }

    private async void OnToggle(PrivacyToggleItem? item)
    {
        if (item is null) return;

        var target = !item.IsEnabled;
        StatusMessage = $"{(target ? "Enabling" : "Disabling")}: {item.Name}...";
        ShowStatus = true;

        var success = await _service.SetStateAsync(item, target);

        if (success)
        {
            if (item == _showNotificationsItem)
                await ApplyShowNotificationsExtrasAsync(target);
            else if (item == _systemTrayItem)
                await ApplySystemTrayExtrasAsync(target);

            await _service.ReadStateAsync(item);
            item.IsChecking = false;
            StatusMessage = $"{item.Name} \u2014 {item.StatusText.ToLowerInvariant()} successfully.";
        }
        else
        {
            StatusMessage = $"{item.Name} \u2014 failed. Run as administrator.";
        }
    }

    // 25H2 routes toasts through wpnuserservice. The user-visible "Show
    // Notifications" must update three values together: the Explorer policy
    // DisableNotificationCenter, the per-user PushNotifications ToastEnabled,
    // and a service restart so wpnuserservice re-reads them.
    private async Task ApplyShowNotificationsExtrasAsync(bool enable)
    {
        var policyVal = enable ? 0 : 1;
        var userVal = enable ? 1 : 0;
        var script =
            "$pol = 'HKCU:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer'; " +
            "New-Item -Path $pol -Force -EA SilentlyContinue | Out-Null; " +
            $"Set-ItemProperty -Path $pol -Name 'DisableNotificationCenter' -Value {policyVal} -Type DWord -Force; " +
            "$push = 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PushNotifications'; " +
            "New-Item -Path $push -Force -EA SilentlyContinue | Out-Null; " +
            $"Set-ItemProperty -Path $push -Name 'ToastEnabled' -Value {userVal} -Type DWord -Force; " +
            "Restart-Service -Name WpnUserService* -Force -EA SilentlyContinue";
        await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"");
    }

    // Tray badge visibility lives in Advanced\EnableAutoTray (0 = always show
    // all icons, 1 = auto-hide). Changes only apply once Explorer is restarted.
    private async Task ApplySystemTrayExtrasAsync(bool enable)
    {
        var autoTray = enable ? 0 : 1;
        var script =
            "$adv = 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced'; " +
            $"Set-ItemProperty -Path $adv -Name 'EnableAutoTray' -Value {autoTray} -Type DWord -Force; " +
            "Stop-Process -Name explorer -Force -EA SilentlyContinue; Start-Process explorer";
        await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"");
    }
}
