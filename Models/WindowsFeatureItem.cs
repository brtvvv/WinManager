using WinManager.Common;

namespace WinManager.Models;

public class WindowsFeatureItem : ObservableObject
{
    private bool _isEnabled;
    private bool _isChecking = true;

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
            }
        }
    }

    public string ButtonLabel => IsEnabled ? "Disable" : "Enable";
    public string StatusText => IsChecking ? "Checking..." : (IsEnabled ? "Enabled" : "Disabled");
}
