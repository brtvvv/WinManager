using WinManager.Common;

namespace WinManager.Models;

/// <summary>
/// Represents a single app (Windows or external) with metadata and selection/status for UI.
/// </summary>
public class SoftwareItem : ObservableObject
{
    private AppStatus _status;
    private bool _isSelected;
    private bool _isBusy;
    private string _statusNote = string.Empty;

    public SoftwareItem(
        string id,
        string name,
        string? wingetId = null,
        IEnumerable<string>? detectionKeywords = null,
        string? description = null,
        string? category = null,
        string? iconUrl = null)
    {
        Id = id;
        Name = name;
        WingetId = wingetId;
        DetectionKeywords = detectionKeywords?.ToList() ?? new List<string>();
        Description = description ?? string.Empty;
        Category = category ?? string.Empty;
        IconUrl = iconUrl;
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public string Category { get; }

    public string? IconUrl { get; }

    public string? WingetId { get; }

    public List<string> DetectionKeywords { get; }

    public AppStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string StatusNote
    {
        get => _statusNote;
        set => SetProperty(ref _statusNote, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Appx package Name(s) detected as installed (e.g. Microsoft.WindowsAlarms). Used for uninstall.</summary>
    public List<string> InstalledPackageNames { get; } = new();

    public bool SupportsInstall => !string.IsNullOrWhiteSpace(WingetId);
}

