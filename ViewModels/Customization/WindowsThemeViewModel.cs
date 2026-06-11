using System.Text;
using WinManager.Common;
using WinManager.Models;
using WinManager.Services;

namespace WinManager.ViewModels.Customization;

public class WindowsThemeViewModel : CustomizationCategoryViewModelBase
{
    private readonly PrivacySettingsService _service = new();
    private readonly ProcessRunner _runner = new();
    private string _statusMessage = string.Empty;
    private bool _showStatus;

    private readonly PrivacyToggleItem _darkMode;

    public WindowsThemeViewModel() : base("Windows Theme")
    {
        _darkMode = new("Dark Mode",
            "Switch between Windows dark and light application theme",
            "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 0, 1)
        {
            DefaultIsEnabled = false,
            ExtraValueNames = new[] { "SystemUsesLightTheme" }
        };

        ToggleGroups = new List<PrivacyToggleGroup>
        {
            new("Windows Theme", new List<PrivacyToggleItem>
            {
                _darkMode,
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
            if (item == _darkMode)
            {
                await BroadcastThemeChangeAsync();
                await _runner.RunAsync("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -Command \"Stop-Process -Name explorer -Force; Start-Process explorer\"");
            }

            await _service.ReadStateAsync(item);
            item.IsChecking = false;
            StatusMessage = $"{item.Name} \u2014 {item.StatusText.ToLowerInvariant()} successfully.";
        }
        else
        {
            StatusMessage = $"{item.Name} \u2014 failed. Check permissions.";
        }
    }

    private static readonly string ThemeBroadcastEncoded = Convert.ToBase64String(
        Encoding.Unicode.GetBytes(
            "Add-Type -TypeDefinition @'\r\n" +
            "using System; using System.Runtime.InteropServices;\r\n" +
            "public class TB {\r\n" +
            "  [DllImport(\"user32.dll\", CharSet=CharSet.Auto)]\r\n" +
            "  public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, string l, uint f, uint t, out IntPtr r);\r\n" +
            "}\r\n" +
            "'@ -EA SilentlyContinue\r\n" +
            "$r = [IntPtr]::Zero\r\n" +
            "[TB]::SendMessageTimeout([IntPtr]0xFFFF, 0x001A, [IntPtr]::Zero, 'ImmersiveColorSet', 2, 1000, [ref]$r)\r\n"));

    private async Task BroadcastThemeChangeAsync()
    {
        await _runner.RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {ThemeBroadcastEncoded}");
    }
}
