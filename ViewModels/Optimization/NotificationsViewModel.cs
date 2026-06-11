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

    public NotificationsViewModel() : base("Notifications")
    {
        // NoToastApplicationNotification under the Explorer policy key is the
        // system-wide kill switch for all toast notifications. Enabled = 0
        // (no policy = notifications on), Disabled = 1 (policy set = off).
        // We also mirror the user-scope ToastEnabled value (different key) in
        // the toggle handler so legacy UWP/WinRT toast sources also honour it.
        _showNotificationsItem = new("Show Notifications",
            "Enable or disable all Windows toast notifications globally. A sign-out or Explorer restart may be needed for full effect.",
            "HKCU", @"SOFTWARE\Policies\Microsoft\Windows\CurrentVersion\PushNotifications",
            "NoToastApplicationNotification", 0, 1);

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

                new("Show Notifications in the System Tray",
                    "Display notification badges and alerts in the taskbar system tray area",
                    "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings",
                    "NOC_GLOBAL_SETTING_BADGE_ENABLED", 1, 0),
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
            {
                // Secondary write: HKCU\...\PushNotifications\ToastEnabled —
                // different key path than the policy, so we can't piggy-back
                // on ExtraValueNames. Mirror the state so apps that still
                // check the legacy value honour the user's choice. The user
                // perspective is: target = true ⇒ notifications on ⇒
                // ToastEnabled = 1; target = false ⇒ ToastEnabled = 0.
                var toast = target ? 1 : 0;
                var ts = @"HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\PushNotifications";
                await _runner.RunAsync("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -Command \"New-Item -Path '{ts}' -Force -EA SilentlyContinue | Out-Null; Set-ItemProperty -Path '{ts}' -Name 'ToastEnabled' -Value {toast} -Type DWord -Force\"");
            }

            await _service.ReadStateAsync(item);
            item.IsChecking = false;
            StatusMessage = $"{item.Name} \u2014 {item.StatusText.ToLowerInvariant()} successfully.";
        }
        else
        {
            StatusMessage = $"{item.Name} \u2014 failed. Run as administrator.";
        }
    }
}
