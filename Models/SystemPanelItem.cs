namespace WinManager.Models;

public class SystemPanelItem
{
    public SystemPanelItem(string name, string fileName, string? arguments = null)
    {
        Name = name;
        FileName = fileName;
        Arguments = arguments;
    }

    public string Name { get; }
    public string FileName { get; }
    public string? Arguments { get; }
}
