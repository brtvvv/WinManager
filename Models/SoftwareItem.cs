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
        string? description = null)
    {
        Id = id;
        Name = name;
        WingetId = wingetId;
        DetectionKeywords = detectionKeywords?.ToList() ?? new List<string>();
        Description = description ?? string.Empty;
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

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

    public bool SupportsInstall => !string.IsNullOrWhiteSpace(WingetId);
}

