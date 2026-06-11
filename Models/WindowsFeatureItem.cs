using WinManager.Common;

namespace WinManager.Models;

public class WindowsFeatureItem : ObservableObject
{
    private bool _isEnabled;
    private bool _isChecking = true;
    private bool _isNotAvailable;

    public WindowsFeatureItem(string name, string windowsFeatureName)
    {
        Name = name;
        WindowsFeatureName = windowsFeatureName;
    }

    public string Name { get; }
    public string WindowsFeatureName { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                Notify(nameof(ButtonLabel));
                Notify(nameof(StatusText));
            }
        }
    }

    public bool IsChecking
    {
        get => _isChecking;
        set
        {
            if (SetProperty(ref _isChecking, value))
            {
                Notify(nameof(StatusText));
                Notify(nameof(IsActionable));
            }
        }
    }

    // True when Get-WindowsOptionalFeature reports no such feature on this
    // edition (Home strips several optional features entirely). When set the
    // toggle button is disabled and the status reads "Not available".
    public bool IsNotAvailable
    {
        get => _isNotAvailable;
        set
        {
            if (SetProperty(ref _isNotAvailable, value))
            {
                Notify(nameof(ButtonLabel));
                Notify(nameof(StatusText));
                Notify(nameof(IsActionable));
            }
        }
    }

    public bool IsActionable => !IsChecking && !IsNotAvailable;

    public string ButtonLabel => IsNotAvailable
        ? "N/A"
        : (IsEnabled ? "Disable" : "Enable");

    public string StatusText => IsNotAvailable
        ? "Not available"
        : (IsChecking ? "Checking..." : (IsEnabled ? "Enabled" : "Disabled"));
}
