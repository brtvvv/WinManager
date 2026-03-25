using WinManager.Common;

namespace WinManager.Models;

public class PrivacyToggleItem : ObservableObject
{
    private bool _isEnabled;
    private bool _isChecking = true;

    public PrivacyToggleItem(string name, string description,
        string hive, string path, string valueName,
        object enabledValue, object disabledValue)
    {
        Name = name;
        Description = description;
        Hive = hive;
        Path = path;
        ValueName = valueName;
        EnabledValue = enabledValue;
        DisabledValue = disabledValue;
    }

    public string Name { get; }
    public string Description { get; }
    public string Hive { get; }
    public string Path { get; }
    public string ValueName { get; }
    public object EnabledValue { get; }
    public object DisabledValue { get; }

    public bool IsStringValue { get; init; }
    public string[]? ExtraValueNames { get; init; }
    public string? ServiceName { get; init; }
    public bool DeleteOnEnable { get; init; }
    public bool DefaultIsEnabled { get; init; } = true;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                Notify(nameof(StatusText));
                Notify(nameof(ButtonLabel));
            }
        }
    }

    public bool IsChecking
    {
        get => _isChecking;
        set
        {
            if (SetProperty(ref _isChecking, value))
                Notify(nameof(StatusText));
        }
    }

    public string StatusText => IsChecking ? "Checking..." : (IsEnabled ? "Enabled" : "Disabled");
    public string ButtonLabel => IsEnabled ? "Disable" : "Enable";

    public static PrivacyToggleItem ForService(string name, string description, string serviceName)
        => new(name, description, "", "", "", 0, 0) { ServiceName = serviceName };
}
