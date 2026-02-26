namespace WinManager.Models;

public class OptimizationCategoryItem
{
    public string Key { get; }
    public string Name { get; }
    public string Description { get; }

    public OptimizationCategoryItem(string key, string name, string description)
    {
        Key = key;
        Name = name;
        Description = description;
    }
}
