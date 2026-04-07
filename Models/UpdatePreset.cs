using WinManager.Common;

namespace WinManager.Models;

public class UpdatePreset : ObservableObject
{
    private bool _isActive;

    public UpdatePreset(string name, string description, bool[] targetStates,
        string? badge = null, bool isWarning = false)
    {
        Name = name;
        Description = description;
        TargetStates = targetStates;
        Badge = badge;
        IsWarning = isWarning;
    }

    public string Name { get; }
    public string Description { get; }
    public bool[] TargetStates { get; }
    public string? Badge { get; }
    public bool IsWarning { get; }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
