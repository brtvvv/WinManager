using WinManager.Common;
using WinManager.Models;
using WinManager.Services;

namespace WinManager.ViewModels.Optimization;

public class NotificationsViewModel : OptimizationCategoryViewModelBase
{
    private readonly PrivacySettingsService _service = new();
    private string _statusMessage = string.Empty;
    private bool _showStatus;

    public NotificationsViewModel() : base("Notifications")
    {
        ToggleGroups = new List<PrivacyToggleGroup>
        {
            new("Notifications", new List<PrivacyToggleItem>
            {
                // ToastEnabled only gates push-based toasts; many notification
                // sources bypass it. NoToastApplicationNotification under the
                // Explorer policy key is the system-wide kill switch for all
                // toast notifications. Enabled=0 (no policy = notifications on),
                // Disabled=1 (policy set to 1 = notifications globally off).
                new("Show Notifications",
                    "Enable or disable all Windows notifications globally",
                    "HKCU", @"SOFTWARE\Policies\Microsoft\Windows\CurrentVersion\PushNotifications",
                    "NoToastApplicationNotification", 0, 1),

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
