namespace WinManager.Models;

public class FeatureItem
{
    public FeatureItem(string name, string powerShellCommand, string buttonLabel = "Run")
    {
        Name = name;
        PowerShellCommand = powerShellCommand;
        ButtonLabel = buttonLabel;
    }

    public string Name { get; }
    public string PowerShellCommand { get; }
    public string ButtonLabel { get; }
}
