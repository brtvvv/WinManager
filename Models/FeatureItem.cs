namespace WinManager.Models;

public class FeatureItem
{
    public FeatureItem(string name, string powerShellCommand, string buttonLabel = "Run",
        string? description = null)
    {
        Name = name;
        PowerShellCommand = powerShellCommand;
        ButtonLabel = buttonLabel;
        Description = description;
    }

    public string Name { get; }
    public string PowerShellCommand { get; }
    public string ButtonLabel { get; }
    public string? Description { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
}
