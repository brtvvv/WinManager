using WinManager.Common;

namespace WinManager.Models;

public class UacOption : ObservableObject
{
    private bool _isSelected;

    public UacOption(UacLevel level, string name, string description)
    {
        Level = level;
        Name = name;
        Description = description;
    }

    public UacLevel Level { get; }

    public string Name { get; }

    public string Description { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
