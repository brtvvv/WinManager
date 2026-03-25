namespace WinManager.Models;

public class PrivacyToggleGroup
{
    public PrivacyToggleGroup(string name, IReadOnlyList<PrivacyToggleItem> items)
    {
        Name = name;
        Items = items;
    }

    public string Name { get; }
    public IReadOnlyList<PrivacyToggleItem> Items { get; }
}
